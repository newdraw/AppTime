 
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AppTime
{
    static class Program
    {
        public const int Port = 15720;
        public static InitDB init;
        public static Recorder recorder;
        public static WebServer server;
        public static Controller controller;
        public static FrmMain frmMain;

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
#if DEBUG
            CopyWebUI();
#endif

            init = new InitDB();
            init.Start();

            recorder = new Recorder();
            recorder.Start();

            controller = new Controller();
            server = new WebServer();
            server.Start(Port, controller, "./webui");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(frmMain = new FrmMain());
        }


        static void CopyWebUI()
        {
            CopyDirectory("../../webui", "./webui");
        }

        static void CopyDirectory(string src, string dest)
        {
            if (!Directory.Exists(dest))
            {
                Directory.CreateDirectory(dest);
            }

            foreach (var srcfile in Directory.GetFiles(src))
            {
                var destfile = Path.Combine(dest, Path.GetFileName(srcfile));
                //只复制更新的文件
                if (File.Exists(destfile) && File.GetLastWriteTime(srcfile) == File.GetLastWriteTime(destfile))
                {
                    continue;
                }

                File.Copy(srcfile, destfile, true);
            }

            foreach (var srcdir in Directory.GetDirectories(src))
            {
                var destdir = Path.Combine(dest, Path.GetFileName(srcdir));
                CopyDirectory(srcdir, destdir);
            }
        }
    }
}
