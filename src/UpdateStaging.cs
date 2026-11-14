using System;
using System.IO;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

namespace GTBook {
    public static class UpdateStaging {
        public static string RootOverride = null;

        public static string DefaultStagingRoot {
            get {
                string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                return Path.Combine(commonAppData, "GTBookEssentials", "staging");
            }
        }

        public static string RootPath {
            get { return RootOverride ?? DefaultStagingRoot; }
        }

        public static bool IsElevated() {
            try {
                using (var identity = WindowsIdentity.GetCurrent()) {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            } catch {
                return false;
            }
        }

        public static bool IsReparsePoint(string path) {
            if (string.IsNullOrEmpty(path)) return false;
            if (!File.Exists(path) && !Directory.Exists(path)) return false;
            try {
                FileAttributes attrs = File.GetAttributes(path);
                return (attrs & FileAttributes.ReparsePoint) != 0;
            } catch {
                return false;
            }
        }

        public static void AssertPathLineageNotReparsePoint(string path) {
            string fullPath = Path.GetFullPath(path);
            string current = fullPath;
            while (!string.IsNullOrEmpty(current)) {
                if (Directory.Exists(current) || File.Exists(current)) {
                    if (IsReparsePoint(current)) {
                        throw new SecurityException("Reparse point or symbolic link detected in update staging lineage: " + current);
                    }
                }
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) {
                    break;
                }
                current = parent;
            }
        }

