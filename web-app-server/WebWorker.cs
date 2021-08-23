using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace web_app_server
{

    public class WebWorker
    {

        public HttpListenerContext context;
        static readonly HttpClient client = new HttpClient();

        static HttpWebRequest webRequest;


        public void run()
        {
            try
            {
                HttpListenerRequest request = context.Request;

                StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string text = reader.ReadToEnd();

                string[] path = request.RawUrl.Substring(1).Split("/");

                Console.WriteLine(request.RawUrl);

                //array to store the response in
                byte[] binaryData = Encoding.ASCII.GetBytes("Internal error.");

                //TODO - clean up these program logic paths

                bool processedAPI = false;
                if (path[0].StartsWith("get_balance"))
                {
                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string address = args["addr"];

                    lock (Global.walletList)
                    {
                        if (Global.walletList.ContainsKey(address))
                            binaryData = Encoding.ASCII.GetBytes(Global.walletList[address].balance.ToString("0"));
                        else
                            binaryData = Encoding.ASCII.GetBytes("0");
                        processedAPI = true;
                    }
                }

                else if (path[0].StartsWith("get_utxo"))
                {
                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string address = args["addr"];
                    UInt64 targetAmount = Convert.ToUInt64(args["amount"]);

                    string result = "";

                    lock (Global.walletList)
                    {
                        if (Global.walletList.ContainsKey(address))
                        {
                            UInt64 total = 0;
                            int ptr = 0;

                            foreach (Global.UTXO utxo in Global.walletList[address].utxo.Values)
                            {
                                result += utxo.hash + "," + utxo.vout + "," + utxo.amount.ToString("0") + "\n";
                                total += Convert.ToUInt64(utxo.amount);
                                ptr++;
                                if (ptr >= 500)
                                {
                                    result = "Too many inputs.";
                                    break;
                                }
                                if (total >= targetAmount)
                                    break;
                            }

                            binaryData = Encoding.ASCII.GetBytes(result);
                        }
                        processedAPI = true;
                    }
                }

                else if (path[0].StartsWith("get_transactions"))
                {
                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string address = args["addr"];
                    int start = Convert.ToInt32(args["start"]);

                    string result = "";

                    lock (Global.walletList)
                    {
                        if (Global.walletList.ContainsKey(address))
                        {

                            result = result + Global.lastBlock.ToString() + "\n";
                            result = result + Global.lastBlockTimestamp.ToString() + "\n";

                            int num = 0;
                            int ptr = start;

                            while ((num < 10) && (ptr < Global.walletList[address].history.Count))
                            {
                                string action = "";
                                if (Global.walletList[address].history[ptr].from == "coinbase")
                                    action = "Mine";
                                else
                                {
                                    if (Global.walletList[address].history[ptr].amount < 0)
                                        action = "Send";
                                    else
                                        action = "Recv";
                                }
                                result += Global.walletList[address].history[ptr].timestamp + "," +
                                    action + "," +
                                    Global.walletList[address].history[ptr].to + "," +
                                    Global.walletList[address].history[ptr].amount.ToString("0") + "\n";
                                ptr++;
                                num++;
                            }

                            binaryData = Encoding.ASCII.GetBytes(result);
                        }

                        processedAPI = true;
                    }
                }

                else if (path[0].StartsWith("send_tx"))
                {
                    string[] args = text.Split("=");
                    string hex = args[1];

                    string result = "error";

                    string command = "{ \"id\": 0, \"method\" : \"sendrawtransaction\", \"params\" : [ \"" + hex + "\" ] }";

                    try
                    {
                        string rpcResult = rpcExec(command);
                        dynamic jRPCResult = JObject.Parse(rpcResult);
                        result = jRPCResult.result;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                    }

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;
                }

                else if (path[0].StartsWith("get_nft_asset_class_list"))
                {

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string addr = args["addr"];

                    string result = "error";

                    string command = "{ \"id\": 0, \"method\" : \"listnft\", \"params\" : [ \"list-class\", \"" + addr + "\", 0 ] }";

                    try
                    {
                        string rpcResult = rpcExec(command);
                        dynamic jRPCResult = JObject.Parse(rpcResult);
                        result = jRPCResult.result;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                    }

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }

                else if (path[0].StartsWith("get_nft_asset_class"))
                {

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string classHash = args["hash"];

                    string result = "error";

                    string command = "{ \"id\": 0, \"method\" : \"getnft\", \"params\" : [ \"get-class\", \"" + classHash + "\" ] }";

                    try
                    {
                        string rpcResult = rpcExec(command);
                        dynamic jRPCResult = JObject.Parse(rpcResult);
                        result = jRPCResult.result;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                    }

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }

                else if (path[0].StartsWith("submit_nft"))
                {

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string nftcommand = args["command"];
                    string ownerAddr = args["ownerAddr"];
                    string txID = args["txID"];
                    string assetClassID = "";
                    if (args.ContainsKey("assetClass"))
                        assetClassID = args["assetClass"];

                    string[] body = text.Split("=");
                    string nftRawData = body[1];


                    string result = "error";

                    string command = "{ \"id\": 0, \"method\" : \"submitnft\", \"params\" : [ \"" + nftcommand + "\", \"" + nftRawData + "\", \"" + ownerAddr + "\", \"" + txID + "\", \"" + assetClassID +"\" ] }";


                    try
                    {
                        string rpcResult = rpcExec(command);
                        dynamic jRPCResult = JObject.Parse(rpcResult);
                        result = jRPCResult.result;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                    }

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }


                bool valid = true;
                if (!processedAPI)
                {
                    if (path[0].Length != 64)
                    {
                        binaryData = Encoding.ASCII.GetBytes("Invalid hash format, must be 64 hexadecimal characters.");
                        valid = false;
                    }

                    if (!ValidHex(path[0]))
                    {
                        binaryData = Encoding.ASCII.GetBytes("Invalid hash format, must be 64 hexadecimal characters.");
                        valid = false;
                    }
                }
                else
                    valid = false;

                if (valid)
                {
                    //if the hash is a webpack and it's loaded, then respond with the page from cache
                    if (Global.webPacks.ContainsKey(path[0]))
                    {
                        if ((path.Length > 1) && (path[1].Length > 0))
                        {
                            string fullPath = "";
                            for (int i = 1; i < path.Length - 1; i++)
                                fullPath = fullPath + path[i] + "\\";
                            fullPath = fullPath + path[path.Length - 1];
                            binaryData = Global.GetWebPackPage(path[0], fullPath);
                        }
                        else
                        {
                            binaryData = Global.GetWebPackPage(path[0], Global.webPacks[path[0]].indexPage);
                        }

                    }
                    else
                    {

                        string strResponse = GetAsset(path[0]);
                        if (strResponse == "error-pending-request")
                        {
                            binaryData = Encoding.ASCII.GetBytes("That asset is not available on the server and has been requested from the network.  Please try again in a few minutes");
                        }
                        else
                        {

                            byte[] binaryResponse = HexToByte(strResponse);

                            string assetHash;
                            string assetClassHash;
                            string metaData;
                            string owner;
                            string txnID;

                            int offset = 0;
                            assetHash = ReadString(binaryResponse, ref offset);
                            assetClassHash = ReadString(binaryResponse, ref offset);
                            metaData = ReadString(binaryResponse, ref offset);
                            binaryData = ReadVector(binaryResponse, ref offset);
                            owner = ReadString(binaryResponse, ref offset);
                            txnID = ReadString(binaryResponse, ref offset);

                            bool webpack = false;
                            try
                            {
                                dynamic jMeta = JsonConvert.DeserializeObject(metaData);
                                if ((jMeta.webpack_version == 1) && (jMeta.index_file.ToString().Length > 0))
                                    webpack = true;
                            }
                            catch (Exception ex)
                            {

                            }

                            if (webpack)
                            {
                                dynamic jMeta = JsonConvert.DeserializeObject(metaData);
                                Global.LoadWebPack(assetHash, binaryData, jMeta.index_file.ToString());
                                binaryData = Global.GetWebPackPage(path[0], Global.webPacks[path[0]].indexPage);

                            }
                        }
                    }
                }


                HttpListenerResponse response = context.Response;

                System.IO.Stream output = response.OutputStream;
                output.Write(binaryData, 0, binaryData.Length);
                output.Close();

                uint sum = 0;
                for (int i = 0; i < binaryData.Length; i++)
                    sum += binaryData[i];

                Global.UpdateRand(sum);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in worker thread:" + e.Message);
                Console.WriteLine(e.StackTrace);

            }
        }


        public Dictionary<string,string> ParseArgs (string data)
        {
            Dictionary<string, string> args = new Dictionary<string, string>();
            string[] arguments = data.Substring(1).Split('&');
            for (int i = 0; i < arguments.Length; i++)
            {
                string[] param = arguments[i].Split("=");
                args.Add(param[0], param[1]);
            }

            return args;
        }

        public string ReadString ( byte[] data, ref int offset)
        {
            string result = "";
            int len = (data[offset] << 24) + (data[offset + 1] << 16) + (data[offset + 2] << 8) + (data[offset + 3]);
            for (int i = 0; i < len; i++)
                result += Convert.ToChar(data[offset + i + 4]);
            offset += len + 4;
            return result;
        }

        public byte[] ReadVector(byte[] data, ref int offset)
        {
            byte[] result;
            int len = (data[offset] << 24) + (data[offset + 1] << 16) + (data[offset + 2] << 8) + (data[offset + 3]);
            result = new byte[len];
            System.Array.Copy(data, offset + 4, result, 0, len);
            offset += len + 4;
            return result;
        }


        public static string GetAsset(string hash)
        {
            string result = "error";

            string command = "get-asset";
            string getcommand = "{ \"id\": 0, \"method\" : \"getnft\", \"params\" : [ \"" + command + "\", \"" + hash + "\" ] }";

            try
            {
                string rpcResult = rpcExec(getcommand);
                dynamic jRPCResult = JObject.Parse(rpcResult);
                result = jRPCResult.result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }

            return result;
        }


        public byte[] HexToByte(string data)
        {
            data = data.ToUpper();
            byte[] result = new byte[data.Length / 2];
            for (int i = 0; i < data.Length; i += 2)
            {
                byte hi = hex(data[i]);
                byte lo = hex(data[i + 1]);
                result[i / 2] = (byte)(hi * 16 + lo);
            }

            return result;
        }


        public static string rpcExec(string command)
        {
            webRequest = (HttpWebRequest)WebRequest.Create(Global.FullNodeRPC());
            webRequest.KeepAlive = false;
            webRequest.Timeout = 300000;

            var data = Encoding.ASCII.GetBytes(command);

            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.ContentLength = data.Length;

            var username = Global.FullNodeUser();
            var password = Global.FullNodePass();
            string encoded = System.Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
            webRequest.Headers.Add("Authorization", "Basic " + encoded);


            using (var stream = webRequest.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }


            var webresponse = (HttpWebResponse)webRequest.GetResponse();

            string submitResponse = new StreamReader(webresponse.GetResponseStream()).ReadToEnd();

            webresponse.Dispose();


            return submitResponse;
        }


        public bool ValidHex (string data)
        {
            data = data.ToLower();

            bool result = true;
            int i = 0;
            while ((i < data.Length) && (result))
            {
                result = (((data[i] >= '0') && (data[i] <= '9')) || ((data[i] >= 'a') && (data[i] <= 'f')));
                if (result)
                    i++;
            }
            return result;
        }



        public byte hex(char data)
        {

            if (data < 'A')
                return (byte)(data - '0');
            else
                return (byte)((data - 'A') + 10);
        }


    }
}
