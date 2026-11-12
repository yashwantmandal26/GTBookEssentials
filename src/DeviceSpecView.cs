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
        readonly List<SpecItem> items = new List<SpecItem>();
        string footerTimestamp = "";
        string lastMeasuredTime = "";

        public Action OnHeightChanged;
        public Action OnRefreshBattery;
        bool isRefreshingBattery = false;
        bool hoveredRefreshBtn = false;
        RectangleF refreshBtnBounds = RectangleF.Empty;

        int lastLayoutWidth = -1;

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
            if (Width > 0 && Width != lastLayoutWidth) {
                lastLayoutWidth = Width;
                RecalculateLayout();
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
                // "Battery" first
                return items.Where(it => it.Category == "Battery").ToList();
            }
            if (selectedCategory == 1) {
                // "All" - Battery first at top, then other categories
                return items.Where(it => it.Category == "Battery")
                            .Concat(items.Where(it => it.Category != "Battery"))
                            .ToList();
            }
            string filter = Categories[selectedCategory];
            return items.Where(it => it.Category == filter).ToList();
        }

        RectangleF[] LayoutChips(out int divY) {
            float s = DeviceDpi / 96f;
            int chipY = (int)(6 * s);
            int chipH = (int)(28 * s);
            int startX = (int)(4 * s);
            int maxRight = Math.Max((int)(120 * s), Width - (int)(4 * s));
            var rects = new RectangleF[Categories.Length];
            using (var chipFont = new Font(Font.FontFamily, 9f, FontStyle.Bold)) {
                for (int i = 0; i < Categories.Length; i++) {
                    int textW = TextRenderer.MeasureText(Categories[i], chipFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
                    int w = textW + (int)(24 * s);
                    if (startX > (int)(4 * s) && startX + w > maxRight) {
                        startX = (int)(4 * s);
                        chipY += chipH + (int)(6 * s);
                    }
                    rects[i] = new RectangleF(startX, chipY, w, chipH);
                    startX += w + (int)(8 * s);
                }
            }
            divY = chipY + chipH + (int)(6 * s);
            return rects;
        }

        void MeasureItemHeights(SpecItem item, int textW, float s, out int titleH, out int subH, out int barOffset, out int tileH, bool isBatteryHealth) {
            using (var tf = new Font("Segoe UI", 10.5f, FontStyle.Bold)) {
                int measured = TextRenderer.MeasureText(item.Title ?? "", tf, new Size(Math.Max(40, textW), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
                titleH = Math.Max((int)(22 * s), measured + (int)(2 * s));
            }
            subH = 0;
            if (!string.IsNullOrEmpty(item.Subtitle)) {
                using (var sf = new Font("Segoe UI", 9f)) {
                    int measured = TextRenderer.MeasureText(item.Subtitle, sf, new Size(Math.Max(40, textW), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
                    subH = Math.Max((int)(18 * s), measured + (int)(2 * s));
                }
            }
            barOffset = (int)(8 * s) + titleH + (subH > 0 ? subH + (int)(2 * s) : 0);
            tileH = barOffset + (item.BarPercent.HasValue ? (int)(18 * s) : 0) + (isBatteryHealth ? (int)(40 * s) : 0) + (int)(8 * s);
        }

        public int CalculateRequiredHeight() {
            float s = DeviceDpi / 96f;
            int divY;
            LayoutChips(out divY);
            var visible = GetFilteredItems();
            int itemW = Math.Max((int)(120 * s), Width - (int)(8 * s));
            int textW = Math.Max(40, itemW - (int)(28 * s));
            int sectionHeaderH = (int)(26 * s);

            string lastSection = "";
            int total = divY + (int)(10 * s);
            foreach (var item in visible) {
                if (item.Section != lastSection) {
                    total += sectionHeaderH;
                    lastSection = item.Section;
                }
                bool isBatteryHealth = item.Section.Contains("BATTERY HEALTH") || (selectedCategory == 0 && item == visible.Last());
                int titleH, subH, barOffset, tileH;
                MeasureItemHeights(item, textW, s, out titleH, out subH, out barOffset, out tileH, isBatteryHealth);
                total += tileH + (int)(6 * s);
            }
            if (!string.IsNullOrEmpty(footerTimestamp) && selectedCategory != 0) total += (int)(24 * s);
            return Math.Max((int)(190 * s), total + (int)(10 * s));
        }

        void RecalculateLayout() {
            int req = CalculateRequiredHeight();
            if (Height != req) {
                Height = req;
                if (OnHeightChanged != null) OnHeightChanged();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            int divY;
            var chips = LayoutChips(out divY);
            int newHover = -1;
            for (int i = 0; i < chips.Length; i++) {
                if (chips[i].Contains(e.Location)) {
                    newHover = i;
                    break;
                }
            }

            bool overRefresh = (!refreshBtnBounds.IsEmpty && refreshBtnBounds.Contains(e.Location));
            if (overRefresh != hoveredRefreshBtn) {
                hoveredRefreshBtn = overRefresh;
                Invalidate();
            }

            Cursor = (newHover != -1 || overRefresh) ? Cursors.Hand : Cursors.Default;

            if (newHover != hoveredCategory) {
                hoveredCategory = newHover;
                Invalidate(new Rectangle(0, 0, Width, divY + 2));
            }
        }

        protected override void OnMouseLeave(EventArgs e) {
            base.OnMouseLeave(e);
            if (hoveredCategory != -1 || hoveredRefreshBtn) {
                hoveredCategory = -1;
                hoveredRefreshBtn = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);

            // Check refresh battery button click
            if (!refreshBtnBounds.IsEmpty && refreshBtnBounds.Contains(e.Location)) {
                if (!isRefreshingBattery && OnRefreshBattery != null) {
                    isRefreshingBattery = true;
                    Invalidate();
                    OnRefreshBattery();
                }
                return;
            }

            // Check category chips
            int divY;
            var chips = LayoutChips(out divY);
            for (int i = 0; i < chips.Length; i++) {
                if (chips[i].Contains(e.Location)) {
                    if (selectedCategory != i) {
                        selectedCategory = i;
                        RecalculateLayout();
                        Invalidate();
                    }
                    break;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics;
            Theme.PaintUnder(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96f;
            refreshBtnBounds = RectangleF.Empty;

            // 1. Draw Category Chips
            int divY;
            var chips = LayoutChips(out divY);
            for (int i = 0; i < Categories.Length; i++) {
                var chipRect = chips[i];
                bool active = (i == selectedCategory);
                bool hover = (i == hoveredCategory);

                Color chipBg = active ? Theme.OrangeEnd : hover ? Theme.Tone(Color.FromArgb(235, 230, 222), Color.FromArgb(38, 46, 62)) : Theme.Tone(Color.FromArgb(246, 242, 236), Color.FromArgb(24, 29, 39));
                Color chipText = active ? Color.White : hover ? Theme.OrangeInk : Theme.Ink;
                Color chipBorder = active ? Theme.OrangeEnd : Theme.Tone(Color.FromArgb(220, 210, 200), Color.FromArgb(50, 60, 80));

                using (var path = Theme.Round(chipRect, 14 * s)) {
                    using (var b = new SolidBrush(chipBg)) g.FillPath(b, path);
                    using (var p = new Pen(chipBorder, 1f * s)) g.DrawPath(p, path);
                }

                using (var f = new Font(Font.FontFamily, 9f, active ? FontStyle.Bold : FontStyle.Regular)) {
                    TextRenderer.DrawText(g, Categories[i], f, Rectangle.Round(chipRect), chipText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
            }

            // Divider below chips
            using (var p = new Pen(Theme.Tone(Color.FromArgb(228, 218, 208), Color.FromArgb(42, 50, 66)), 1f * s)) {
                g.DrawLine(p, 4 * s, divY, Width - 4 * s, divY);
            }

            // 2. Draw Spec Items
            var visible = GetFilteredItems();
            int curY = divY + (int)(10 * s);
            string lastSection = "";
            int itemW = Math.Max((int)(120 * s), Width - (int)(8 * s));
            int textW = Math.Max(40, itemW - (int)(28 * s));

            foreach (var item in visible) {
                // Section Header Badge
                if (item.Section != lastSection) {
                    lastSection = item.Section;
                    using (var sf = new Font(Font.FontFamily, 8f, FontStyle.Bold)) {
                        var badgeSize = TextRenderer.MeasureText(g, item.Section, sf, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                        var badgeRect = new RectangleF(6 * s, curY, Math.Min(itemW - 12 * s, badgeSize.Width + 14 * s), 18 * s);
                        using (var bp = Theme.Round(badgeRect, 5 * s)) {
                            using (var bb = new SolidBrush(Theme.Tone(Color.FromArgb(255, 235, 215), Color.FromArgb(42, 34, 28)))) g.FillPath(bb, bp);
                        }
                        TextRenderer.DrawText(g, item.Section, sf, Rectangle.Round(badgeRect), Theme.OrangeInk, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

                        // Horizontal accent line
                        if (badgeRect.Right + 12 * s < Width - 6 * s) {
                            using (var lp = new Pen(Theme.Tone(Color.FromArgb(235, 225, 215), Color.FromArgb(36, 44, 58)), 1f * s)) {
                                g.DrawLine(lp, badgeRect.Right + 8 * s, curY + 9 * s, Width - 6 * s, curY + 9 * s);
                            }
                        }
                    }
                    curY += (int)(26 * s);
                }

                bool isBatteryHealth = item.Section.Contains("BATTERY HEALTH") || (selectedCategory == 0 && item == visible.Last());
                int titleH, subH, barOffset, tileH;
                MeasureItemHeights(item, textW, s, out titleH, out subH, out barOffset, out tileH, isBatteryHealth);
                var tileRect = new RectangleF(4 * s, curY, itemW, tileH);
                Color tileBg = Theme.Tone(Color.FromArgb(252, 250, 246), Color.FromArgb(20, 25, 34));
                Color tileBorder = Theme.Tone(Color.FromArgb(230, 222, 212), Color.FromArgb(40, 48, 64));

                using (var tp = Theme.Round(tileRect, 10 * s)) {
                    using (var tb = new SolidBrush(tileBg)) g.FillPath(tb, tp);
                    using (var tpen = new Pen(tileBorder, 1f * s)) g.DrawPath(tpen, tp);
                }

                // Title
                using (var tf = new Font("Segoe UI", 10.5f, FontStyle.Bold)) {
                    var titleRect = new Rectangle((int)(tileRect.X + 14 * s), (int)(tileRect.Y + 8 * s), textW, titleH);
                    TextRenderer.DrawText(g, item.Title ?? "", tf, titleRect, Theme.Ink, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
                }

                // Subtitle
                if (subH > 0) {
                    using (var sf = new Font("Segoe UI", 9f)) {
                        var subRect = new Rectangle((int)(tileRect.X + 14 * s), (int)(tileRect.Y + 8 * s) + titleH + (int)(2 * s), textW, subH);
                        TextRenderer.DrawText(g, item.Subtitle, sf, subRect, Theme.Muted, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
                    }
                }

                // Progress Bar (if available)
                float barBottom = tileRect.Y + barOffset + 2 * s;
                if (item.BarPercent.HasValue) {
                    float pct = Math.Max(0, Math.Min(100, item.BarPercent.Value));
                    float barY = tileRect.Y + barOffset + 2 * s;
                    float barW = tileRect.Width - 28 * s;
                    float barH = 8 * s;
                    var trackRect = new RectangleF(tileRect.X + 14 * s, barY, barW, barH);
                    var fillRect = new RectangleF(tileRect.X + 14 * s, barY, Math.Max(barH, barW * pct / 100f), barH);

                    using (var trackP = Theme.Round(trackRect, 4f * s)) {
                        using (var trackB = new SolidBrush(Theme.Tone(Color.FromArgb(225, 218, 208), Color.FromArgb(36, 44, 58)))) g.FillPath(trackB, trackP);
                    }

                    Color barColor = item.Section.Contains("BATTERY") ? (pct >= 80 ? Color.FromArgb(46, 204, 113) : pct >= 50 ? Theme.OrangeEnd : Color.FromArgb(231, 76, 60)) : Theme.OrangeEnd;
                    using (var fillP = Theme.Round(fillRect, 4f * s)) {
                        using (var fillB = new SolidBrush(barColor)) g.FillPath(fillB, fillP);
                    }
                    barBottom = barY + barH;
                }

                // Dedicated Refresh Button and Date & Time Timestamp on Battery Health tile
                if (isBatteryHealth) {
                    float btnY = barBottom + 8 * s;
                    float btnW = 152 * s;
                    float btnH = 28 * s;
                    var btnRect = new RectangleF(tileRect.X + 14 * s, btnY, btnW, btnH);
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
                    using (var bf = new Font("Segoe UI", 9f, FontStyle.Bold)) {
                        TextRenderer.DrawText(g, btnLabel, bf, Rectangle.Round(btnRect), btnText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    }

                    // Date & Time Timestamp
                    string timeText = string.IsNullOrEmpty(lastMeasuredTime) ? DateTime.Now.ToString("dd MMM yyyy · HH:mm:ss") : lastMeasuredTime;
                    var timeRect = new Rectangle((int)(btnRect.Right + 12 * s), (int)btnY, Math.Max(40, (int)(tileRect.Right - btnRect.Right - 20 * s)), (int)btnH);
                    using (var tf = new Font("Segoe UI", 9f)) {
                        TextRenderer.DrawText(g, "📅 Measured: " + timeText, tf, timeRect, Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                    }
                }

                curY += tileH + (int)(6 * s);
            }

            // Timestamp footer (if not already shown on battery tile)
            if (!string.IsNullOrEmpty(footerTimestamp) && selectedCategory != 0) {
                using (var ff = new Font("Segoe UI", 8.5f, FontStyle.Italic)) {
                    var footRect = new Rectangle((int)(6 * s), curY + (int)(4 * s), Math.Max(40, (int)(Width - 12 * s)), (int)(18 * s));
                    TextRenderer.DrawText(g, footerTimestamp, ff, footRect, Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }
    }
}
