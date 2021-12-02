using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace web_app_server
{
    public class Log
    {

        public static Object logLock = new object();
        public static void log(string data)
        {
            lock (logLock)
            {
                Console.WriteLine(data);
                File.AppendAllText("log.txt", data + "\r\n");
            }
        }
    }
}
