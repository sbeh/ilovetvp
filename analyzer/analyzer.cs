using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ilovetvp
{
    class analyzer
    {
        public static SqlConnection db = new SqlConnection(@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ilovetvp;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False");

        static void Main()
        {
            var gamelogs = Environment.CurrentDirectory;

            db.Open();

            var regexp_gamelog = new Regex(@"\\[A-Z0-9]+$", RegexOptions.Compiled);
            foreach (var log in Directory.GetFiles(gamelogs))
            {
                Debug.Assert(log.StartsWith(gamelogs));

                if (regexp_gamelog.IsMatch(log))
                    File.get(log);
            }

            var gamelogs_watcher = new FileSystemWatcher(gamelogs);
            gamelogs_watcher.NotifyFilter = NotifyFilters.LastWrite;
            gamelogs_watcher.Changed += (sender, e) =>
            {
                Debug.Assert(e.FullPath.StartsWith(gamelogs));

                if (regexp_gamelog.IsMatch(e.FullPath))
                    File.get(e.FullPath).changed();
            };
            gamelogs_watcher.EnableRaisingEvents = true;

            var http = new HttpListener();
            http.Prefixes.Add(@"http://ilovetvp.serverstaff.de:47617/");
            http.Start();
            AsyncCallback callback = null;
            callback = result =>
            {
                HttpListenerContext context;
                try
                {
                    context = http.EndGetContext(result);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                handle(context, context.Request, context.Response);
                
                http.BeginGetContext(callback, null);
            };
            http.BeginGetContext(callback, null);

            Console.ReadLine();

            gamelogs_watcher.EnableRaisingEvents = false;

            http.Stop();
        }

        private static void handle(HttpListenerContext context, HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                response.Headers.Add(HttpResponseHeader.CacheControl, "no-cache");
                response.ContentType = @"text/html; charset=UTF-8";

                var endpoint = (IPEndPoint)request.RemoteEndPoint;
                var param = request.Url.LocalPath;

                if(param.Equals(@"/site.html") || param.Equals(@"/smoothie.js"))
                {
                    response.Close(System.IO.File.ReadAllBytes(param.Substring(1)), false);
                }
                else if(param.Equals(@"/oneshot"))
                {
                    var character = Character.get(request.Headers[@"EVE_CHARNAME"]);
                    character.endpoint = endpoint;

                    Action<Event> callback = null;
                    callback = ev =>
                    {
                        try {
                            response.Close(Encoding.UTF8.GetBytes(character.damage.ToString()), false);
                            character.EventAdded -= callback;
                        }
                        catch (Exception ex)
                        {
                            Util.debug(@"{0,15}:{1,-5} | {2} | Failed to write: {2}", endpoint.Address, endpoint.Port, character, ex.Message);
                        }
                    };
                    character.EventAdded += callback;

                }
                else if (param.StartsWith(@"/livelog/") && param.Length > @"/livelog/".Length + 3 /*minimum character name length*/)
                {
                    var character = param.Substring(@"/livelog/".Length);

                    Util.debug(@"{0,15}:{1,-5} | {2} | Start", endpoint.Address, endpoint.Port, character);

                    response.SendChunked = true;

                    Action<Event> callback = null;
                    callback = ev =>
                    {
                        var buffer = Encoding.UTF8.GetBytes(ev.ToString() + @"<br />");
                        try
                        {
                            response.OutputStream.Write(buffer, 0, buffer.Length);
                            response.OutputStream.Flush();
                            Util.debug(@"{0,15}:{1,-5} | {2} | Send {3} bytes", endpoint.Address, endpoint.Port, character, buffer.Length);
                        }
                        catch (Exception ex)
                        {
                            Util.debug(@"{0,15}:{1,-5} | {2} | Failed to write: {2}", endpoint.Address, endpoint.Port, character, ex.Message);

                            try
                            {
                                response.Close();
                            }
                            catch { }

                            Character.get(character).EventAdded -= callback;
                        }
                    };
                    Character.get(character).EventAdded += callback;
                }
                else
                {
                    Util.debug(@"{0,15}:{1,-5} | Unkown request: {2}", endpoint.Address, endpoint.Port, request.Url.LocalPath);

                    response.Close();
                }
            }
            catch(Exception e)
            {
                Util.debug(@"HTTP request failed: {0}", e.Message);

                try
                {
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response.Close();
                }
                catch { }
            }
        }
    }
}