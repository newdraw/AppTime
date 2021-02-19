 
using System;
using System.Collections.Generic;
using System.Data;
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
using static AppTime.Recorder;

namespace AppTime
{
    class Controller
    {
        public WebServer Server =>Program.server;
        public Recorder Recorder=>Program.recorder;


        #region basic
    
        int getColor(string str)
        {
            return str.GetHashCode(); 
        } 

        static Dictionary<long, int> appColors = new Dictionary<long, int>();

        int getAppColor(long appId)
        {
            if(!appColors.TryGetValue(appId, out var color))
            { 
                var text = db.ExecuteValue<string>($"select text from app where id={appId}");
                lock (appColors)
                {
                    color = appColors[appId] = getColor(text);
                }
            }
            return color;
        }

        static Dictionary<long, int> tagColors = new Dictionary<long, int>();
        int getTagColor(long tagId)
        {
            if (!tagColors.TryGetValue(tagId, out var color))
            {
                if (tagId == -1)
                {
                    color = tagColors[tagId] = BitConverter.ToInt32(new byte[] { 0xFF, 0x99, 0x99, 0x99 }, 0);
                }
                else
                {
                    var text = db.ExecuteValue<string>($"select text from tag where id={tagId}");
                    color = tagColors[tagId] = getColor(text);
                }
            }
            return color;
        }


        public unsafe byte[] getPeriodBar(DateTime timefrom, DateTime timeto, string view, int width)
        {
            if (width <= 0 || width > 8000)
            {
                return null;
            }


            var totalsecs = (timeto - timefrom).TotalSeconds;
            IEnumerable<dynamic> data;
            if (view == "app")
            {
                data = db.ExecuteDynamic(@"
select  
	app.id appId,
	p.timeStart,
	p.timeEnd
from app
join win on win.appid = app.id
join period p on p.winid = win.id   
where 
    timeStart between @v0 and @v1 
    or timeEnd between @v0 and @v1
    or @v0 between timeStart and timeEnd
order by p.timeStart
",
                timefrom, timeto
                );
            }
            else
            {
                data = db.ExecuteDynamic(@"
select  
	ifnull(tag.id,-1) tagId,
	p.timeStart,
	p.timeEnd
from app
join win on win.appid = app.id
join period p on p.winid = win.id  
left join tag on tag.id = win.tagId or (win.tagId = 0 and tag.id = app.tagId) 
where 
    timeStart between @v0 and @v1 
    or timeEnd between @v0 and @v1
    or @v0 between timeStart and timeEnd
order by p.timeStart
",
                timefrom, timeto
                );
            }

            //绘制PeriodBar，直接写内存比gdi+快
            var imgdata = new int[width]; 
            foreach (var period in data)
            {
                var from = Math.Max(0, (int)Math.Round((period.timeStart - timefrom).TotalSeconds / totalsecs * width));
                var to = Math.Min(width - 1, (int)Math.Round((period.timeEnd - timefrom).TotalSeconds / totalsecs * width));

                for (var x = from; x <= Math.Min(width - 1, to); x++)
                {
                    imgdata[x] = view == "app" ? (int)getAppColor(period.appId) : (int)getTagColor(period.tagId);
                }
            }
             
            fixed (int* p = &imgdata[0])
            {
                var ptr = new IntPtr(p);
                using var bmp = new Bitmap(width, 1, width * 4, PixelFormat.Format32bppArgb, ptr);
                using var mem = new MemoryStream();
                bmp.Save(mem, ImageFormat.Png);
                return mem.ToArray();
            }

        }

        public object getTree(DateTime timefrom, DateTime timeto, string view, long parentKey)
        {
            var result = new List<object>();
            var totalSeconds = (timeto - timefrom).TotalSeconds;
            IEnumerable<dynamic> data;

            if (view == "app")
            {
                if (parentKey == 0)
                {
                    data = db.ExecuteDynamic(@"
select 
	app.id appId,
	app.text appText,
    tag.text tagText,
	sum(julianday(case when timeEnd > @v1 then @v1 else timeEnd end) - 
        julianday(case when timeStart < @v0 then @v0 else timeStart end)) days
from app
join win on win.appid = app.id
join period p on p.winid = win.id
left join tag on tag.id = app.tagId
where 
    timeStart between @v0 and @v1 
    or timeEnd between @v0 and @v1
    or @v0 between timeStart and timeEnd
group by app.id 
order by days desc
",
        timefrom, timeto
    );

                    foreach (var i in data)
                    {
                        var time = new TimeSpan((long)(i.days * TimeSpan.TicksPerDay));
                        result.Add(
                            new
                            {
                                i.appId,
                                i.tagText,
                                text = i.appText,
                                time = time.ToString(@"hh\:mm\:ss"),
                                percent = Math.Round(time.TotalSeconds * 100 / totalSeconds, 2) + "%",
                                children = new object[0]
                            }
                        );
                    }

                    return result;
                }

                data = db.ExecuteDynamic(@"
select 
	win.id winId,
	win.text winText,
    tag.text tagText,
	sum(julianday(case when timeEnd > @v1 then @v1 else timeEnd end) - 
        julianday(case when timeStart < @v0 then @v0 else timeStart end)) days
from win
join period p on p.winid = win.id
left join tag on tag.id = win.tagId
where 
    win.appId = @v2
    and (
        timeStart between @v0 and @v1 
        or timeEnd between @v0 and @v1
        or @v0 between timeStart and timeEnd
    )
group by win.id 
order by days desc
",
                timefrom, timeto, parentKey 
);

                foreach (var i in data)
                {
                    var time = new TimeSpan((long)(i.days * TimeSpan.TicksPerDay));
                    result.Add(
                        new
                        {
                            i.winId,
                            i.tagText,
                            text = string.IsNullOrWhiteSpace(i.winText) ? "(无标题)" : i.winText,
                            time = time.ToString(@"hh\:mm\:ss"),
                            percent = Math.Round(time.TotalSeconds * 100 / totalSeconds, 2) + "%"
                        }
                    );
                }

                return result;
            }