        public static void ValidateArchivePath(string entryFullName, string destinationDirectory) {
            if (string.IsNullOrEmpty(entryFullName)) {
                throw new ArgumentException("Archive contains an entry with an empty or null path.");
            }
            if (entryFullName.Contains("..") ||
                entryFullName.StartsWith("/") ||
                entryFullName.StartsWith("\\") ||
                (entryFullName.Length > 1 && entryFullName[1] == ':')) {
                throw new ArgumentException("Unsafe archive entry path detected: " + entryFullName);
            }
            string fullDest = Path.GetFullPath(destinationDirectory);
            if (!fullDest.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                fullDest += Path.DirectorySeparatorChar;
            }
            string target = Path.GetFullPath(Path.Combine(destinationDirectory, entryFullName));
            if (!target.StartsWith(fullDest, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException("Archive entry escapes target destination directory: " + entryFullName);
            }
        }

        public static string ResolveSystemExecutable(string executableName) {
            string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string fullPath;
            if (string.Equals(executableName, "powershell.exe", StringComparison.OrdinalIgnoreCase)) {
                fullPath = Path.Combine(systemDir, "WindowsPowerShell\\v1.0\\powershell.exe");
            } else {
                fullPath = Path.Combine(systemDir, executableName);
            }
            if (!File.Exists(fullPath)) {
                throw new FileNotFoundException("Trusted system executable not found: " + fullPath, fullPath);
            }
            AssertPathLineageNotReparsePoint(fullPath);
            return fullPath;
        }

        public static void EnsureSecureDirectory(string path, bool allowTestFixture = false) {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            string fullPath = Path.GetFullPath(path);
            AssertPathLineageNotReparsePoint(fullPath);

            if (!Directory.Exists(fullPath)) {
                Directory.CreateDirectory(fullPath);
            }

            AssertPathLineageNotReparsePoint(fullPath);

            var dirInfo = new DirectoryInfo(fullPath);
            var sec = new DirectorySecurity();
            sec.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            sec.AddAccessRule(new FileSystemAccessRule(
                adminSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            sec.AddAccessRule(new FileSystemAccessRule(
                systemSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            bool elevated = IsElevated();
            if (elevated) {
                try {
                    sec.SetOwner(adminSid);
                    sec.SetGroup(adminSid);
                } catch (Exception ex) {
                    if (!allowTestFixture) {
                        throw new SecurityException("Failed to establish administrator ownership on update staging directory: " + ex.Message, ex);
                    }
                }
            } else {
                if (!allowTestFixture) {
                    throw new SecurityException("Secure update staging requires administrator privileges. Elevate the application before updating.");
                }
                // Under test fixtures in medium integrity, use current user as owner
                using (var currentIdentity = WindowsIdentity.GetCurrent()) {
                    sec.SetOwner(currentIdentity.User);
                    sec.AddAccessRule(new FileSystemAccessRule(
                        currentIdentity.User,
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                }
            }

            try {
                dirInfo.SetAccessControl(sec);
            } catch (Exception ex) {
                if (!allowTestFixture) {
                    throw new SecurityException("Failed to apply secure ACL on update staging directory: " + ex.Message, ex);
                }
            }
        }

        public static string ComputeFileSha256(string filePath) {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(64);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static void VerifyPayloadFile(string filePath, string expectedSha256, string stagingRoot) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                throw new FileNotFoundException("Update payload file does not exist: " + filePath, filePath);
            }
            if (string.IsNullOrEmpty(expectedSha256)) {
                throw new SecurityException("Release payload verification failed: expected checksum is missing.");
            }
            string fullFilePath = Path.GetFullPath(filePath);
            string fullRoot = Path.GetFullPath(stagingRoot);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                fullRoot += Path.DirectorySeparatorChar;
            }
            if (!fullFilePath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) {
                throw new SecurityException("Update payload is not located inside the secure staging directory.");
            }
            AssertPathLineageNotReparsePoint(fullFilePath);

            string actualSha = ComputeFileSha256(fullFilePath);
            if (!string.Equals(actualSha, expectedSha256, StringComparison.OrdinalIgnoreCase)) {
                throw new IOException("Update payload checksum verification failed. Expected " + expectedSha256 + ", got " + actualSha + ".");
            }
        }

        public static void ExtractZipSafely(string zipFilePath, string destinationDirectory) {
            string fullZip = Path.GetFullPath(zipFilePath);
            string fullDest = Path.GetFullPath(destinationDirectory);
            AssertPathLineageNotReparsePoint(fullZip);
            AssertPathLineageNotReparsePoint(fullDest);

            string powershellPath = ResolveSystemExecutable("powershell.exe");

            // PowerShell script extracts entry by entry, validating safe canonical targets
            string extractionScript =
                "$ErrorActionPreference = 'Stop'; " +
                "$zipPath = '" + fullZip.Replace("'", "''") + "'; " +
                "$dest = '" + fullDest.Replace("'", "''") + "'; " +
                "Add-Type -AssemblyName System.IO.Compression.FileSystem; " +
                "$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath); " +
                "try { " +
                "  $destFull = [System.IO.Path]::GetFullPath($dest); " +
                "  if (-not $destFull.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) { $destFull += [System.IO.Path]::DirectorySeparatorChar }; " +
                "  foreach ($entry in $zip.Entries) { " +
                "    $name = $entry.FullName; " +
                "    if ([string]::IsNullOrWhiteSpace($name)) { continue }; " +
                "    if ($name.Contains('..') -or $name.StartsWith('/') -or $name.StartsWith('\\') -or ($name.Length -gt 1 -and $name[1] -eq ':')) { " +
                "      throw ('Unsafe archive path: ' + $name) " +
                "    }; " +
                "    $target = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($dest, $name)); " +
                "    if (-not $target.StartsWith($destFull, [System.StringComparison]::OrdinalIgnoreCase)) { " +
                "      throw ('Archive entry escapes destination: ' + $name) " +
                "    }; " +
                "    if ($name.EndsWith('/') -or $name.EndsWith('\\')) { " +
                "      [System.IO.Directory]::CreateDirectory($target) | Out-Null; " +
                "    } else { " +
                "      $parent = [System.IO.Path]::GetDirectoryName($target); " +
                "      if (-not [System.IO.Directory]::Exists($parent)) { [System.IO.Directory]::CreateDirectory($parent) | Out-Null }; " +
                "      [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true); " +
                "    } " +
                "  } " +
                "} finally { $zip.Dispose() }";

            var psi = new ProcessStartInfo(powershellPath, "-NoProfile -ExecutionPolicy Bypass -Command \"" + extractionScript.Replace("\"", "\\\"") + "\"") {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (var process = Process.Start(psi)) {
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) {
                    throw new IOException("Secure extraction failed (exit " + process.ExitCode + "): " + stderr.Trim());
                }
            }

            // Post-extraction verification: ensure no extracted file is a reparse point
            foreach (string file in Directory.GetFiles(fullDest, "*", SearchOption.AllDirectories)) {
                AssertPathLineageNotReparsePoint(file);
            }
        }

        public static void SafeCleanup(string stagingDirectory) {
            if (string.IsNullOrEmpty(stagingDirectory) || !Directory.Exists(stagingDirectory)) return;
            try {
                foreach (string file in Directory.GetFiles(stagingDirectory)) {
                    try {
                        string ext = Path.GetExtension(file);
                        if (string.Equals(ext, ".part", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".tmp", StringComparison.OrdinalIgnoreCase)) {
                            File.Delete(file);
                        }
                    } catch {}
                }
                foreach (string dir in Directory.GetDirectories(stagingDirectory, "extract-*")) {
                    try { Directory.Delete(dir, true); } catch {}
                }
            } catch {}
        }
    }
}
