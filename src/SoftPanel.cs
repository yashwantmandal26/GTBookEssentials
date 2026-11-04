using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GTBook
{
	public class SoftPanel : Panel
	{
		public SoftPanel()
		{
			DoubleBuffered = true;
			BackColor = Theme.White;
			base.Padding = new Padding(22);
			SetStyle(ControlStyles.ResizeRedraw, true);
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
            Theme.PaintUnder(this,e.Graphics);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
            Theme.PaintUnder(this,e.Graphics);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			float num = (float)base.DeviceDpi / 96f;
			RectangleF r = new RectangleF(num, num, (float)base.Width - 3f * num, (float)base.Height - 5f * num);
			using (GraphicsPath path = Theme.Round(new RectangleF(r.X, r.Y + 3f * num, r.Width, r.Height), 17f * num))
			{
				using (SolidBrush brush = new SolidBrush(Color.FromArgb(244,222,203)))
				{
					e.Graphics.FillPath(brush, path);
				}
			}
			using (GraphicsPath path2 = Theme.Round(r, 17f * num))
			{
				using (SolidBrush brush2 = new SolidBrush(Theme.White))
				{
					e.Graphics.FillPath(brush2, path2);
				}
                using(var gradient=Theme.OrangeBorder(r))
				using (Pen pen = new Pen(gradient, 1.1f*num))
				{
					e.Graphics.DrawPath(pen, path2);
				}
			}
			base.OnPaint(e);
		}
	}
}
