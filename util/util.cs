using System;

namespace ilovetvp
{
    class Util
    {
#if DEBUG
        static object console_lock = new object();
#endif

        public static void debug(string format, params object[] arg)
        {
#if DEBUG
            lock (console_lock)
                Console.WriteLine(format, arg);
#endif
        }
    }
}
