using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace ilovetvp
{
    class ilovetvp
    {
        static void Main()
        {
            TcpClient connection = null;
            while (connection == null)
            {
                Util.debug(@"Connect");
                try
                {
                    connection = new TcpClient();
                    connection.ReceiveTimeout = 15;
                    connection.SendTimeout = 15;
                    connection.Connect(@"ilovetvp.serverstaff.de", 47616);
                }
                catch (Exception e)
                {
                    Util.debug(@"Connect failed: {0}", e.Message);
                }
            }
            
            var gamelogs = new List<FileStream>();

            var gamelogs_watcher = new FileSystemWatcher(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Eve\logs\Gamelogs");
            gamelogs_watcher.Changed += (sender, e) =>
            {
                var buffer = new byte[1/*file id*/ + 2/*content length*/ + UInt16.MaxValue];
                FileStream current = null;

                lock (gamelogs)
                {
                    for (var i = 0; current == null && i < gamelogs.Count; ++i)
                        if (e.FullPath.EndsWith(gamelogs[i].Name))
                        {
                            buffer[0] = (byte)i; //file id
                            current = gamelogs[i];
                        }

                    if (current == null)
                    {
                        buffer[0] = (byte)gamelogs.Count; //file id
                        try
                        {
                            gamelogs.Add(current = File.Open(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                            Util.debug(@"{0,3} | Start", buffer[0]);
                        }
                        catch
                        {
                            Util.debug(@"Opening file failed: {0}", e.Name);

                            return;
                        }
                    }
                }

                var gamelog_new = current.Position == 0;

                const int LOG_IDENT_LENGTH = 0x200;

                if (gamelog_new && current.Length < LOG_IDENT_LENGTH)
                    return; // wait until file has minimum size

                int content_length;

                try
                {
                    content_length = current.Read(buffer, 1/*file id*/ + 2/*content length*/, gamelog_new ? LOG_IDENT_LENGTH/*content*/ : (buffer.Length - 1/*file id*/ - 2/*content length*/));

                    Debug.Assert(!gamelog_new || (content_length == LOG_IDENT_LENGTH));
                }
                catch (Exception ex)
                {
                    Util.debug(@"{0,3} | Failed to read from file: {1}", buffer[0], ex.Message);

                    try
                    {
                        current.Close();
                    }
                    catch { }

                    lock (gamelogs)
                        if (!gamelogs.Remove(current))
                            throw new Exception(@"Failed to remove file from active gamelogs list after it failed to read from it");

                    return;
                }

                if (content_length == 0)
                    return;

                Array.Copy(BitConverter.GetBytes((UInt16)content_length), 0, buffer, 1, 2); //content length

                try
                {
                    connection.GetStream().Write(buffer, 0, 1/*file id*/ + 2/*content length*/ + content_length/*content*/);

                    Util.debug(@"{0,3} | Send {1} bytes", buffer[0], content_length);

                    if (gamelog_new)
                    {
                        current.Seek(0, SeekOrigin.End); // skip old log messages for gamelog that has not yet been monitored

                        Util.debug(@"{0,3} | Ident packet send, seeked to file end: {1}", buffer[0], current.Position);
                    }
                }
                catch (Exception ex)
                {
                    Util.debug(@"Failed to send {0} bytes via network: {1}", content_length, ex.Message);

                    current.Seek(-content_length, SeekOrigin.Current); // reset to last position, try again later

                    try
                    {
                        connection.GetStream().Close();
                        connection.Close();
                    }
                    catch { }

                    foreach (var gamelog in gamelogs)
                        try
                        {
                            gamelog.Close();
                        }
                        catch { }

                    lock(gamelogs)
                        gamelogs.Clear();

                    connection = null;
                    while (connection == null)
                    {
                        Util.debug(@"Reconnect");
                        connection = new TcpClient();
                        connection.ReceiveTimeout = 15;
                        connection.SendTimeout = 15;
                        connection.Connect(@"ilovetvp.serverstaff.de", 47616);
                    }
                }
            };

            gamelogs_watcher.EnableRaisingEvents = true;

            Console.ReadLine();
        }
    }
}
