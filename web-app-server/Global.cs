using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace web_app_server
{
    public class Global
    {
        public static bool Shutdown = false;

        public static Dictionary<string, string> settings = new Dictionary<string, string>();


        public static uint randSeed;
        public static Object randLock = new Object();
        public static uint RandomNum(uint x)
        {
            lock (randLock)
            {
                randSeed += x;
                return randSeed;
            }
        }

        public static void UpdateRand(uint x)
        {
            lock (randLock)
            {
                randSeed += x;
            }
        }

        public static void LoadSettings()
        {
            using (StreamReader r = new StreamReader("settings.txt"))
            {
                string json = r.ReadToEnd();
                settings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
        }

        public static string  WebServerURL()
        {
            if (settings.ContainsKey("ListenURL"))
                return settings["ListenURL"];
            else
                return "http://*:8080/";
        }

        public static string FullNodeRPC()
        {
            return settings["FullNodeRPC"];
        }

        public static string FullNodeUser()
        {
            return settings["FullNodeUser"];
        }

        public static string FullNodePass()
        {
            return settings["FullNodePass"];
        }

    }
}
