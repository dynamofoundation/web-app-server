using System;
using System.Net;
using System.Threading;

namespace web_app_server
{


    public class WebServer
    {
        public void run()
        {
            if (!HttpListener.IsSupported)
            {
                Log.log("HTTP Listener not supported");
                return;
            }

            HttpListener listener = new HttpListener();

            listener.Prefixes.Add(Global.WebServerURL());

            listener.Start();
            Log.log("HTTP Listening...");

            while (!Global.Shutdown)
            {
                Global.UpdateRand(17);
                HttpListenerContext context = listener.GetContext();
                WebWorker worker = new WebWorker();
                worker.context = context;
                Thread t1 = new Thread(new ThreadStart(worker.run));
                t1.Start();
            }

            listener.Stop();
        }

    }
}
