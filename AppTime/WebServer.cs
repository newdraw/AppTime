using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text; 
using System.Threading;
using System.Web;
using System.Windows.Forms;

namespace AppTime
{
    class WebServer
    {  
        HttpListener listener;
        Thread thread;
        //public HttpListenerRequest Request;
        //public HttpListenerResponse Response;
        public string WebRootPath;
        HashSet<string> defaultPage = new HashSet<string>(StringComparer.OrdinalIgnoreCase){
                    "index.html",
                    "index.htm",
                    "default.html",
                    "default.htm"
                };

        public void Start(int port, object controller, string webfolder = "web")
        {
            thread = new Thread(() =>
            {
                WebRootPath = webfolder = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), webfolder);
                var folders = Directory.GetDirectories(webfolder, "*", SearchOption.AllDirectories);

                var webroot = $@"http://localhost:{port}/";


                listener = new HttpListener();
                foreach (var f in folders)
                {
                    listener.Prefixes.Add(webroot + f.Substring(webfolder.Length + 1).Replace("\\", "/") + "/");
                }
                listener.Prefixes.Add(webroot);
                listener.Start();

                while (true)
                {
                    var ctx = listener.GetContext();
                    //Debug.WriteLine($"Web Request：{ctx.Request.Url}");
                    //ThreadPool.QueueUserWorkItem（_ => processRequest(ctx, webfolder, controller));
                    new Thread(() => processRequest(
                        ctx,
                        webfolder,
                        controller)
                    )
                    { IsBackground = true, Name = "WebServer Process Request" }.Start();
                }
            })
            {
                Name = "WebServer",
                IsBackground = true
            };
            thread.Start();
        }


        void processRequest(HttpListenerContext context, string webfolder, object controller)
        {
            var request = context.Request; 
            var file = webfolder + request.RawUrl.Replace("/", "\\");
            string query = "";
            var p = file.IndexOf("?");
            if (p > 0)
            {
                query = file.Substring(p + 1);
                file = file.Substring(0, p);
            }
            var response = context.Response;
            byte[] data = null;


            if (Path.GetFileName(Path.GetDirectoryName(file)).Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        using var reader = new StreamReader(request.InputStream);
                        query = reader.ReadToEnd();
                    }
                    var info = JObject.Parse(HttpUtility.UrlDecode(query));
                    var method = controller.GetType().GetMethod(Path.GetFileName(file));
                    var args = info.Value<JArray>("args").Cast<object>().ToArray();
                    var @params = method.GetParameters();
                    for (var i = 0; i < args.Length; i++)
                    {
                        if (args[i] is JObject jo)
                        {
                            args[i] = jo.ToObject(@params[i].ParameterType);
                        }
                        else
                        {
                            args[i] = Convert.ChangeType(args[i], @params[i].ParameterType);
                        }
                    } 

                    var result = method.Invoke(controller, args);
                    if (result is byte[] bytes)
                    {
                        data = bytes;
                    }
                    else
                    {
                        data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
                    }
                }
                catch (Exception ex)
                {
                    data = Encoding.UTF8.GetBytes($"bad request: method:{Path.GetFileName(file)} query:{query}");
                }
            }
            else
            {
                if (Directory.Exists(file))
                {
                    var def = Directory.GetFiles(file).FirstOrDefault(f => defaultPage.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase));
                    if (def != null)
                    {
                        file = def;
                    }
                }

                if (File.Exists(file))
                {
                    var mime = MimeMapping.GetMimeMapping(file);
                    response.Headers.Add("Content-type", mime);
                    if (mime.StartsWith("text/"))
                    {
                        response.ContentEncoding = Encoding.GetEncoding("utf-8");
                        data = Encoding.UTF8.GetBytes(File.ReadAllText(file, Encoding.Default));
                    }
                    else
                    {
                        data = File.ReadAllBytes(file);
                    }
                }
            }

            if (data == null)
            {
                data = Encoding.UTF8.GetBytes($"file not found: {file}");
            }

            response.ContentLength64 = data.Length;

            var output = response.OutputStream;
            try
            {
                output.Write(data, 0, data.Length);
            }
            catch (HttpListenerException ex)
            {

            }
            output.Close();
        }

         
        public void Stop()
        {
            thread.Abort();
            listener.Stop();
            listener.Close();
        }
    }
}
