using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GTBook {
    public sealed class DeviceSpecView : Control {
        public sealed class SpecItem {
            public string Category; // "Battery", "System", "CPU & RAM", "Graphics", "Storage"
            public string Section;  // "BATTERY HEALTH & CAPACITY", "POWER STATUS", "DEVICE", etc.
            public string Title;
            public string Subtitle;
            public float? BarPercent;
            public string BarText;
        }

        static readonly string[] Categories = { "Battery", "All", "System", "CPU & RAM", "Graphics", "Storage" };
        int selectedCategory = 0; // Battery first by default
        int hoveredCategory = -1;
        int currentPage = 0;
        int totalPages = 1;
        int hoveredPager = 0; // -1 prev, 1 next
        RectangleF prevPageBounds = RectangleF.Empty;
        RectangleF nextPageBounds = RectangleF.Empty;
        readonly List<SpecItem> items = new List<SpecItem>();
        internal List<SpecItem> Items { get { return items; } }
        string footerTimestamp = "";
        string lastMeasuredTime = "";

        public Action OnHeightChanged;
        public Action OnRefreshBattery;
        public int MaxHeight { get; set; }
        bool isRefreshingBattery = false;
        bool hoveredRefreshBtn = false;
        RectangleF refreshBtnBounds = RectangleF.Empty;

        int lastLayoutWidth = -1;
        int lastMaxHeight = 0;

        sealed class SpecRow {
            public string SectionHeader;
            public SpecItem Left;
            public SpecItem Right;
            public bool LeftIsBatteryHealth;
            public bool RightIsBatteryHealth;
            public int Height;
        }

        public DeviceSpecView() {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.White;
            Font = new Font("Segoe UI", 9.5f);
        }

        protected override void OnPaintBackground(PaintEventArgs e) {
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);
            if (Width > 0 && (Width != lastLayoutWidth || MaxHeight != lastMaxHeight)) {
                lastLayoutWidth = Width;
                lastMaxHeight = MaxHeight;
                RecalculateLayout();
            }
        }

        internal void SelectCategory(int index) {
            if (index < 0 || index >= Categories.Length) return;
            if (selectedCategory != index) {
                selectedCategory = index;
                currentPage = 0;
                RecalculateLayout();
                Invalidate();
            }
        }

        public void SetRefreshing(bool refreshing) {
            isRefreshingBattery = refreshing;
            Invalidate();
        }

        public void SetReport(string report) {
            items.Clear();
            footerTimestamp = "";
            isRefreshingBattery = false;
            currentPage = 0;
            if (string.IsNullOrWhiteSpace(report)) {
                items.Add(new SpecItem { Category = "Battery", Section = "BATTERY HEALTH & CAPACITY", Title = "Querying live battery readings…", Subtitle = "Reading battery firmware and ACPI capacity." });
                RecalculateLayout();
                Invalidate();
                return;
            }

            ParseReport(report);
            RecalculateLayout();
            Invalidate();
        }

        void ParseReport(string report) {
            var rawSections = new List<Tuple<string, List<string>>>();
            string currentHeader = "";
            var currentLines = new List<string>();

            string[] lines = report.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (string rawLine in lines) {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("Updated ", StringComparison.OrdinalIgnoreCase)) {
                    footerTimestamp = line;
                    lastMeasuredTime = line.Substring(8).Trim();
                    continue;
                }

                if (IsSectionHeader(line)) {
                    if (!string.IsNullOrEmpty(currentHeader) && currentLines.Count > 0) {
                        rawSections.Add(Tuple.Create(currentHeader, new List<string>(currentLines)));
                        currentLines.Clear();
                    }
                    currentHeader = line;
                } else {
                    if (string.IsNullOrEmpty(currentHeader)) currentHeader = "BATTERY";
                    currentLines.Add(line);
                }
            }
            if (!string.IsNullOrEmpty(currentHeader) && currentLines.Count > 0) {
                rawSections.Add(Tuple.Create(currentHeader, currentLines));
            }

            if (string.IsNullOrEmpty(lastMeasuredTime)) {
                lastMeasuredTime = DateTime.Now.ToString("dd MMM yyyy · HH:mm:ss");
            }

            foreach (var section in rawSections) {
                string header = section.Item1.ToUpperInvariant();
                var sLines = section.Item2;
                string cat = MapCategory(header);

                if (header.Contains("BATTERY CAPACITY")) {
                    float? health = null;
                    string sub = string.Join(" · ", sLines);
                    var m = Regex.Match(sub, @"Estimated health:\s*([0-9]+)%", RegexOptions.IgnoreCase);
                    if (m.Success) {
                        float h;
                        if (float.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out h)) health = h;
                    }
                    items.Add(new SpecItem {
                        Category = "Battery",
                        Section = "BATTERY HEALTH & CAPACITY",
                        Title = health.HasValue ? string.Format("Battery Health: {0:0}%", health.Value) : "Battery Capacity",
                        Subtitle = sLines.Count > 0 ? sLines[0] : "",
                        BarPercent = health,
                        BarText = health.HasValue ? string.Format("{0:0}% Health", health.Value) : null
                    });
                } else if (header.Contains("BATTERY")) {
                    string bat = sLines.Count > 0 ? sLines[0] : "Battery";
                    string pwr = sLines.Count > 1 ? sLines[1] : "";
                    items.Add(new SpecItem {
                        Category = "Battery",
                        Section = "POWER STATUS",
                        Title = bat,
                        Subtitle = pwr
                    });
                } else if (header.Contains("DEVICE") && !header.Contains("STORAGE")) {
                    string model = sLines.Count > 0 ? sLines[0] : "Infinix Laptop";
                    string ram = sLines.Count > 1 ? sLines[1] : "";
                    items.Add(new SpecItem {
                        Category = cat,
                        Section = "DEVICE & HARDWARE",
                        Title = model,
                        Subtitle = ram
                    });
                } else if (header.Contains("BIOS") || header.Contains("MOTHERBOARD")) {
                    string bios = sLines.Count > 0 ? sLines[0] : "BIOS";
                    string board = sLines.Count > 1 ? sLines[1] : "";
                    items.Add(new SpecItem {
                        Category = cat,
                        Section = "FIRMWARE & MOTHERBOARD",
                        Title = bios,
                        Subtitle = board.Length > 0 ? "Motherboard: " + board : ""
                    });
                } else if (header.Contains("WINDOWS")) {
                    string os = sLines.Count > 0 ? sLines[0] : "Windows";
                    string ver = sLines.Count > 1 ? sLines[1] : "";
                    string boot = sLines.Count > 2 ? sLines[2] : "";
                    items.Add(new SpecItem {
                        Category = cat,
                        Section = "OPERATING SYSTEM",
                        Title = os,
                        Subtitle = ver + (boot.Length > 0 ? " · " + boot : "")
                    });
                } else if (header.Contains("PROCESSOR")) {
                    string cpu = sLines.Count > 0 ? sLines[0] : "CPU";
                    string cores = sLines.Count > 1 ? sLines[1] : "";
                    items.Add(new SpecItem {
                        Category = cat,
                        Section = "PROCESSOR (CPU)",
                        Title = cpu,
                        Subtitle = cores
                    });
                } else if (header.Contains("MEMORY MODULES") || header == "MEMORY") {
                    for (int i = 0; i < sLines.Count; i += 2) {
                        string slot = sLines[i];
                        string part = (i + 1 < sLines.Count) ? sLines[i + 1] : "";
                        items.Add(new SpecItem {
                            Category = cat,
                            Section = "RAM MODULES",
                            Title = part.Length > 0 ? part : slot,
                            Subtitle = part.Length > 0 ? slot : "Installed Memory"
                        });
                    }
                } else if (header.Contains("GRAPHICS")) {
                    for (int i = 0; i < sLines.Count; i++) {
                        string gName = sLines[i];
                        string driver = (i + 1 < sLines.Count && sLines[i + 1].StartsWith("Driver", StringComparison.OrdinalIgnoreCase)) ? sLines[++i] : "";
                        string res = (i + 1 < sLines.Count && sLines[i + 1].StartsWith("Output", StringComparison.OrdinalIgnoreCase)) ? sLines[++i] : "";
                        items.Add(new SpecItem {
                            Category = cat,
                            Section = "GRAPHICS ADAPTER (GPU)",
                            Title = gName,
                            Subtitle = driver + (res.Length > 0 ? " · " + res : "")
                        });
                    }
                } else if (header.Contains("DISPLAYS")) {
                    foreach (string d in sLines) {
                        items.Add(new SpecItem {
                            Category = cat,
                            Section = "DISPLAY PANELS",
                            Title = d.Contains(":") ? d.Substring(0, d.IndexOf(':')).Trim() : "Display Panel",
                            Subtitle = d.Contains(":") ? d.Substring(d.IndexOf(':') + 1).Trim() : d
                        });
                    }
                } else if (header.Contains("STORAGE DEVICES")) {
                    foreach (string s in sLines) {
                        items.Add(new SpecItem {
                            Category = cat,
                            Section = "PHYSICAL STORAGE (DRIVES)",
                            Title = s,
                            Subtitle = "Internal NVMe / SSD Storage"
                        });
                    }
                } else if (header.Contains("STORAGE SPACE")) {
                    foreach (string s in sLines) {
                        float? percent = null;
                        var m = Regex.Match(s, @"([0-9\.]+)\s*GB\s*free\s*of\s*([0-9\.]+)\s*GB", RegexOptions.IgnoreCase);
                        if (m.Success) {
                            double free, total;
                            if (double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out free) &&
                                double.TryParse(m.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out total) && total > 0) {
                                percent = (float)Math.Max(0, Math.Min(100, 100 * (1 - (free / total))));
                            }
                        }
                        items.Add(new SpecItem {
                            Category = cat,
                            Section = "STORAGE PARTITIONS",
                            Title = s,
                            Subtitle = percent.HasValue ? string.Format("{0:0.#}% used", percent.Value) : "",
                            BarPercent = percent,
                            BarText = percent.HasValue ? string.Format("{0:0}% used", percent.Value) : null
                        });
                    }
                } else {
                    items.Add(new SpecItem {
                        Category = cat,
                        Section = header,
                        Title = sLines.Count > 0 ? sLines[0] : header,
                        Subtitle = sLines.Count > 1 ? string.Join(" · ", sLines.Skip(1)) : ""
                    });
                }
            }
        }

        static bool IsSectionHeader(string line) {
            string upper = line.ToUpperInvariant();
            return upper == "DEVICE" || upper == "BIOS & MOTHERBOARD" || upper == "WINDOWS" ||
                   upper == "PROCESSOR" || upper == "MEMORY MODULES" || upper == "MEMORY" ||
                   upper == "GRAPHICS" || upper == "DISPLAYS" || upper == "GRAPHICS & DISPLAYS" ||
                   upper == "STORAGE DEVICES" || upper == "STORAGE SPACE" || upper == "WINDOWS, BIOS & STORAGE" ||
                   upper == "BATTERY" || upper == "BATTERY CAPACITY";
        }

        static string MapCategory(string header) {
            if (header.Contains("BATTERY")) return "Battery";
            if (header.Contains("DEVICE") && !header.Contains("STORAGE")) return "System";
            if (header.Contains("BIOS") || header.Contains("MOTHERBOARD") || header.Contains("WINDOWS")) return "System";
            if (header.Contains("PROCESSOR") || header.Contains("MEMORY")) return "CPU & RAM";
            if (header.Contains("GRAPHICS") || header.Contains("DISPLAYS")) return "Graphics";
            if (header.Contains("STORAGE")) return "Storage";
            return "System";
        }

        List<SpecItem> GetFilteredItems() {
            if (selectedCategory == 0) {
                return items.Where(it => it.Category == "Battery").ToList();
            }
            if (selectedCategory == 1) {
                return items.Where(it => it.Category == "Battery")
                            .Concat(items.Where(it => it.Category != "Battery"))
                            .ToList();
            }
            string filter = Categories[selectedCategory];
            return items.Where(it => it.Category == filter).ToList();
        }

        RectangleF[] LayoutChips(bool showPager, out int divY, out RectangleF prevRect, out RectangleF nextRect) {
            float s = DeviceDpi / 96f;
            int chipY = (int)(6 * s);
            int chipH = (int)(26 * s);
            int startX = (int)(4 * s);
            int maxRight = Math.Max((int)(120 * s), Width - (int)(4 * s));
            var rects = new RectangleF[Categories.Length];
            using (var chipFont = new Font(Font.FontFamily, 9f, FontStyle.Bold)) {
                for (int i = 0; i < Categories.Length; i++) {
                    int textW = TextRenderer.MeasureText(Categories[i], chipFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
                    int w = textW + (int)(22 * s);
                    if (startX > (int)(4 * s) && startX + w > maxRight) {
                        startX = (int)(4 * s);
                        chipY += chipH + (int)(6 * s);
                    }
                    rects[i] = new RectangleF(startX, chipY, w, chipH);
                    startX += w + (int)(6 * s);
                }
            }
            prevRect = RectangleF.Empty;
            nextRect = RectangleF.Empty;
            if (showPager) {
                float btnW = 28 * s;
                float labelW = 36 * s;
                float pagerTotal = btnW * 2 + labelW + 8 * s;
                if (startX + pagerTotal > maxRight) {
                    startX = (int)(4 * s);
                    chipY += chipH + (int)(6 * s);
                }
                float rightEdge = maxRight;
                nextRect = new RectangleF(rightEdge - btnW, chipY, btnW, chipH);
                prevRect = new RectangleF(nextRect.Left - labelW - btnW - 4 * s, chipY, btnW, chipH);
            }
            divY = chipY + chipH + (int)(6 * s);
            return rects;
        }

        void MeasureItemHeights(SpecItem item, int tileW, float s, out int titleH, out int subH, out int barOffset, out int tileH, bool isBatteryHealth) {
            int textW = Math.Max(40, tileW - (int)(24 * s));
            using (var tf = new Font("Segoe UI", 10f, FontStyle.Bold)) {
                int measured = TextRenderer.MeasureText(item.Title ?? "", tf, new Size(textW, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
                titleH = Math.Max((int)(20 * s), measured + (int)(2 * s));
            }
            subH = 0;
            if (!string.IsNullOrEmpty(item.Subtitle)) {
                using (var sf = new Font("Segoe UI", 8.5f)) {
                    int measured = TextRenderer.MeasureText(item.Subtitle, sf, new Size(textW, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
                    subH = Math.Max((int)(16 * s), measured + (int)(2 * s));
                }
            }
            barOffset = (int)(7 * s) + titleH + (subH > 0 ? subH + (int)(2 * s) : 0);
            bool stackRefresh = isBatteryHealth && tileW < (int)(360 * s);
            int batteryExtra = isBatteryHealth ? (stackRefresh ? (int)(56 * s) : (int)(36 * s)) : 0;
            tileH = barOffset + (item.BarPercent.HasValue ? (int)(15 * s) : 0) + batteryExtra + (int)(7 * s);
        }

        List<SpecRow> BuildRows(float s, out int colW, out int colGap, out bool twoCol) {
            var visible = GetFilteredItems();
            int itemW = Math.Max((int)(120 * s), Width - (int)(8 * s));
            twoCol = (Width >= (int)(480 * s)) && (visible.Count >= 4 || selectedCategory == 1);
            colGap = twoCol ? (int)(8 * s) : 0;
            colW = twoCol ? Math.Max((int)(140 * s), (itemW - colGap) / 2) : itemW;

            var rows = new List<SpecRow>();
            if (visible.Count == 0) return rows;

            var lastBat = visible.LastOrDefault(v => v.Category == "Battery");
            int idx = 0;
            while (idx < visible.Count) {
                string sec = visible[idx].Section;
                var secItems = new List<SpecItem>();
                while (idx < visible.Count && visible[idx].Section == sec) {
                    secItems.Add(visible[idx]);
                    idx++;
                }

                if (!twoCol) {
                    for (int j = 0; j < secItems.Count; j++) {
                        var item = secItems[j];
                        bool isBat = item.Section.Contains("BATTERY HEALTH") || (selectedCategory == 0 && item == lastBat);
                        int tH, sH, bOff, tileH;
                        MeasureItemHeights(item, colW, s, out tH, out sH, out bOff, out tileH, isBat);
                        rows.Add(new SpecRow {
                            SectionHeader = j == 0 ? sec : null,
                            Left = item,
                            LeftIsBatteryHealth = isBat,
                            Height = tileH
                        });
                    }
                } else {
                    for (int j = 0; j < secItems.Count; j += 2) {
                        var left = secItems[j];
                        var right = (j + 1 < secItems.Count) ? secItems[j + 1] : null;
                        bool lBat = left.Section.Contains("BATTERY HEALTH") || (selectedCategory == 0 && left == lastBat);
                        bool rBat = right != null && (right.Section.Contains("BATTERY HEALTH") || (selectedCategory == 0 && right == lastBat));
                        int tH, sH, bOff, lH, rH = 0;
                        MeasureItemHeights(left, right == null ? itemW : colW, s, out tH, out sH, out bOff, out lH, lBat);
                        if (right != null) {
                            MeasureItemHeights(right, colW, s, out tH, out sH, out bOff, out rH, rBat);
                        }
                        rows.Add(new SpecRow {
                            SectionHeader = j == 0 ? sec : null,
                            Left = left,
                            Right = right,
                            LeftIsBatteryHealth = lBat,
                            RightIsBatteryHealth = rBat,
                            Height = Math.Max(lH, rH)
                        });
                    }
                }
            }
            return rows;
        }

        List<List<SpecRow>> PaginateRows(List<SpecRow> rows, float s, out int divY, out bool needsPager) {
            int sectionHeaderH = (int)(22 * s);
            int rowGap = (int)(5 * s);
            int footerH = (!string.IsNullOrEmpty(footerTimestamp) && selectedCategory != 0) ? (int)(20 * s) : (int)(8 * s);
            RectangleF pTmp, nTmp;
            LayoutChips(false, out divY, out pTmp, out nTmp);

            if (MaxHeight <= 0 || rows.Count == 0) {
                needsPager = false;
                return new List<List<SpecRow>> { rows };
            }

            // First check if all rows fit on 1 page without pager.
            int totalH = divY + (int)(8 * s) + footerH;
            foreach (var r in rows) {
                if (r.SectionHeader != null) totalH += sectionHeaderH;
                totalH += r.Height + rowGap;
            }
            if (totalH <= MaxHeight) {
                needsPager = false;
                return new List<List<SpecRow>> { rows };
            }

            // Paginate with pager bar space reserved.
            needsPager = true;
            LayoutChips(true, out divY, out pTmp, out nTmp);
            int maxBodyH = Math.Max((int)(90 * s), MaxHeight - divY - (int)(8 * s) - footerH);
            var pages = new List<List<SpecRow>>();
            var cur = new List<SpecRow>();
            int curH = 0;
            foreach (var r in rows) {
                int rh = (r.SectionHeader != null ? sectionHeaderH : 0) + r.Height + rowGap;
                if (cur.Count > 0 && curH + rh > maxBodyH) {
                    pages.Add(cur);
                    cur = new List<SpecRow>();
                    curH = 0;
                }
                cur.Add(r);
                curH += rh;
            }
            if (cur.Count > 0) pages.Add(cur);
            return pages;
        }

        public int CalculateRequiredHeight() {
            float s = DeviceDpi / 96f;
            int colW, colGap;
            bool twoCol;
            var rows = BuildRows(s, out colW, out colGap, out twoCol);
            int divY;
            bool needsPager;
            var pages = PaginateRows(rows, s, out divY, out needsPager);
            totalPages = Math.Max(1, pages.Count);
            if (currentPage >= totalPages) currentPage = totalPages - 1;

            int sectionHeaderH = (int)(22 * s);
            int rowGap = (int)(5 * s);
            int footerH = (!string.IsNullOrEmpty(footerTimestamp) && selectedCategory != 0) ? (int)(20 * s) : (int)(8 * s);
            int maxPageH = 0;
            foreach (var pg in pages) {
                int h = divY + (int)(8 * s);
                foreach (var r in pg) {
                    if (r.SectionHeader != null) h += sectionHeaderH;
                    h += r.Height + rowGap;
                }
                h += footerH;
                if (h > maxPageH) maxPageH = h;
            }
            int result = Math.Max((int)(190 * s), maxPageH);
            if (MaxHeight > 0) result = Math.Min(MaxHeight, result);
            return result;
        }

        internal void RecalculateLayout() {
            int req = CalculateRequiredHeight();
            if (Height != req) {
                Height = req;
                if (OnHeightChanged != null) OnHeightChanged();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            int divY;
            RectangleF pRect, nRect;
            var chips = LayoutChips(totalPages > 1, out divY, out pRect, out nRect);
            int newHover = -1;
            for (int i = 0; i < chips.Length; i++) {
                if (chips[i].Contains(e.Location)) {
                    newHover = i;
                    break;
                }
            }

            int newPagerHover = 0;
            if (totalPages > 1) {
                if (!pRect.IsEmpty && pRect.Contains(e.Location) && currentPage > 0) newPagerHover = -1;
                else if (!nRect.IsEmpty && nRect.Contains(e.Location) && currentPage < totalPages - 1) newPagerHover = 1;
            }

            bool overRefresh = (!refreshBtnBounds.IsEmpty && refreshBtnBounds.Contains(e.Location));
            if (overRefresh != hoveredRefreshBtn || newPagerHover != hoveredPager) {
                hoveredRefreshBtn = overRefresh;
                hoveredPager = newPagerHover;
                Invalidate();
            }

            Cursor = (newHover != -1 || overRefresh || newPagerHover != 0) ? Cursors.Hand : Cursors.Default;

            if (newHover != hoveredCategory) {
                hoveredCategory = newHover;
                Invalidate(new Rectangle(0, 0, Width, divY + 2));
            }
        }

        protected override void OnMouseLeave(EventArgs e) {
            base.OnMouseLeave(e);
            if (hoveredCategory != -1 || hoveredRefreshBtn || hoveredPager != 0) {
                hoveredCategory = -1;
                hoveredRefreshBtn = false;
                hoveredPager = 0;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);

            if (!refreshBtnBounds.IsEmpty && refreshBtnBounds.Contains(e.Location)) {
                if (!isRefreshingBattery && OnRefreshBattery != null) {
                    isRefreshingBattery = true;
                    Invalidate();
                    OnRefreshBattery();
                }
                return;
            }

            if (totalPages > 1) {
                if (!prevPageBounds.IsEmpty && prevPageBounds.Contains(e.Location) && currentPage > 0) {
                    currentPage--;
                    Invalidate();
                    return;
                }
                if (!nextPageBounds.IsEmpty && nextPageBounds.Contains(e.Location) && currentPage < totalPages - 1) {
                    currentPage++;
                    Invalidate();
                    return;
                }
            }

            int divY;
            RectangleF pRect, nRect;
            var chips = LayoutChips(totalPages > 1, out divY, out pRect, out nRect);
            for (int i = 0; i < chips.Length; i++) {
                if (chips[i].Contains(e.Location)) {
                    SelectCategory(i);
                    break;
                }
            }
        }

        void DrawTile(Graphics g, SpecItem item, RectangleF tileRect, bool isBatteryHealth, float s) {
            Color tileBg = Theme.Tone(Color.FromArgb(252, 250, 246), Color.FromArgb(20, 25, 34));
            Color tileBorder = Theme.Tone(Color.FromArgb(230, 222, 212), Color.FromArgb(40, 48, 64));

            using (var tp = Theme.Round(tileRect, 10 * s)) {
                using (var tb = new SolidBrush(tileBg)) g.FillPath(tb, tp);
                using (var tpen = new Pen(tileBorder, 1f * s)) g.DrawPath(tpen, tp);
            }

            int tileW = (int)tileRect.Width;
            int textW = Math.Max(40, tileW - (int)(24 * s));
            int titleH, subH, barOffset, tileH;
            MeasureItemHeights(item, tileW, s, out titleH, out subH, out barOffset, out tileH, isBatteryHealth);

            using (var tf = new Font("Segoe UI", 10f, FontStyle.Bold)) {
                var titleRect = new Rectangle((int)(tileRect.X + 12 * s), (int)(tileRect.Y + 7 * s), textW, titleH);
                TextRenderer.DrawText(g, item.Title ?? "", tf, titleRect, Theme.Ink, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
            }

            if (subH > 0) {
                using (var sf = new Font("Segoe UI", 8.5f)) {
                    var subRect = new Rectangle((int)(tileRect.X + 12 * s), (int)(tileRect.Y + 7 * s) + titleH + (int)(2 * s), textW, subH);
                    TextRenderer.DrawText(g, item.Subtitle, sf, subRect, Theme.Muted, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
                }
            }

            float barBottom = tileRect.Y + barOffset + 2 * s;
            if (item.BarPercent.HasValue) {
                float pct = Math.Max(0, Math.Min(100, item.BarPercent.Value));
                float barY = tileRect.Y + barOffset + 2 * s;
                float barW = tileRect.Width - 24 * s;
                float barH = 7 * s;
                var trackRect = new RectangleF(tileRect.X + 12 * s, barY, barW, barH);
                var fillRect = new RectangleF(tileRect.X + 12 * s, barY, Math.Max(barH, barW * pct / 100f), barH);

                using (var trackP = Theme.Round(trackRect, 4f * s)) {
                    using (var trackB = new SolidBrush(Theme.Tone(Color.FromArgb(225, 218, 208), Color.FromArgb(36, 44, 58)))) g.FillPath(trackB, trackP);
                }

                Color barColor = item.Section.Contains("BATTERY") ? (pct >= 80 ? Color.FromArgb(46, 204, 113) : pct >= 50 ? Theme.OrangeEnd : Color.FromArgb(231, 76, 60)) : Theme.OrangeEnd;
                using (var fillP = Theme.Round(fillRect, 4f * s)) {
                    using (var fillB = new SolidBrush(barColor)) g.FillPath(fillB, fillP);
                }
                barBottom = barY + barH;
            }

            if (isBatteryHealth) {
                bool stackRefresh = tileW < (int)(360 * s);
                float btnY = barBottom + 6 * s;
                float btnW = Math.Min(146 * s, tileRect.Width - 24 * s);
                float btnH = 26 * s;
                var btnRect = new RectangleF(tileRect.X + 12 * s, btnY, btnW, btnH);
                refreshBtnBounds = btnRect;

                bool rActive = isRefreshingBattery;
                bool rHover = hoveredRefreshBtn;
                Color btnBg = (rActive || rHover) ? Theme.OrangeEnd : Theme.Tone(Color.FromArgb(240, 234, 226), Color.FromArgb(32, 40, 54));
                Color btnText = (rActive || rHover) ? Color.White : Theme.Ink;
                Color btnBorder = (rActive || rHover) ? Theme.OrangeEnd : Theme.Tone(Color.FromArgb(215, 205, 195), Color.FromArgb(50, 60, 78));

                using (var bp = Theme.Round(btnRect, 6 * s)) {
                    using (var b = new SolidBrush(btnBg)) g.FillPath(b, bp);
                    using (var p = new Pen(btnBorder, 1f * s)) g.DrawPath(p, bp);
                }

                string btnLabel = isRefreshingBattery ? "⟳ Refreshing…" : "⟳ Refresh Battery";
                using (var bf = new Font("Segoe UI", 8.5f, FontStyle.Bold)) {
                    TextRenderer.DrawText(g, btnLabel, bf, Rectangle.Round(btnRect), btnText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }

                string timeText = string.IsNullOrEmpty(lastMeasuredTime) ? DateTime.Now.ToString("dd MMM yyyy · HH:mm:ss") : lastMeasuredTime;
                var timeRect = stackRefresh
                    ? new Rectangle((int)(tileRect.X + 12 * s), (int)(btnRect.Bottom + 3 * s), Math.Max(40, (int)(tileRect.Width - 24 * s)), (int)(16 * s))
                    : new Rectangle((int)(btnRect.Right + 10 * s), (int)btnY, Math.Max(40, (int)(tileRect.Right - btnRect.Right - 18 * s)), (int)btnH);
                using (var tf = new Font("Segoe UI", 8.5f)) {
                    TextRenderer.DrawText(g, "Measured: " + timeText, tf, timeRect, Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics;
            Theme.PaintUnder(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96f;
            refreshBtnBounds = RectangleF.Empty;

            int colW, colGap;
            bool twoCol;
            var allRows = BuildRows(s, out colW, out colGap, out twoCol);
            int divY;
            bool needsPager;
            var pages = PaginateRows(allRows, s, out divY, out needsPager);
            totalPages = Math.Max(1, pages.Count);
            if (currentPage >= totalPages) currentPage = totalPages - 1;

            RectangleF pRect, nRect;
            var chips = LayoutChips(needsPager, out divY, out pRect, out nRect);
            prevPageBounds = pRect;
            nextPageBounds = nRect;

            for (int i = 0; i < Categories.Length; i++) {
                var chipRect = chips[i];
                bool active = (i == selectedCategory);
                bool hover = (i == hoveredCategory);

                Color chipBg = active ? Theme.OrangeEnd : hover ? Theme.Tone(Color.FromArgb(235, 230, 222), Color.FromArgb(38, 46, 62)) : Theme.Tone(Color.FromArgb(246, 242, 236), Color.FromArgb(24, 29, 39));
                Color chipText = active ? Color.White : hover ? Theme.OrangeInk : Theme.Ink;
                Color chipBorder = active ? Theme.OrangeEnd : Theme.Tone(Color.FromArgb(220, 210, 200), Color.FromArgb(50, 60, 80));

                using (var path = Theme.Round(chipRect, 13 * s)) {
                    using (var b = new SolidBrush(chipBg)) g.FillPath(b, path);
                    using (var p = new Pen(chipBorder, 1f * s)) g.DrawPath(p, path);
                }

                using (var f = new Font(Font.FontFamily, 9f, active ? FontStyle.Bold : FontStyle.Regular)) {
                    TextRenderer.DrawText(g, Categories[i], f, Rectangle.Round(chipRect), chipText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
            }

            if (needsPager) {
                for (int d = -1; d <= 1; d += 2) {
                    var pr = d < 0 ? pRect : nRect;
                    bool enabled = d < 0 ? currentPage > 0 : currentPage < totalPages - 1;
                    bool hover = hoveredPager == d;
                    Color bg = hover ? Theme.Tone(Color.FromArgb(235, 230, 222), Color.FromArgb(38, 46, 62)) : Theme.Tone(Color.FromArgb(246, 242, 236), Color.FromArgb(24, 29, 39));
                    Color fg = !enabled ? Theme.Muted : (hover ? Theme.OrangeInk : Theme.Ink);
                    Color bd = Theme.Tone(Color.FromArgb(220, 210, 200), Color.FromArgb(50, 60, 80));
                    using (var path = Theme.Round(pr, 10 * s)) {
                        using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                        using (var p = new Pen(bd, 1f * s)) g.DrawPath(p, path);
                    }
                    using (var f = new Font(Font.FontFamily, 9f, FontStyle.Bold)) {
                        TextRenderer.DrawText(g, d < 0 ? "<" : ">", f, Rectangle.Round(pr), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    }
                }
                var labelRect = new Rectangle((int)pRect.Right, (int)pRect.Top, Math.Max(20, (int)(nRect.Left - pRect.Right)), (int)pRect.Height);
                using (var f = new Font(Font.FontFamily, 8.5f, FontStyle.Bold)) {
                    TextRenderer.DrawText(g, (currentPage + 1) + "/" + totalPages, f, labelRect, Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
            }

            using (var p = new Pen(Theme.Tone(Color.FromArgb(228, 218, 208), Color.FromArgb(42, 50, 66)), 1f * s)) {
                g.DrawLine(p, 4 * s, divY, Width - 4 * s, divY);
            }

            var pageRows = pages.Count > 0 ? pages[currentPage] : new List<SpecRow>();
            int curY = divY + (int)(8 * s);
            int itemW = Math.Max((int)(120 * s), Width - (int)(8 * s));

            foreach (var row in pageRows) {
                if (row.SectionHeader != null) {
                    using (var sf = new Font(Font.FontFamily, 7.5f, FontStyle.Bold)) {
                        var badgeSize = TextRenderer.MeasureText(g, row.SectionHeader, sf, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                        var badgeRect = new RectangleF(6 * s, curY, Math.Min(itemW - 12 * s, badgeSize.Width + 12 * s), 16 * s);
                        using (var bp = Theme.Round(badgeRect, 5 * s)) {
                            using (var bb = new SolidBrush(Theme.Tone(Color.FromArgb(255, 235, 215), Color.FromArgb(42, 34, 28)))) g.FillPath(bb, bp);
                        }
                        TextRenderer.DrawText(g, row.SectionHeader, sf, Rectangle.Round(badgeRect), Theme.OrangeInk, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

                        if (badgeRect.Right + 10 * s < Width - 6 * s) {
                            using (var lp = new Pen(Theme.Tone(Color.FromArgb(235, 225, 215), Color.FromArgb(36, 44, 58)), 1f * s)) {
                                g.DrawLine(lp, badgeRect.Right + 8 * s, curY + 8 * s, Width - 6 * s, curY + 8 * s);
                            }
                        }
                    }
                    curY += (int)(22 * s);
                }

                if (row.Right == null) {
                    int w = twoCol ? itemW : colW;
                    DrawTile(g, row.Left, new RectangleF(4 * s, curY, w, row.Height), row.LeftIsBatteryHealth, s);
                } else {
                    DrawTile(g, row.Left, new RectangleF(4 * s, curY, colW, row.Height), row.LeftIsBatteryHealth, s);
                    DrawTile(g, row.Right, new RectangleF(4 * s + colW + colGap, curY, colW, row.Height), row.RightIsBatteryHealth, s);
                }

                curY += row.Height + (int)(5 * s);
            }

            if (!string.IsNullOrEmpty(footerTimestamp) && selectedCategory != 0) {
                using (var ff = new Font("Segoe UI", 8f, FontStyle.Italic)) {
                    var footRect = new Rectangle((int)(6 * s), curY + (int)(2 * s), Math.Max(40, (int)(Width - 12 * s)), (int)(16 * s));
                    TextRenderer.DrawText(g, footerTimestamp, ff, footRect, Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }
    }
}
