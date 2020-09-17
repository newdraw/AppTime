
using AppTime.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.ModelBinding;
using System.Web.UI.WebControls;
using System.Windows.Forms;

namespace AppTime
{
    public partial class FrmMain : Form
    {
        public FrmMain()
        {
            InitializeComponent();

        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(Settings.Default.DataPath))
            {
                txtDataPath.Text = Application.StartupPath;
            }
            else
            {
                txtDataPath.Text = Settings.Default.DataPath;
            }
            cboRecordScreen.DataSource = new[] {
                new {Text="最近30天", Value=30},
                new {Text="最近15天", Value=15},
                new {Text="无限制", Value=int.MaxValue},
                new {Text="不留存", Value=0},
            };
            cboRecordScreen.DisplayMember = "Text";
            cboRecordScreen.ValueMember = "Value";
            cboRecordScreen.SelectedValue = Settings.Default.RecordScreenDays;
            numScreenBufferMB.Value = Settings.Default.ScreenBufferMB;
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            Process.Start($@"http://localhost:{Program.Port}/");
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            btnOpen_Click(null, null);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        bool cancelClose = true;
        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Hide();
            cancelClose = false;
            Program.recorder.FlushScreenBuffer();
            this.Close();
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            e.Cancel = cancelClose;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(txtDataPath.Text);
                Settings.Default.DataPath = txtDataPath.Text == Application.StartupPath ? "" : txtDataPath.Text;
                
                Program.recorder.BuildDataPath();
            }
            catch
            {
                MessageBox.Show("数据存储位置无效，请重新选择。");
            }
            Settings.Default.ScreenBufferMB = (int)numScreenBufferMB.Value;
            Settings.Default.RecordScreenDays = (int)cboRecordScreen.SelectedValue;
            Settings.Default.Save();
            this.Hide();
        }

        bool firstLoad = true;
        private void FrmMain_Shown(object sender, EventArgs e)
        {
            if(firstLoad)
            {
                this.Hide();
                firstLoad = false;
            }
        }

        private void btnSetting_Click(object sender, EventArgs e)
        {
            this.Show();
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show($"AppTime桌面时间管理\r\n\r\n联系作者：newdraw@hotmail.com", "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnDataPath_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            if(dlg.ShowDialog()== DialogResult.OK)
            {
                txtDataPath.Text = dlg.SelectedPath;
            }
        }
    }
}
