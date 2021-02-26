using AppTime.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AppTime.Recorder;

namespace AppTime
{
    class Ffmpeg
    {

        static Process lastFfmpeg; 

        public static void KillLastFfmpeg()
        {
            if (lastFfmpeg != null && !lastFfmpeg.HasExited)
            {
                Utils.Try(() => lastFfmpeg.Kill());
                lastFfmpeg = null;
            }
        }

        public static byte[] Snapshot(string file, TimeSpan time)
        {
            var args = $@"-loglevel quiet -ss {time} -i ""{file}"" -y -frames 1 -q:v 2 -f image2 -";


            var info = new ProcessStartInfo(@"ffmpeg\ffmpeg.exe", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath)
            };

            var p = lastFfmpeg = Process.Start(info);

            var output = p.StandardOutput.BaseStream;
            var data = new List<byte>();
            var b = output.ReadByte();
            while (b != -1)
            {
                data.Add((byte)b);
                b = output.ReadByte();
            }

            return data.ToArray();

        }


        public static void Save(string file, params Frame[] images)
        {
            if (images.Length == 0)
            {
                return;
            }

            var rate = images.Length / ((images.Last().Time - images.First().Time).TotalSeconds + 1);
            var crf = Settings.Default.ImageQuality;//0-质量最高 63-质量最低 实测40质量也不错且体积较小

            var tempfile = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".tmp");

            var args = $@"-loglevel quiet -f image2pipe -r {rate} -i - -vcodec libx264 -crf {crf} -f matroska -y ""{tempfile}""";
            var info = new ProcessStartInfo(@"ffmpeg\ffmpeg.exe", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath)
            };
            var p = Process.Start(info);
            p.PriorityClass = ProcessPriorityClass.BelowNormal;
             
            foreach (var i in images)
            {
                p.StandardInput.BaseStream.Write(i.Data, 0, i.Data.Length); 
            }
            p.StandardInput.Close();
            p.WaitForExit();
            if (File.Exists(file))
            {
                File.Delete(file);
            }
            File.Move(tempfile, file);
        }
    }

    public class Frame
    {
        public TimeSpan Time;
        public byte[] Data;
        public Frame(TimeSpan time, byte[] data)
        {
            this.Time = time;
            this.Data = data;
        }
    }

}
