﻿using System;
#if !NO_DB
using System.Data.SqlClient;
#endif
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ilovetvp
{
    class analyzer
    {
#if !NO_DB
        public static SqlConnection db = new SqlConnection(@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ilovetvp;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False");
#endif

        static void Main()
        {
#if TEST
            var gamelogs = @"..\..\..\server\bin\Debug";
#else
            var gamelogs = Environment.CurrentDirectory;
#endif

#if !NO_DB
            db.Open();
#endif

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
#if TEST
            http.Prefixes.Add(@"http://localhost:47617/");
#else
            http.Prefixes.Add(@"http://ilovetvp.serverstaff.de:47617/");
#endif
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

#if TEST
        static Random rnd = new Random();
#endif

        private static void handle(HttpListenerContext context, HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                response.Headers.Add(HttpResponseHeader.CacheControl, @"no-cache");
                response.ContentType = @"text/html; charset=UTF-8";

                var endpoint = (IPEndPoint)request.RemoteEndPoint;
                var param = request.Url.LocalPath;

                if(param.Equals(@"/site.html") || param.Equals(@"/smoothie.js"))
                {
                    response.Close(System.IO.File.ReadAllBytes(
#if TEST
                        @"..\..\" +
#endif
                        param.Substring(1)), false);
                }
                else if(param.Equals(@"/oneshot") && request.Headers[@"EVE_CHARNAME"] != null)
                {
                    var character = Character.get(request.Headers[@"EVE_CHARNAME"]);
                    character.endpoint = endpoint;

                    Action<Event[]> callback = null;

                    var done = false;

                    new Timer(s =>
                    {
                        callback(null);
                    }, null, 5000, Timeout.Infinite);

                    callback = evs =>
                    {
                        try
                        {
                            lock (response)
                            {
                                if (done)
                                    return;

                                done = true;
                            }

                            character.EventAdded -= callback;

                            if (evs != null)
                            {
                                var json = new StringBuilder();
                                json.Append(@"[[""timestamp"",""weapon"",""damage""]");
                                Array.ForEach(evs, ev =>
                                {
                                    if (ev is CombatEvent)
                                        json.AppendFormat(@",[""{0:O}"",{1},""{2}""]", ev.timestamp, ((CombatEvent)ev).damage, ((CombatEvent)ev).weapon);
                                });
                                json.Append(']');
                                response.ContentType = @"application/json";
                                response.Close(Encoding.UTF8.GetBytes(json.ToString()), false);
                            }
                            else
                                response.Close();
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

                    Action<Event[]> callback = null;
                    callback = evs =>
                    {
                        var body = new StringBuilder();
                        foreach (var ev in evs)
                        {
                            body.Append(ev.ToString());
                            body.Append(@"<br />");
                        }

                        var buffer = Encoding.UTF8.GetBytes(body.ToString());
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