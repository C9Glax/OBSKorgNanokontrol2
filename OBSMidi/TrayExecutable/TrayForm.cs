using System;
using System.Windows.Forms;
using nanoKontrol2OBS;

namespace TrayExecutable
{
    public partial class TrayForm : Form
    {
        private Kontrol2OBS control;
        private string password = "";
        public TrayForm()
        {
            InitializeComponent();
        }

        private void toolStripConnect_Click(object sender, EventArgs e)
        {
            this.toolStripIP.Enabled = false;
            this.toolStripPassword.Enabled = false;
            this.toolStripConnect.Enabled = false;
            this.toolStripClose.Enabled = false;
            string url = this.toolStripIP.Text;
            this.control = new Kontrol2OBS(url, this.password);
            this.control.OnStatusLog += OnStatusChange;
            this.control.Create();
            this.toolStripClose.Enabled = true;
        }

        private void OnStatusChange(object sender, Kontrol2OBS.LogEventArgs e)
        {
            this.notifyIcon.BalloonTipText = e.text;
            this.notifyIcon.ShowBalloonTip(2000);
        }

        private void toolStripClose_Click(object sender, EventArgs e)
        {
            this.control.Dispose();
            this.Close();
        }


        private void TrayForm_Shown(object sender, EventArgs e)
        {
            this.toolStripPassword.Text = this.password;
            this.Hide();
        }

        private void toolStripPassword_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\b')
                this.password = this.password.Substring(0, (this.password.Length > 1) ? this.password.Length - 1 : 0);
            else
                this.password += e.KeyChar;
            this.toolStripPassword.Text = new string('•', this.password.Length);
        }

        private void toolStripPassword_TextChanged(object sender, EventArgs e)
        {
            this.toolStripPassword.Text = new string('•', this.password.Length);
        }
    }
}
