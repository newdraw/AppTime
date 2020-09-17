
using AppTime.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppTime
{
    class Recorder
    {

        public Recorder()
        {
            BuildDataPath();
        }

        /// <summary>
        /// 统计周期。
        /// </summary>
        public int IntervalMs = 1000;
        public void Start()
        {
            new Thread(RecorderThreadProc) { IsBackground = true }.Start();
        }

        class App
        {
            public string WinText;
            public string AppProcess;
            public DateTime TimeStart;
            public long WinId;
        }

        public void BuildDataPath()
        {
            Directory.CreateDirectory(IconPath);
            Directory.CreateDirectory(ScreenPath);
        }

        Dictionary<int, Process> processes;
        public Process GetProcess(int processID)
        { 
            if (processes != null && processes.TryGetValue(processID, out var p))
            {
                return p;
            }

            processes = Process.GetProcesses().ToDictionary(p => p.Id);
            return processes[processID];
        }
         
        class app
        {
            public long id;
            public string process;
            public Dictionary<string, win> wins = new Dictionary<string, win>();
        }

        class win
        {
            public long id;
            public string text;
        }

        long nextAppId = 0;

        Icon GetIcon(string fileName, bool largeIcon)
        {
            var shfi = new SHFILEINFO();
            WinApi.SHGetFileInfo(fileName, 0, ref shfi,
                (uint)Marshal.SizeOf(shfi),
                (uint)FileInfoFlags.SHGFI_ICON | (uint)FileInfoFlags.SHGFI_USEFILEATTRIBUTES
                | (uint)(largeIcon ? FileInfoFlags.SHGFI_LARGEICON : FileInfoFlags.SHGFI_SMALLICON)
            );

            return Icon.FromHandle(shfi.hIcon);  
        }

        void SaveIcon(Icon icon, string filename)
        {
            using var img = icon.ToBitmap();
            img.Save(filename);
        } 

        public string GetIconPath(long appId, bool large)
        {
            return Path.Combine(IconPath, $"{appId}{(large ? "l" : "s")}.png");
        }

        Dictionary<string, app> apps = new Dictionary<string, app>();
        app GetApp(Process process)
        {
            var name = process.ProcessName;
            if (apps.TryGetValue(name, out var app))
            {
                return app;
            }

            var data = db.ExecuteDynamic(
                    @"select id from app where process = @process",
                    new SQLiteParameter("process", name)
            ).FirstOrDefault();
             
            if (data == null)
            {
                if (nextAppId == 0)
                {
                    nextAppId = (int)(long)db.ExecuteData("select ifnull(max(id),0) + 1 from app")[0][0];
                }

                var text = "";
                try
                {
                    text = process.MainModule.FileVersionInfo.FileDescription;
                }
                catch(Win32Exception ex)
                {
                    //ignore
                }
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = process.ProcessName;
                }

                using var iconl = GetIcon(process.MainModule.FileName, true);
                SaveIcon(iconl, GetIconPath(nextAppId, true));

                using var icons = GetIcon(process.MainModule.FileName, false);
                SaveIcon(icons, GetIconPath(nextAppId, false)); 

                db.Execute(
                    "insert into app (id, process, text, tagId) values(@id, @process, @text, 0)",
                    new SQLiteParameter("id", nextAppId),
                    new SQLiteParameter("process", name),
                    new SQLiteParameter("text", text)
                );

                
                app = new app { id = nextAppId, process = name };
                nextAppId++;
            }
            else
            {
                app = new app
                {
                    id = data.id,
                    process = name
                };
            }

            apps.Add(name, app);

            //fix icons
            var largeIconPath = GetIconPath(app.id, true);
            if (!File.Exists(largeIconPath))
            {
                using var iconl = GetIcon(process.MainModule.FileName, true);
                SaveIcon(iconl, largeIconPath);

                using var icons = GetIcon(process.MainModule.FileName, false);
                SaveIcon(icons, GetIconPath(app.id, false));
            }
            return app;

        }

        long nextWinId = 0;
        win GetWin(Process process, string winText)
        {
            var app = GetApp(process);
            if (app.wins.TryGetValue(winText, out var win))
            {
                return win;
            }

            var data = db.ExecuteDynamic(
                "select id from win where appid=@appid and text=@winText",
                new SQLiteParameter("appid", app.id),
                new SQLiteParameter("winText", winText)
            ).FirstOrDefault();

            if (data == null)
            {
                if (nextWinId == 0)
                {
                    nextWinId = (int)(long)db.ExecuteData("select ifnull(max(id),0) + 1 from win")[0][0];
                }
                db.Execute(
                    "insert into win (id, appId, text) values(@id, @appId, @text)",
                    new SQLiteParameter("id", nextWinId),
                    new SQLiteParameter("appId", app.id),
                    new SQLiteParameter("text", winText)
                );
                win = new win { id = nextWinId, text = winText };
                nextWinId++;
            }
            else
            {
                win = new win
                {
                    id = data.id,
                    text = winText
                };
            }

            app.wins.Add(winText, win);
            return win;
        }

        DB db = DB.Instance;

        public void RecorderThreadProc()
        {
            App lastApp = null;
            while (true)
            {
                var now = DateTime.Now; 
                var hwnd = WinApi.GetForegroundWindow();
                var text = new StringBuilder(255);
                WinApi.GetWindowText(hwnd, text, 255);
                var winText = text.ToString();
                WinApi.GetWindowThreadProcessId(hwnd, out var processid);
                var process = GetProcess(processid);
                var appname = process.ProcessName;

                if (lastApp != null)
                {
                    db.Execute(
                        "update period set timeend = @v1 where timestart = @v0",
                        lastApp.TimeStart,
                        now.AddMilliseconds(-1)//必须减小，否则可能与下个周期开始时间重叠 
                    );
                }

                if (lastApp == null || lastApp.AppProcess != appname || lastApp.WinText != winText)
                {
                    var win = GetWin(process, winText);
                    lastApp = new App { WinId = win.id, AppProcess = appname, TimeStart = now, WinText = winText };
                    db.Execute(
                        "insert into [period](winid, timeStart, timeEnd) values(@v0, @v1, @v1)",
                        win.id, now
                    );
                }

                Screenshot(now, lastApp);

                var nextTime = now.AddMilliseconds(IntervalMs);
                now = DateTime.Now;
                Thread.Sleep(nextTime > now ? nextTime - now : TimeSpan.Zero);//等到下一个周期
            }
        }


        EncoderParameters ep = null;

        string DataPath => string.IsNullOrWhiteSpace(Settings.Default.DataPath) ? Application.StartupPath : Settings.Default.DataPath;
        public string ScreenPath => Path.Combine(DataPath, "images");
        public string IconPath => Path.Combine(DataPath, "icons");
        ImageCodecInfo jpgcodec = ImageCodecInfo.GetImageDecoders().First(codec => codec.MimeType == "image/jpeg");


        public string getImageFile(DateTime timeStart, DateTime timeImage)
        {
            var folder = Path.Combine(ScreenPath, timeImage.ToString("yyyyMMdd"));
            var filename = $"{timeStart:HHmmss}+{Math.Round((timeImage - timeStart).TotalSeconds)}";
            return Path.Combine(folder, $"{filename}.jpg");
        }

        DateTime lastCheck = DateTime.MinValue.Date;

        void Screenshot(DateTime now, App lastApp)
        {
            //检查记录天数限制
            if (lastCheck != now.Date)
            {
                var firstDate = now.Date.AddDays(-Settings.Default.RecordScreenDays);
                var dirs = Directory.EnumerateDirectories(ScreenPath, "????????");
                foreach (var i in dirs)
                {
                    if (DateTime.TryParseExact(Path.GetFileName(i), "yyyyMMdd", CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
                    {
                        if (date < firstDate)
                        {
                            Directory.Delete(i);
                        }
                    }
                }
                lastCheck = now.Date;
            }

            if (Settings.Default.RecordScreenDays == 0)
            {
                return;
            }

            var path = getImageFile(lastApp.TimeStart, now);
            var folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (ep == null)
            {
                ep = new EncoderParameters(1);
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, new long[] { 10 });
            }

            using var img = GetScreen();
            using var mem = new MemoryStream();
            img.Save(mem, jpgcodec, ep);
            var file = new ImageFile { Data = mem.ToArray(), FileName = path }; 
            ScreenBuffer.Add(file.FileName, file);
            BufferSize += file.Data.Length; 
            if (BufferSize > Settings.Default.ScreenBufferMB * 1024 * 1024)
            {
                FlushScreenBuffer();
            }
        }

        public void FlushScreenBuffer()
        {
            Parallel.ForEach(ScreenBuffer.Values, i => File.WriteAllBytes(i.FileName, i.Data));
            ScreenBuffer.Clear();
            BufferSize = 0;
        }

        public class ImageFile
        {
            public string FileName;
            public byte[] Data;
        }

        public Dictionary<string, ImageFile> ScreenBuffer = new Dictionary<string, ImageFile>();
        int BufferSize = 0;

        Bitmap GetScreen()
        {
            var result = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            using var g = Graphics.FromImage(result);
            retry:
            try
            {
                g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);
            }
            catch
            {
                goto retry;
            }
            return result;
        }
    }
}
