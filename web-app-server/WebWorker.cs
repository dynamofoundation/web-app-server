using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Net.Http;

namespace web_app_server
{

    public class WebWorker
    {

        public HttpListenerContext context;
        static readonly HttpClient client = new HttpClient();



        public void run()
        {
            try
            {
                HttpListenerRequest request = context.Request;

                StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string text = reader.ReadToEnd();

                /*
                dynamic rpcData = JsonConvert.DeserializeObject<dynamic>(text);
                string method = rpcData.method;
                Global.UpdateRand((uint)text.Length);
                */

                string[] path = request.RawUrl.Substring(1).Split("/");

                string strResponse = getNFT(path[0], path[1]);


                HttpListenerResponse response = context.Response;

                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(strResponse);
                response.ContentLength64 = buffer.Length;
                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();

                uint sum = 0;
                for (int i = 0; i < buffer.Length; i++)
                    sum += buffer[i];

                Global.UpdateRand(sum);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in worker thread:" + e.Message);
                Console.WriteLine(e.StackTrace);

            }
        }

        public string getNFT(string assetClass, string asset)
        {
            return "hi";
        }

    }
}