            if (parentKey == 0)
            {

                data = db.ExecuteDynamic(@"
select 
    ifnull(tag.id,-1) tagId,
    ifnull(tag.text, '(无标签)') tagText,
	sum(julianday(case when timeEnd > @v1 then @v1 else timeEnd end) - 
        julianday(case when timeStart < @v0 then @v0 else timeStart end)) days
from win
join app on app.id = win.appid
join period p on p.winid = win.id
left join tag on tag.id = win.tagId or (win.tagId = 0 and tag.id = app.tagId)
where 
    timeStart between @v0 and @v1 
    or timeEnd between @v0 and @v1
    or @v0 between timeStart and timeEnd
group by tag.id
order by days desc 
",
                    timefrom, timeto
                );

                foreach (var i in data)
                {
                    var time = new TimeSpan((long)(i.days * TimeSpan.TicksPerDay));
                    result.Add(
                        new
                        {
                            i.tagId,
                            i.tagText,
                            time = time.ToString(@"hh\:mm\:ss"),
                            percent = Math.Round(time.TotalSeconds * 100 / totalSeconds, 2) + "%"
                        }
                    );
                }
                return result;

            }

            data = db.ExecuteDynamic(@"
select 
	win.id winId,
	win.text winText, 
	sum(julianday(case when timeEnd > @v1 then @v1 else timeEnd end) - 
        julianday(case when timeStart < @v0 then @v0 else timeStart end)) days
from win
join period p on p.winid = win.id
join app on app.id = win.appid
left join tag on tag.id = win.tagId or (win.tagId = 0 and tag.id = app.tagId)
where 
    ifnull(tag.id, -1) = @v2
    and (
        timeStart between @v0 and @v1 
        or timeEnd between @v0 and @v1
        or @v0 between timeStart and timeEnd
    )
group by win.id 
order by days desc
",
                timefrom, timeto, parentKey
            );

            foreach (var i in data)
            {
                var time = new TimeSpan((long)(i.days * TimeSpan.TicksPerDay));
                result.Add(
                    new
                    {
                        i.winId,
                        text = string.IsNullOrWhiteSpace(i.winText) ? "(无标题)" : i.winText,
                        time = time.ToString(@"hh\:mm\:ss"),
                        percent = Math.Round(time.TotalSeconds * 100 / totalSeconds, 2) + "%"
                    }
                );
            }

            return result;
        }
 
        /// <summary>
        /// 时间
        /// </summary>
        public class TimeInfo
        {
            /// <summary>
            /// 原始时间
            /// </summary>
            public DateTime timeSrc;
            /// <summary>
            /// 切换到应用的时间
            /// </summary>
            public DateTime timeStart;
            /// <summary>
            /// 应用名
            /// </summary>
            public string app;
            /// <summary>
            /// 应用id
            /// </summary>
            public long appId;
            /// <summary>
            /// 窗口标题
            /// </summary>
            public string title;
        }

        /// <summary>
        /// 获取指定时间的记录信息
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public TimeInfo getTimeInfo(DateTime time)
        {
            
            var data = db.ExecuteDynamic(@" 
SELECT timeStart, app.text appText, win.text winText, app.id appId
from period
join win on win.id = period.winid
join app on app.id = win.appid
where @v0 between timeStart and timeEnd 
limit 1",
               time
           ).FirstOrDefault();

            if (data == null)
            {
                return null;
            }
              
            return new TimeInfo
            {
                timeSrc = time,
                timeStart = data.timeStart,
                app = data.appText, 
                title = data.winText, 
                appId = data.appId
            };
        }

        public byte[] getIcon(int appId, bool large)
        {
            var path = Recorder.GetIconPath(appId, large);
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
            return null;
        }

        static byte[] imageNone = null;

        TimeSpan getTime(string file)
        {
            return TimeSpan.ParseExact(Path.GetFileNameWithoutExtension(file), "hhmmss", CultureInfo.InvariantCulture);
        }
         

