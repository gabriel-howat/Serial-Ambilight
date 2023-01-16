using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ambilight
{
    public partial class TrayForm : Form
    {
        FormAmbilight formAmbi;
        bool canEvent = false;
        public TrayForm(FormAmbilight form, bool enabled)
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.Manual;
            this.Left = Cursor.Position.X - (this.Width / 2);
            this.Top = Screen.PrimaryScreen.WorkingArea.Bottom - this.Height - 8;
            trackBar2.Value = form.Brightness;
            trackBar2.Enabled = enabled;
            formAmbi = form;
            this.TopMost = true;
            canEvent = true;
        }

        private void TrayForm_Deactivate(object sender, EventArgs e)
        {
            this.Close();
        }

        protected override void WndProc(ref Message message)
        {
            const int WM_NCHITTEST = 0x0084;

            if (message.Msg == WM_NCHITTEST)
                return;

            base.WndProc(ref message);
        }

        private void TrayForm_Leave(object sender, EventArgs e)
        {
            this.Close();
        }

        private void trackBar2_ValueChanged(object sender, EventArgs e)
        {
            if (canEvent)
                formAmbi.Brightness = trackBar2.Value;
        }

    }
}
