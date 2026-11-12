using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GTBook {
    public sealed class MetricTile : Control {
        string title = "";
        string valueText = "";
        string detailText = "";
        Color dotColor = Color.Empty;
        Color barStart = Theme.OrangeStart;
        Color barEnd = Theme.OrangeEnd;
        int progress = -1;
        bool compact;

        public string Title {
            get { return title; }
            set { if (title != (value ?? "")) { title = value ?? ""; Invalidate(); } }
        }
        public string ValueText {
            get { return valueText; }
            set { if (valueText != (value ?? "")) { valueText = value ?? ""; Invalidate(); } }
        }
        public string DetailText {
            get { return detailText; }
            set { if (detailText != (value ?? "")) { detailText = value ?? ""; Invalidate(); } }
        }
        public Color DotColor {
            get { return dotColor; }
            set { if (dotColor != value) { dotColor = value; Invalidate(); } }
        }
        public Color BarStart {
            get { return barStart; }
            set { if (barStart != value) { barStart = value; Invalidate(); } }
        }
        public Color BarEnd {
            get { return barEnd; }
            set { if (barEnd != value) { barEnd = value; Invalidate(); } }
        }
        public int Progress {
            get { return progress; }
            set { int clamped = value < 0 ? -1 : Math.Min(100, value); if (progress != clamped) { progress = clamped; Invalidate(); } }
        }
        public bool Compact {
            get { return compact; }
            set { if (compact != value) { compact = value; Invalidate(); } }
        }

        public MetricTile() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            TabStop = false;
        }

        protected override void OnPaintBackground(PaintEventArgs e) {
        }

        static void DrawFittedText(Graphics g, string text, FontFamily family, float baseSize, float minSize, FontStyle style, Rectangle rect, Color color, TextFormatFlags flags) {
            if (string.IsNullOrEmpty(text) || rect.Width <= 4 || rect.Height <= 4) return;
            float size = baseSize;
            Font font = new Font(family, size, style);
            try {
                while (size > minSize) {
                    var measured = TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    if (measured.Width <= rect.Width && measured.Height <= rect.Height + 2) break;
                    size = Math.Max(minSize, size - 0.4f);
                    font.Dispose();
                    font = new Font(family, size, style);
                }
                TextRenderer.DrawText(g, text, font, rect, color, flags | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            } finally {
                font.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics;
            Theme.PaintUnder(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96f;
            var rect = new RectangleF(1f * s, 1f * s, Math.Max(10f, Width - 3f * s), Math.Max(10f, Height - 3f * s));
            Color surface = Theme.Tone(Color.FromArgb(255, 248, 242), Color.FromArgb(20, 25, 36));
            Color border = Theme.Tone(Color.FromArgb(245, 218, 195), Color.FromArgb(49, 59, 79));

            using (var path = Theme.Round(rect, 11f * s)) {
                using (var fill = new SolidBrush(surface)) g.FillPath(fill, path);
                using (var pen = new Pen(border, 1f * s)) g.DrawPath(pen, path);
            }

            int padX = (int)Math.Round(10f * s);
            int padTop = (int)Math.Round(6f * s);
            int innerW = Math.Max(20, Width - padX * 2);

            int titleX = padX;
            int titleH = (int)Math.Round(14f * s);
            if (!dotColor.IsEmpty) {
                float d = 6.5f * s;
                float dy = padTop + (titleH - d) / 2f;
                using (var dot = new SolidBrush(dotColor)) g.FillEllipse(dot, padX, dy, d, d);
                titleX += (int)Math.Round(11f * s);
            }

            var titleRect = new Rectangle(titleX, padTop, Math.Max(10, Width - titleX - padX), titleH);
            DrawFittedText(g, title, Font.FontFamily, compact ? 7.5f : 7.8f, 6.2f, FontStyle.Bold, titleRect, Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            int valY = padTop + titleH;
            int valH = (int)Math.Round((compact ? 21f : 23f) * s);
            var valRect = new Rectangle(padX, valY, innerW, valH);
            DrawFittedText(g, valueText, Font.FontFamily, compact ? 12.2f : 13.5f, 9.0f, FontStyle.Bold, valRect, Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            int detailY = valY + valH;
            int bottomLimit = progress >= 0 ? (int)Math.Floor(Height - 10f * s) : (int)Math.Floor(Height - 4f * s);
            int detailH = Math.Max((int)Math.Round(13f * s), Math.Min((int)Math.Round(18f * s), bottomLimit - detailY));
            if (!string.IsNullOrEmpty(detailText)) {
                var detailRect = new Rectangle(padX, detailY, innerW, detailH);
                DrawFittedText(g, detailText, Font.FontFamily, 8.2f, 5.5f, FontStyle.Regular, detailRect, Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }

            if (progress >= 0) {
                float barH = 4f * s;
                float barY = Height - (8f * s);
                float barW = Math.Max(16f, innerW);
                var trackRect = new RectangleF(padX, barY, barW, barH);
                Color trackColor = Theme.Tone(Color.FromArgb(240, 224, 210), Color.FromArgb(37, 45, 62));
                using (var trackPath = Theme.Round(trackRect, barH / 2f))
                using (var trackBrush = new SolidBrush(trackColor)) {
                    g.FillPath(trackBrush, trackPath);
                }
                if (progress > 0) {
                    float fillW = Math.Max(barH, barW * (progress / 100f));
                    var fillRect = new RectangleF(padX, barY, fillW, barH);
                    using (var fillPath = Theme.Round(fillRect, barH / 2f))
                    using (var fillBrush = new LinearGradientBrush(fillRect, barStart, barEnd, LinearGradientMode.Horizontal)) {
                        g.FillPath(fillBrush, fillPath);
                    }
                }
            }
        }
    }

    public sealed class StatusPill : Control {
        string pillText = "";
        Color dotColor = Theme.OrangeEnd;
        bool rightAligned = true;

        public string PillText {
            get { return pillText; }
            set { if (pillText != (value ?? "")) { pillText = value ?? ""; Invalidate(); } }
        }
        public Color DotColor {
            get { return dotColor; }
            set { if (dotColor != value) { dotColor = value; Invalidate(); } }
        }
        public bool RightAligned {
            get { return rightAligned; }
            set { if (rightAligned != value) { rightAligned = value; Invalidate(); } }
        }

        public StatusPill() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            TabStop = false;
        }

        protected override void OnPaintBackground(PaintEventArgs e) {
        }

        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics;
            Theme.PaintUnder(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96f;
            using (var font = new Font("Segoe UI", 8.2f, FontStyle.Bold)) {
                Size measured = TextRenderer.MeasureText(g, pillText, font, new Size(int.MaxValue, Height), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                int padH = (int)Math.Round(10f * s);
                int dotGap = dotColor.IsEmpty ? 0 : (int)Math.Round(12f * s);
                int w = Math.Min(Width - 2, measured.Width + padH * 2 + dotGap);
                int h = Math.Min(Height - 2, (int)Math.Round(24f * s));
                int x = rightAligned ? Math.Max(0, Width - w - 1) : 0;
                int y = Math.Max(0, (Height - h) / 2);
                var pillRect = new RectangleF(x, y, Math.Max(10, w), Math.Max(10, h));
                Color fill = Theme.Tone(Color.FromArgb(255, 245, 236), Color.FromArgb(23, 29, 42));
                Color border = Theme.Tone(Color.FromArgb(245, 214, 188), Color.FromArgb(52, 63, 84));
                using (var path = Theme.Round(pillRect, h / 2f)) {
                    using (var fillBrush = new SolidBrush(fill)) g.FillPath(fillBrush, path);
                    using (var pen = new Pen(border, 1f * s)) g.DrawPath(pen, path);
                }
                int textX = x + padH;
                if (!dotColor.IsEmpty) {
                    float d = 6.5f * s;
                    float dy = y + (h - d) / 2f;
                    using (var dotBrush = new SolidBrush(dotColor)) g.FillEllipse(dotBrush, x + padH, dy, d, d);
                    textX += dotGap;
                }
                var textRect = new Rectangle(textX, y, Math.Max(10, w - (textX - x) - padH + 2), h);
                TextRenderer.DrawText(g, pillText, font, textRect, Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
