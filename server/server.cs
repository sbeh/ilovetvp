using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ilovetvp
{
    struct Client
    {
        public int id;
        public TcpClient client;
    }

    class server
    {
        static bool shutting_down = false;

        static TcpListener listener = new TcpListener(47616);
        static Dictionary<Client, Dictionary<byte, FileStream>> map = new Dictionary<Client, Dictionary<byte, FileStream>>();
        static HashAlgorithm hash = new SHA256Managed();

        static void closed(Client client, string reason = null)
        {
            foreach (var file in map[client])
                try
                {
                    file.Value.Close();

                    Util.debug(@"{0,8} | {1,3} | Stop", client.id, file.Key);
                }
                catch (Exception e)
                {
                    Util.debug(@"{0,8} | {1,3} | Stop failed: {2}", client.id, file.Key, e.Message);
                }

            map[client].Clear();
            map.Remove(client);

            try
            {
                client.client.Close();
            }
            catch (Exception e)
            {
                Util.debug(@"{0,8} | Close failed: {1}", client.id, e.Message);
            }

            if(reason == null)
                Util.debug(@"{0,8} | Disconnect", client.id);
            else
                Util.debug(@"{0,8} | Disconnect: {1}", client.id, reason);
        }

        static void read(Client client)
        {
            var stream = client.client.GetStream();

            var buffer = new byte[1/*file id*/ + 2/*content length*/ + UInt16.MaxValue];

            try
            {
                stream.BeginRead(buffer, 0, 3/*buffer.Length*/, result =>
                {
                    try
                    {
                        var buffer_length = 0;
                        try
                        {
                            buffer_length = stream.EndRead(result);
                        }
                        catch (IOException e)
                        {
                            closed(client, e.Message);
                            return;
                        }

                        if (buffer_length < 3)
                            throw new Exception(@"Procotol violation: Short packet");

                        var content_length = BitConverter.ToUInt16(buffer, 1);
                        while (buffer_length - 1/*file id*/ - 2/*content length*/ < content_length)
                        {
                            var r = stream.Read(buffer, buffer_length, content_length/*buffer.Length - buffer_length*/);
                            if (r == 0)
                                throw new Exception(@"Procotol violation: Incomplete packet");
                            //debug(@"{0,8} | Packet: {1} bytes", client.id, buffer_length);
                            buffer_length += r;
                        }

                        Util.debug(@"{0,8} | Packet: {1} bytes", client.id, content_length);

                        if (!map[client].ContainsKey(buffer[0]))
                        {
                            Util.debug(@"{0,8} | {1,3} | Start", client.id, buffer[0]);

                            const int LOG_IDENT_LENGTH = 0x200;
                            if (content_length != LOG_IDENT_LENGTH)
                                throw new Exception(@"Procotol violation: Wrong length of gamelog identification packet");

                            var ident = BitConverter.ToString(hash.ComputeHash(buffer, 1/*file id*/ + 2/*content length*/, LOG_IDENT_LENGTH)).Replace(@"-", @"");

                            var gamelog_known = File.Exists(ident);

                            map[client].Add(buffer[0], File.Open(ident, FileMode.Append, FileAccess.Write, FileShare.Read));

                            if (gamelog_known) // skip write of ident packet cause this gamelog has already been transfered earlier
                            {
                                Util.debug(@"{0,8} | {1,3} | Known #{2}", client.id, buffer[0], ident);
                                
                                read(client);
                                return;
                            }

                            var ep = (IPEndPoint)client.client.Client.RemoteEndPoint;
                            var epb = Encoding.UTF8.GetBytes(string.Format(@"{0,-512}", string.Format(@"{0}:{1}", ep.Address, ep.Port)));
                            map[client][buffer[0]].Write(epb, 0, 64);
                        }

                        var log = map[client][buffer[0]];
                        log.BeginWrite(buffer, 1/*file id*/ + 2/*content length*/, content_length, log_result =>
                        {
                            log.EndWrite(log_result);
                            log.Flush(true);

                            Util.debug(@"{0,8} | {1,3} | Wrote: {2} bytes", client.id, buffer[0], content_length);

                            read(client);
                        }, null);
                    }
                    catch (Exception e)
                    {
                        closed(client, e.Message);
                    }
                }, null);
            }
            catch (IOException e)
            {
                closed(client, e.Message);
            }
        }

        static void accept()
        {
            listener.BeginAcceptTcpClient(result =>
            {
                Client client;
                client.id = map.Count;
                try
                {
                    client.client = listener.EndAcceptTcpClient(result);
                }
                catch (ObjectDisposedException e)
                {
                    if (shutting_down)
                        return;

                    throw e;
                }

                var endpoint = (IPEndPoint)client.client.Client.RemoteEndPoint;
                Util.debug(@"{0,8} | Connect from {1}:{2}", client.id, endpoint.Address, endpoint.Port);

                map.Add(client, new Dictionary<byte, FileStream>());
                read(client);

                accept();
            }, null);
        }

        static void Main()
        {
            listener.Start();

            accept();

            Util.debug(@"Listen");

            while (!Console.ReadLine().Equals(@"stop"))
                ;

            shutting_down = true;
            
            listener.Stop();
            
            foreach (var client in map.Keys)
                client.client.GetStream().Close();

            Util.debug(@"Halt");

            Console.ReadLine();
        }
    }
}
