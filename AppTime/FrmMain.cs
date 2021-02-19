
using AppTime.Properties;
using Microsoft.Win32;
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
using System.Security.AccessControl;
using System.Text;
using System.Web.ModelBinding;
using System.Web.UI.WebControls;
using System.Windows.Forms;

namespace AppTime
{
    public partial class FrmMain : Form
    {

        const string appname = "AppTime";
        const string regkey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public FrmMain()
        {
            InitializeComponent();

        }

        private void FrmMain_Load(object sender, EventArgs e)
        {

            cboRecordScreen.DataSource = new[] {
                new {Text="最近30天", Value=30},
                new {Text="最近15天", Value=15},
                new {Text="无限制", Value=int.MaxValue},
                new {Text="不留存", Value=0},
            };
            cboRecordScreen.DisplayMember = "Text";
            cboRecordScreen.ValueMember = "Value"; 
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
            Settings.Default.BufferSeconds = trackBufferSeconds.Value;
            Settings.Default.ImageQuality = trackImageQuality.Value;
            Settings.Default.RecordScreenDays = (int)cboRecordScreen.SelectedValue;
            Settings.Default.Save();

            using var reg = Registry.CurrentUser.CreateSubKey(regkey);
            try
            {
                if (chkAutoRun.Checked)
                {
                    reg.SetValue(appname, Application.ExecutablePath);
                }
                else
                {
                    reg.DeleteValue(appname);
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("设置启动失败，请检查：\r\n\r\n1、关闭杀毒软件（如360等）；\r\n2、以管理员身份运行本程序。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            this.Hide();
        }

        private void FrmMain_Shown(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void btnSetting_Click(object sender, EventArgs e)
        {
            if (!this.Visible)
            {
                if (string.IsNullOrEmpty(Settings.Default.DataPath))
                {
                    txtDataPath.Text = Application.StartupPath;
                }
                else
                {
                    txtDataPath.Text = Settings.Default.DataPath;
                }
                cboRecordScreen.SelectedValue = Settings.Default.RecordScreenDays;
                trackBufferSeconds.Value = Settings.Default.BufferSeconds;
                trackImageQuality.Value = Settings.Default.ImageQuality;
                trackBufferSeconds_Scroll(null, null);

                using var reg = Registry.CurrentUser.CreateSubKey(regkey);
                chkAutoRun.Checked = (reg.GetValue(appname) as string) == Application.ExecutablePath;

                this.Show();
            }
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

        private void trackBufferSeconds_Scroll(object sender, EventArgs e)
        {
            lblBufferSeconds.Text = Math.Round(TimeSpan.FromSeconds(trackBufferSeconds.Value).TotalMinutes) + "分钟";
        }
    }
}
