 
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
            init = new InitDB();
            init.Start();

            recorder = new Recorder();
            recorder.Start();

            controller = new Controller();
            server = new WebServer();
            server.Start(Port, controller, "../../webui");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(frmMain = new FrmMain());
        } 

    }
}
