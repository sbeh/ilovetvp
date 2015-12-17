using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ilovetvp
{
    class File
    {

        bool ignore = true;

        StreamReader stream;
        string path;
        Character character;

        private File(string path) {
            this.path = path;

            stream = new StreamReader(System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Encoding.UTF8);
            stream.BaseStream.Seek(64, SeekOrigin.Current); // skip network source info

            ident();
        }

        #region File ident packet
        private const int LOG_IDENT_LENGTH = 0x200;

        private void ident()
        {
            var ident = new byte[LOG_IDENT_LENGTH];
            stream.BaseStream.BeginRead(ident, 0, ident.Length, identRead, ident);
        }

        private static Regex regexp_ident = new Regex(@"^-+\r\n  Gamelog\r\n  Listener: (?<Character>.+)\r\n  Session Started: (?<Timestamp>\d\d\d\d\.\d\d\.\d\d \d\d:\d\d:\d\d)\r\n-+", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private void identRead(IAsyncResult ar)
        {
            var read = stream.BaseStream.EndRead(ar);
            if(read != LOG_IDENT_LENGTH)
            {
                Util.debug(@"{0} | Short read for ident packet", path);
                return;
            }

            var match_ident = regexp_ident.Match(Encoding.UTF8.GetString((byte[])ar.AsyncState));
            if (!match_ident.Success)
            {
                Util.debug(@"{0} | Ident regex failed to match", path);
                return;
            }

            character = Character.get(match_ident.Groups[@"Character"].Value);

            ignore = false;
            changed();
        }
        #endregion

        public void changed()
        {
            if (ignore)
                return;

            string line;
            while ((line = stream.ReadLine()) != null)
                Event.create(character, line);
        }

        #region Singleton
        private static Dictionary<string, File> files = new Dictionary<string, File>();

        public static File get(string path)
        {
            try
            {
                return files[path];
            } catch
            {
                // file not yet loaded into memory
            }

            Util.debug(@"{0} | Start Gamelog", path);

            var file = new File(path);
            files.Add(path, file);
            return file;
        }
        #endregion
    }
}
