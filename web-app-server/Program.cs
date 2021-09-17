using System;
using System.Diagnostics;
using System.Threading;

namespace web_app_server
{
    class Program
    {




        static void Main(string[] args)
        {

            uint loops = 0;

            Global.LoadSettings();

            //string result = BlockScanner.rpcExec("{\"jsonrpc\": \"1.0\", \"id\":\"1\", \"method\": \"loadwallet\", \"params\": [\"foundation-prod\"] }");

            Console.WriteLine("Starting Web server...");
            WebServer server = new WebServer();
            Thread t1 = new Thread(new ThreadStart(server.run));
            t1.Start();

            Console.WriteLine("Starting block scanner...");
            BlockScanner scanner = new BlockScanner();
            t1 = new Thread(new ThreadStart(scanner.run));
            t1.Start();

            /*
            Console.WriteLine("Starting BSC block scanner...");
            Swap swap = new Swap();
            t1 = new Thread(new ThreadStart(swap.run));
            t1.Start();
            */

            while (!Global.Shutdown)
            {
                Thread.Sleep(100);
                loops++;
                Global.UpdateRand(loops);
                if (Console.KeyAvailable)
                    Global.Shutdown = true;
            }

        }
    }
}
