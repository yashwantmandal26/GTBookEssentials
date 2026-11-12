using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GTBook
{
	public class SoftPanel : Panel
	{
		public SoftPanel()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			DoubleBuffered = true;
			BackColor = Theme.White;
			base.Padding = new Padding(22);
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
		}

		protected override void OnPaint(PaintEventArgs e)
		{
            Theme.PaintUnder(this,e.Graphics);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			float num = (float)base.DeviceDpi / 96f;
			RectangleF r = new RectangleF(num, num, (float)base.Width - 3f * num, (float)base.Height - 5f * num);
			using (GraphicsPath path = Theme.Round(new RectangleF(r.X, r.Y + 3f * num, r.Width, r.Height), 17f * num))
			{
				using (SolidBrush brush = new SolidBrush(Theme.Tone(Color.FromArgb(244,222,203),Color.FromArgb(5,8,14))))
				{
					e.Graphics.FillPath(brush, path);
				}
			}
			using (GraphicsPath path2 = Theme.Round(r, 17f * num))
			{
				using (var brush2 = new SolidBrush(Theme.White))
				{
					e.Graphics.FillPath(brush2, path2);
				}
                using(var gradient=Theme.OrangeBorder(r))
				using (Pen pen = new Pen(gradient, 1.1f*num))
				{
					e.Graphics.DrawPath(pen, path2);
				}
				using(var bevel=new Pen(Color.FromArgb(28,Color.White),num))e.Graphics.DrawLine(bevel,r.Left+20*num,r.Top+2*num,r.Right-20*num,r.Top+2*num);
			}
			base.OnPaint(e);
		}
	}
}
