using System.Windows.Forms;

namespace GTBook {
    public class WheelSafeTrackBar : TrackBar {
        protected override void OnMouseWheel(MouseEventArgs e){var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;}
        protected override void WndProc(ref Message m){if(m.Msg==0x020A||m.Msg==0x020E){m.Result=System.IntPtr.Zero;return;}base.WndProc(ref m);}
    }
    public class WheelSafeComboBox : ComboBox {
        protected override void OnMouseWheel(MouseEventArgs e){if(!DroppedDown){var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;return;}base.OnMouseWheel(e);}
        protected override void WndProc(ref Message m){if((m.Msg==0x020A||m.Msg==0x020E)&&!DroppedDown){m.Result=System.IntPtr.Zero;return;}base.WndProc(ref m);}
    }
}
