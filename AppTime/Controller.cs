 
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
                color = appColors[appId] = getColor(text);
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
 

        public class TimeInfo
        {
            public DateTime timeSrc;
            public DateTime timeStart;
            public string app;
            public long appId;
            public string title;
        }

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

        public byte[] getImage(TimeInfo info)
        {

            var path = Path.Combine(
                Recorder.ScreenPath,
                info.timeSrc.ToString("yyyyMMdd"),
                $"{info.timeStart:HHmmss}+{(int)(info.timeSrc - info.timeStart).TotalSeconds}.jpg"
            );

            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return File.ReadAllBytes(path);
            }

            if (Recorder.ScreenBuffer.TryGetValue(path, out var file))
            {
                return file.Data;
            }

            if (imageNone == null)
            {
                imageNone = File.ReadAllBytes(Path.Combine(Server.WebRootPath, "img", "none.png"));
            }

            return imageNone;
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
