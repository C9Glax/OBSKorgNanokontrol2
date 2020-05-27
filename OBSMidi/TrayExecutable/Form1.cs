using System;
using System.Windows.Forms;
using nanoKontrol2OBS;

namespace TrayExecutable
{
    public partial class TrayForm : Form
    {
        private Kontrol2OBS control;
        private string notifyIconText;
        public TrayForm()
        {
            InitializeComponent();
            this.notifyIconText = this.notifyIcon1.Text;
            this.WindowState = FormWindowState.Minimized;
            this.Hide();
        }

        private void TrayForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.Hide();
        }

        private void toolStripConnect_Click(object sender, EventArgs e)
        {
            this.toolStripIP.Enabled = false;
            this.toolStripPassword.Enabled = false;
            this.toolStripConnect.Enabled = false;
            this.toolStripClose.Enabled = true;
            string url = this.toolStripIP.Text;
            string password = this.toolStripPassword.Text;
            this.control = new Kontrol2OBS(url, password);
            this.control.OnLoggingEvent += OnLoggingEvent;
            this.control.Create();
        }

        private void OnLoggingEvent(object sender, Kontrol2OBS.LogEventArgs e)
        {
            this.notifyIcon1.Text = string.Format("{0} - {1}", this.notifyIconText, e.text);
        }

        private void toolStripClose_Click(object sender, EventArgs e)
        {
            this.control.Dispose();
            this.Close();
        }
    }
}
