using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace GTBook {
    public sealed class UpdateCheckResult {
        public bool Checked;
        public bool UpdateAvailable;
        public bool NoPublishedReleases;
        public string CurrentVersion = "";
        public string LatestVersion = "";
        public string TagName = "";
        public string ReleaseTitle = "";
        public string ReleaseNotes = "";
        public string PublishedAt = "";
        public string HtmlUrl = AppUpdater.ReleasesPageUrl;
        public string AssetName = "";
        public string DownloadUrl = "";
        public string Sha256AssetUrl = "";
        public string ExpectedSha256 = "";
        public long AssetSize;
        public string Summary = "";
        public string Error = "";
    }

    public sealed class SemVersion : IComparable<SemVersion>, IEquatable<SemVersion> {
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }
        public string Prerelease { get; private set; }
        public string BuildMetadata { get; private set; }
        public bool IsPrerelease { get { return !string.IsNullOrEmpty(Prerelease); } }

        public SemVersion(int major, int minor, int patch = 0, string prerelease = "", string buildMetadata = "") {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = prerelease ?? "";
            BuildMetadata = buildMetadata ?? "";
        }

        public static bool TryParse(string raw, out SemVersion result) {
            result = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string s = raw.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1).Trim();

            string buildMeta = "";
            int plusIdx = s.IndexOf('+');
            if (plusIdx >= 0) {
                buildMeta = s.Substring(plusIdx + 1);
                s = s.Substring(0, plusIdx);
            }

            string prerelease = "";
            int dashIdx = s.IndexOf('-');
            if (dashIdx >= 0) {
                prerelease = s.Substring(dashIdx + 1);
                s = s.Substring(0, dashIdx);
            }

            string[] parts = s.Split('.');
            if (parts.Length < 1 || parts.Length > 4) return false;
            int[] nums = new int[3];
            for (int i = 0; i < Math.Min(3, parts.Length); i++) {
                int n;
                if (!int.TryParse(parts[i], out n) || n < 0) return false;
                nums[i] = n;
            }
            if (parts.Length == 1 && nums[0] == 0 && string.IsNullOrEmpty(prerelease)) return false;

            result = new SemVersion(nums[0], nums[1], nums[2], prerelease, buildMeta);
            return true;
        }

        public static SemVersion Parse(string raw) {
            SemVersion sem;
            if (!TryParse(raw, out sem)) throw new FormatException("Invalid SemVer: " + raw);
            return sem;
        }

        public int CompareTo(SemVersion other) {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;

            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            if (Patch != other.Patch) return Patch.CompareTo(other.Patch);

            if (IsPrerelease && !other.IsPrerelease) return -1;
            if (!IsPrerelease && other.IsPrerelease) return 1;
            if (!IsPrerelease && !other.IsPrerelease) return 0;

            string[] aIds = Prerelease.Split('.');
            string[] bIds = other.Prerelease.Split('.');
            int minLen = Math.Min(aIds.Length, bIds.Length);
            for (int i = 0; i < minLen; i++) {
                string aId = aIds[i];
                string bId = bIds[i];
                int aNum, bNum;
                bool aIsNum = int.TryParse(aId, out aNum);
                bool bIsNum = int.TryParse(bId, out bNum);
                if (aIsNum && bIsNum) {
                    if (aNum != bNum) return aNum.CompareTo(bNum);
                } else if (aIsNum) {
                    return -1;
                } else if (bIsNum) {
                    return 1;
                } else {
                    int cmp = string.Compare(aId, bId, StringComparison.Ordinal);
                    if (cmp != 0) return cmp;
                }
            }
            return aIds.Length.CompareTo(bIds.Length);
        }

        public bool Equals(SemVersion other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Major == other.Major && Minor == other.Minor && Patch == other.Patch &&
                   string.Equals(Prerelease, other.Prerelease, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) {
            return Equals(obj as SemVersion);
        }

        public override int GetHashCode() {
            unchecked {
                int hash = (Major * 397) ^ (Minor * 17) ^ Patch;
                hash = (hash * 397) ^ (Prerelease != null ? StringComparer.Ordinal.GetHashCode(Prerelease) : 0);
                return hash;
            }
        }

        public override string ToString() {
            var sb = new StringBuilder();
            sb.Append(Major).Append('.').Append(Minor).Append('.').Append(Patch);
            if (!string.IsNullOrEmpty(Prerelease)) sb.Append('-').Append(Prerelease);
            if (!string.IsNullOrEmpty(BuildMetadata)) sb.Append('+').Append(BuildMetadata);
            return sb.ToString();
        }

        public static bool operator <(SemVersion left, SemVersion right) {
            return Comparer<SemVersion>.Default.Compare(left, right) < 0;
        }
        public static bool operator >(SemVersion left, SemVersion right) {
            return Comparer<SemVersion>.Default.Compare(left, right) > 0;
        }
        public static bool operator <=(SemVersion left, SemVersion right) {
            return Comparer<SemVersion>.Default.Compare(left, right) <= 0;
        }
        public static bool operator >=(SemVersion left, SemVersion right) {
            return Comparer<SemVersion>.Default.Compare(left, right) >= 0;
        }
        public static bool operator ==(SemVersion left, SemVersion right) {
            return Equals(left, right);
        }
        public static bool operator !=(SemVersion left, SemVersion right) {
            return !Equals(left, right);
        }
    }

    public static class AppUpdater {
        public const string Owner = "yashwantmandal26";
        public const string Repo = "GTBookEssentials";
        public const string LatestReleaseApiUrl = "https://api.github.com/repos/yashwantmandal26/GTBookEssentials/releases/latest";
        public const string ReleasesPageUrl = "https://github.com/yashwantmandal26/GTBookEssentials/releases";

        static readonly string[] AllowedAssetCdnHosts = {
            "objects.githubusercontent.com",
            "release-assets.githubusercontent.com",
            "github-releases.githubusercontent.com"
        };

        public static Version CurrentVersion {
            get {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
            }
        }

        public static string FormatVersion(Version v) {
            if (v == null) return "v0.0";
            if (v.Revision > 0) return "v" + v.Major + "." + v.Minor + "." + v.Build + "." + v.Revision;
            if (v.Build > 0) return "v" + v.Major + "." + v.Minor + "." + v.Build;
            return "v" + v.Major + "." + v.Minor;
        }

        public static string FormatSemVersion(SemVersion v) {
            if (v == null) return "v0.0";
            var sb = new StringBuilder("v");
            sb.Append(v.Major).Append('.').Append(v.Minor);
            if (v.Patch > 0 || v.IsPrerelease) sb.Append('.').Append(v.Patch);
            if (v.IsPrerelease) sb.Append('-').Append(v.Prerelease);
            return sb.ToString();
        }

        public static Version ParseVersion(string raw) {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string clean = raw.Trim();
            if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(1);
            int suffix = clean.IndexOfAny(new[] { '-', '+' });
            if (suffix >= 0) clean = clean.Substring(0, suffix);
            string[] parts = clean.Split('.');
            if (parts.Length < 1 || parts.Length > 4) return null;
            int[] nums = { 0, 0, 0, 0 };
            for (int i = 0; i < parts.Length; i++) {
                int n;
                if (!int.TryParse(parts[i], out n) || n < 0) return null;
                nums[i] = n;
            }
            if (parts.Length == 1 && nums[0] == 0) return null;
            return new Version(nums[0], nums[1], nums[2], nums[3]);
        }

        public static bool IsNewer(string remoteTag, Version current) {
            return IsNewer(remoteTag, current, false);
        }

        public static bool IsNewer(string remoteTag, Version current, bool allowPrerelease) {
            if (current == null) return false;
            SemVersion cur = new SemVersion(current.Major, current.Minor, Math.Max(0, current.Build));
            return IsNewer(remoteTag, cur, allowPrerelease);
        }

        public static bool IsNewer(string remoteTag, SemVersion current, bool allowPrerelease = false) {
            if (current == null) return false;
            SemVersion remote;
            if (!SemVersion.TryParse(remoteTag, out remote)) return false;
            if (!allowPrerelease && remote.IsPrerelease) return false;
            return remote > current;
        }

        public static bool IsTrustedDownloadUrl(string url) {
            Uri uri;
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
            string host = uri.Host.ToLowerInvariant();
            string path = uri.AbsolutePath;
            if (host == "github.com") {
                string prefix = "/" + Owner + "/" + Repo + "/";
                string repoRoot = "/" + Owner + "/" + Repo;
                return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(path, repoRoot, StringComparison.OrdinalIgnoreCase);
            }
            if (host == "api.github.com") {
                string prefix = "/repos/" + Owner + "/" + Repo + "/";
                return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            for (int i = 0; i < AllowedAssetCdnHosts.Length; i++) {
                if (host == AllowedAssetCdnHosts[i] || host.EndsWith("." + AllowedAssetCdnHosts[i], StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static string ExtractSha256Hex(string rawDigestOrText) {
            if (string.IsNullOrWhiteSpace(rawDigestOrText)) return "";
            string trimmed = rawDigestOrText.Trim();
            if (trimmed.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed.Substring(7).Trim();
            string token = trimmed.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
            if (token.Length != 64) return "";
            for (int i = 0; i < token.Length; i++) {
                char c = token[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return "";
            }
            return token.ToLowerInvariant();
        }

        public static UpdateCheckResult ParseReleaseJson(string json, Version current = null) {
            return ParseReleaseJson(json, current, false);
        }

        public static UpdateCheckResult ParseReleaseJson(string json, Version current, bool allowPrerelease) {
            var cur = current ?? CurrentVersion;
            SemVersion curSem = new SemVersion(cur.Major, cur.Minor, Math.Max(0, cur.Build));
            return ParseReleaseJson(json, curSem, allowPrerelease);
        }

        public static UpdateCheckResult ParseReleaseJson(string json, SemVersion current, bool allowPrerelease = false) {
            var cur = current ?? new SemVersion(CurrentVersion.Major, CurrentVersion.Minor, Math.Max(0, CurrentVersion.Build));
            var result = new UpdateCheckResult { CurrentVersion = FormatSemVersion(cur) };
            if (string.IsNullOrWhiteSpace(json)) {
                result.Error = "Empty response from GitHub.";
                return result;
            }
            var dict = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (dict == null) {
                result.Error = "Unexpected GitHub release response.";
                return result;
            }
            bool draft = dict.ContainsKey("draft") && Convert.ToBoolean(dict["draft"]);
            bool prerelease = dict.ContainsKey("prerelease") && Convert.ToBoolean(dict["prerelease"]);
            string tag = dict.ContainsKey("tag_name") ? Convert.ToString(dict["tag_name"]) ?? "" : "";
            SemVersion remoteSem;
            bool parsedSem = SemVersion.TryParse(tag, out remoteSem);
            bool isPrerelease = prerelease || (parsedSem && remoteSem.IsPrerelease);

            if (draft || (!allowPrerelease && isPrerelease)) {
                result.Checked = true;
                result.Summary = "You're up to date (" + result.CurrentVersion + ").";
                return result;
            }
            string name = dict.ContainsKey("name") ? Convert.ToString(dict["name"]) ?? "" : "";
            string body = dict.ContainsKey("body") ? Convert.ToString(dict["body"]) ?? "" : "";
            string html = dict.ContainsKey("html_url") ? Convert.ToString(dict["html_url"]) ?? ReleasesPageUrl : ReleasesPageUrl;
            string published = dict.ContainsKey("published_at") ? Convert.ToString(dict["published_at"]) ?? "" : "";
            result.TagName = tag;
            result.LatestVersion = parsedSem ? FormatSemVersion(remoteSem) : (ParseVersion(tag) != null ? FormatVersion(ParseVersion(tag)) : tag);
            result.ReleaseTitle = string.IsNullOrWhiteSpace(name) ? result.LatestVersion : name.Trim();
            result.ReleaseNotes = body.Trim();
            result.HtmlUrl = IsTrustedDownloadUrl(html) ? html : ReleasesPageUrl;
            result.PublishedAt = published;
            result.Checked = true;

            object[] assets = dict.ContainsKey("assets") ? dict["assets"] as object[] : null;
            if (assets != null) {
                var byName = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, object> chosen = null;
                int bestRank = int.MaxValue;
                foreach (object entry in assets) {
                    var asset = entry as Dictionary<string, object>;
                    if (asset == null) continue;
                    string aName = asset.ContainsKey("name") ? Convert.ToString(asset["name"]) ?? "" : "";
                    string aUrl = asset.ContainsKey("browser_download_url") ? Convert.ToString(asset["browser_download_url"]) ?? "" : "";
                    if (string.IsNullOrEmpty(aName) || !IsTrustedDownloadUrl(aUrl)) continue;
                    byName[aName] = asset;
                    int rank = int.MaxValue;
                    if (string.Equals(aName, "GTBOOK-Essentials-Setup.exe", StringComparison.OrdinalIgnoreCase)) rank = 0;
                    else if (aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && aName.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0) rank = 1;
                    else if (aName.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase) && aName.IndexOf("source", StringComparison.OrdinalIgnoreCase) < 0) rank = 2;
                    else if (aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) rank = 3;
                    if (rank < bestRank) { bestRank = rank; chosen = asset; }
                }
                if (chosen != null) {
                    result.AssetName = Convert.ToString(chosen["name"]) ?? "";
                    result.DownloadUrl = Convert.ToString(chosen["browser_download_url"]) ?? "";
                    if (chosen.ContainsKey("size")) result.AssetSize = Convert.ToInt64(chosen["size"]);
                    if (chosen.ContainsKey("digest")) result.ExpectedSha256 = ExtractSha256Hex(Convert.ToString(chosen["digest"]));
                    Dictionary<string, object> shaAsset;
                    if (byName.TryGetValue(result.AssetName + ".sha256", out shaAsset)) {
                        string shaUrl = Convert.ToString(shaAsset["browser_download_url"]) ?? "";
                        if (IsTrustedDownloadUrl(shaUrl)) result.Sha256AssetUrl = shaUrl;
                    }
                }
            }

            result.UpdateAvailable = IsNewer(tag, cur, allowPrerelease);
            result.Summary = result.UpdateAvailable
                ? "New version " + result.LatestVersion + " is available on GitHub (current: " + result.CurrentVersion + ")."
                : "You're up to date (" + result.CurrentVersion + ").";
            return result;
        }

        static void ConfigureTls() {
            try {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
            } catch {}
        }

        static HttpWebRequest CreateRequest(string url, string accept) {
            if (!IsTrustedDownloadUrl(url)) throw new InvalidOperationException("Untrusted update URL.");
            ConfigureTls();
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.UserAgent = "GTBOOK-Essentials/" + FormatVersion(CurrentVersion);
            req.Accept = accept;
            req.Timeout = 15000;
            req.ReadWriteTimeout = 30000;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return req;
        }

        public static Task<UpdateCheckResult> CheckLatestAsync() {
            return Task.Run(() => {
                var cur = CurrentVersion;
                string curLabel = FormatVersion(cur);
                try {
                    var req = CreateRequest(LatestReleaseApiUrl, "application/vnd.github+json");
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var stream = resp.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                        return ParseReleaseJson(reader.ReadToEnd(), cur);
                    }
                } catch (WebException wex) {
                    var httpResp = wex.Response as HttpWebResponse;
                    if (httpResp != null && httpResp.StatusCode == HttpStatusCode.NotFound) {
                        return new UpdateCheckResult {
                            Checked = true,
                            UpdateAvailable = false,
                            NoPublishedReleases = true,
                            CurrentVersion = curLabel,
                            LatestVersion = curLabel,
                            Summary = "You're on " + curLabel + ". No newer GitHub release is published yet."
                        };
                    }
                    return new UpdateCheckResult {
                        Checked = false,
                        CurrentVersion = curLabel,
                        Error = wex.Message,
                        Summary = "Could not check GitHub updates: " + wex.Message
                    };
                } catch (Exception ex) {
                    return new UpdateCheckResult {
                        Checked = false,
                        CurrentVersion = curLabel,
                        Error = ex.Message,
                        Summary = "Could not check GitHub updates: " + ex.Message
                    };
                }
            });
        }

        static string ComputeSha256(string filePath) {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath)) {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(64);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        static string FetchText(string url) {
            var req = CreateRequest(url, "text/plain");
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                return reader.ReadToEnd();
            }
        }

        static void CleanupOldUpdates(string updatesDir) {
            UpdateStaging.SafeCleanup(updatesDir);
        }

        internal static string ResolveExpectedSha256(string inlineSha256, string sha256AssetUrl, Func<string, string> fetchText) {
            if (!string.IsNullOrEmpty(inlineSha256)) return inlineSha256;
            if (string.IsNullOrEmpty(sha256AssetUrl)) {
                throw new IOException("Release package has no SHA-256 checksum to verify.");
            }
            string digest;
            try {
                digest = ExtractSha256Hex(fetchText(sha256AssetUrl));
            } catch (Exception ex) {
                throw new IOException("Could not verify release checksum file: " + ex.Message, ex);
            }
            if (string.IsNullOrEmpty(digest)) {
                throw new IOException("Release checksum file did not contain a valid SHA-256 digest.");
            }
            return digest;
        }

        public static Task DownloadAndLaunchAsync(UpdateCheckResult info, Action<int> onProgress, CancellationToken token) {
            return Task.Run(() => {
                if (info == null || string.IsNullOrEmpty(info.DownloadUrl) || !IsTrustedDownloadUrl(info.DownloadUrl)) {
                    throw new InvalidOperationException("No downloadable release package was found in the GitHub release.");
                }
                string updatesDir = UpdateStaging.RootPath;
                UpdateStaging.EnsureSecureDirectory(updatesDir, allowTestFixture: !UpdateStaging.IsElevated());
                CleanupOldUpdates(updatesDir);
                string safeName = Path.GetFileName(info.AssetName);
                if (string.IsNullOrWhiteSpace(safeName)) safeName = "GTBOOK-Essentials-Setup.exe";
                foreach (char bad in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(bad, '_');
                string tempFile = Path.Combine(updatesDir, safeName + "." + Guid.NewGuid().ToString("N") + ".part");
                string finalFile = Path.Combine(updatesDir, safeName);

                string expectedHash;
                try {
                    var req = CreateRequest(info.DownloadUrl, "application/octet-stream");
                    req.Timeout = 30000;
                    req.ReadWriteTimeout = 60000;
                    using (var resp = (HttpWebResponse)req.GetResponse()) {
                        long total = resp.ContentLength > 0 ? resp.ContentLength : info.AssetSize;
                        using (var net = resp.GetResponseStream())
                        using (var fs = File.Create(tempFile)) {
                            byte[] buffer = new byte[65536];
                            long received = 0;
                            int lastPct = -1;
                            int read;
                            while ((read = net.Read(buffer, 0, buffer.Length)) > 0) {
                                token.ThrowIfCancellationRequested();
                                fs.Write(buffer, 0, read);
                                received += read;
                                if (onProgress != null && total > 0) {
                                    int pct = (int)Math.Max(0, Math.Min(100, received * 100L / total));
                                    if (pct != lastPct) { lastPct = pct; onProgress(pct); }
                                }
                            }
                        }
                    }

                    expectedHash = ResolveExpectedSha256(info.ExpectedSha256, info.Sha256AssetUrl, FetchText);
                    string actualHash = ComputeSha256(tempFile);
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase)) {
                        throw new IOException("Update checksum mismatch. Expected " + expectedHash + ", got " + actualHash + ".");
                    }

                    if (File.Exists(finalFile)) File.Delete(finalFile);
                    File.Move(tempFile, finalFile);
                } catch {
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch {}
                    throw;
                }

                // Verify the final protected payload inside secure staging before execution
                UpdateStaging.VerifyPayloadFile(finalFile, expectedHash, updatesDir);

                if (finalFile.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
                    Process.Start(new ProcessStartInfo(finalFile) { UseShellExecute = true });
                } else if (finalFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                    string extractDir = Path.Combine(updatesDir, "extract-" + Guid.NewGuid().ToString("N"));
                    UpdateStaging.EnsureSecureDirectory(extractDir, allowTestFixture: !UpdateStaging.IsElevated());
                    UpdateStaging.ExtractZipSafely(finalFile, extractDir);
                    string[] installers = Directory.GetFiles(extractDir, "Install.ps1", SearchOption.AllDirectories);
                    if (installers.Length == 0) throw new IOException("Downloaded ZIP does not contain Install.ps1.");
                    string installerPath = Path.GetFullPath(installers[0]);
                    UpdateStaging.AssertPathLineageNotReparsePoint(installerPath);
                    string powershellPath = UpdateStaging.ResolveSystemExecutable("powershell.exe");
                    Process.Start(new ProcessStartInfo(powershellPath, "-NoProfile -ExecutionPolicy Bypass -File \"" + installerPath + "\"") {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(installerPath)
                    });
                } else {
                    Process.Start(new ProcessStartInfo(info.HtmlUrl) { UseShellExecute = true });
                }
            }, token);
        }
    }
}
