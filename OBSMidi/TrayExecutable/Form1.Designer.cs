namespace TrayExecutable
{
    partial class TrayForm
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TrayForm));
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripIP = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripPassword = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripConnect = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripClose = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.ContextMenuStrip = this.contextMenuStrip1;
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "nanoKontrol2OBS";
            this.notifyIcon1.Visible = true;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripIP,
            this.toolStripPassword,
            this.toolStripConnect,
            this.toolStripClose});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(181, 120);
            // 
            // toolStripIP
            // 
            this.toolStripIP.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.toolStripIP.Name = "toolStripIP";
            this.toolStripIP.Size = new System.Drawing.Size(100, 23);
            this.toolStripIP.Text = "127.0.0.1:4444";
            // 
            // toolStripPassword
            // 
            this.toolStripPassword.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.toolStripPassword.Name = "toolStripPassword";
            this.toolStripPassword.Size = new System.Drawing.Size(100, 23);
            this.toolStripPassword.Text = "1234";
            // 
            // toolStripConnect
            // 
            this.toolStripConnect.Name = "toolStripConnect";
            this.toolStripConnect.Size = new System.Drawing.Size(180, 22);
            this.toolStripConnect.Text = "Connect";
            this.toolStripConnect.Click += new System.EventHandler(this.toolStripConnect_Click);
            // 
            // toolStripClose
            // 
            this.toolStripClose.Enabled = false;
            this.toolStripClose.Name = "toolStripClose";
            this.toolStripClose.Size = new System.Drawing.Size(180, 22);
            this.toolStripClose.Text = "Close";
            this.toolStripClose.Click += new System.EventHandler(this.toolStripClose_Click);
            // 
            // TrayForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(226, 111);
            this.Name = "TrayForm";
            this.Text = "Form1";
            this.Resize += new System.EventHandler(this.TrayForm_Resize);
            this.contextMenuStrip1.ResumeLayout(false);
            this.contextMenuStrip1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripTextBox toolStripIP;
        private System.Windows.Forms.ToolStripTextBox toolStripPassword;
        private System.Windows.Forms.ToolStripMenuItem toolStripConnect;
        private System.Windows.Forms.ToolStripMenuItem toolStripClose;
    }
}