        /// <summary>
        /// 查找不满足条件的最后一个元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="largerThenTarget"></param>
        /// <returns></returns>
        T find<T>(IList<T> items, Func<T, bool> largerThenTarget)
        {
            if (items.Count == 0)
            {
                return default;
            }

            var match = items[0];
            if (largerThenTarget(match))
            {
                return default;
            }

            for (var i = 1; i < items.Count; i++)
            {
                var item = items[i];
                if (largerThenTarget(item))
                {
                    break;
                }
                match = item;
            }
            return match;
        }


        static Thread lastThread;
        static readonly object threadLock = new object();
        /// <summary>
        /// 获取指定时间的截图
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public byte[] getImage(TimeInfo info)
        {

            if (imageNone == null)
            {
                imageNone = File.ReadAllBytes(Path.Combine(Server.WebRootPath, "img", "none.png"));
            }

            //只响应最后一个请求，避免运行多个ffmpeg占用资源。
            lock (threadLock)
            {
                Ffmpeg.KillLastFfmpeg();
                if (lastThread != null && lastThread.IsAlive)
                {
                    lastThread.Abort();
                }
                lastThread = Thread.CurrentThread;
            }

            try
            {
                //先从buffer中找
                {
                    var buffers = new List<MemoryBuffer>(Recorder.flushing);
                    if (Recorder.buffer != null)
                    {
                        buffers.Add(Recorder.buffer);
                    }

                    var match = find(buffers, i => i.StartTime > info.timeSrc);
                    if (match != null)
                    {
                        var time = info.timeSrc - match.StartTime;
                        var frame = find(match.Frames, f => (match.StartTime + f.Time) > info.timeSrc);
                        if (frame != null)
                        {
                            return frame.Data;
                        }
                    }
                }


                //从文件系统找 
                {
                    var path = Recorder.getFileName(info.timeSrc); 
                    var files = (from f in Directory.GetFiles(Path.GetDirectoryName(path), "????????." + Recorder.ExName) orderby f select f).ToArray(); 
                    var needtime = info.timeSrc.TimeOfDay;// getTime(path);
                    var match = find(files, i => getTime(i) > needtime);
                    if (match != null)
                    { 
                        var time = needtime - getTime(match);
                        var data = Ffmpeg.Snapshot(match, time); 
                        if (data != null)
                        {
                            return data;
                        }
                    }

                    return imageNone;
                }
            }
            catch (ThreadAbortException)
            {
                return imageNone;
            }
        }

        #endregion

        #region tag

        private long nextTagId = 0;
        private long NextTagId()
        { 
            if (nextTagId == 0)
            {
                nextTagId = db.ExecuteValue<long>("select ifnull(max(id), 0) + 1 from tag");
            }
            return nextTagId++;
        }
          
        public bool existsTag(string text)
        { 
            return db.ExecuteValue< bool>(
                "select exists(select * from tag where text = @text)", 
                new SQLiteParameter("text", text)
            );
        }

        public bool addTag(string text)
        {
            if(existsTag(text))
            {
                return false;
            }
            
            db.Execute(
                "insert into tag (id, text) values(@id, @text)",
                new SQLiteParameter("id", NextTagId()),
                new SQLiteParameter("text", text)
            );
            return true;
        }

        public void removeTag(int tagId)
        {
            
            db.Execute(
                "delete from tag where id = @id",
                new SQLiteParameter("id", tagId)
            );

            db.Execute($"update app set tagId=0 where tagId={tagId}");
            db.Execute($"update win set tagId=0 where tagId={tagId}");

        }

        public void clearAppTag(long appId)
        {
            db.Execute("update app set tagid = 0 where id = @v0", appId);
        }

        public void clearWinTag(long winId)
        {
            db.Execute("update win set tagid = 0 where id = @v0", winId);
        }

        public DataTable getTags()
        { 
            return db.ExecuteTable("select id, text from tag order by id");
        }


        public bool isTagUsed(int tagId)
        {
            return db.ExecuteValue<bool>(
                @"select exists(
select * from app where tagId = @tagId
union all
select * from win where tagId = @tagId
)",
                new SQLiteParameter("tagId", tagId)
            );
        }

        DB db = DB.Instance;

        public bool renameTag(long tagId, string newName)
        {
            if(existsTag(newName))
            {
                return false;
            }

            db.Execute("update tag set text=@newName where id=@tagId",
                new SQLiteParameter("newName", newName),
                new SQLiteParameter("tagId", tagId)
            );


            return true;
        }

        public void tagApp(long appId, long tagId)
        {
            db.Execute(
                "update app set tagid = @tagId where id=@appId",
                new SQLiteParameter("appId", appId),
                new SQLiteParameter("tagId", tagId)
            );
        }

        public void tagWin(long winId, long tagId)
        {
            db.Execute(
                "update win set tagid = @tagId where id=@winId",
                new SQLiteParameter("winId", winId),
                new SQLiteParameter("tagId", tagId)
            );
        }
         
        #endregion
    }
}
