using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace web_app_server
{
    class BlockScanner
    {
        static HttpWebRequest webRequest;

        public void run()
        {


            UInt32 lastBlock;
            if (Global.useDatabase)
                lastBlock = (UInt32)Convert.ToInt32(Database.getSetting("last_block"));
            else
                lastBlock = 0;

            while (!Global.Shutdown)
            {
                UInt32 currentHeight = getCurrentHeight();
                if (lastBlock < currentHeight - 3)
                {
                    while (lastBlock < currentHeight - 3)
                    {
                        lastBlock++;
                        parseBlock(lastBlock);
                        if (lastBlock % 100 == 0)
                            Console.WriteLine("Parsing block: " + lastBlock);
                        if (Global.useDatabase)
                            Database.setSetting("last_block", lastBlock.ToString());

                    }
                }
                Thread.Sleep(5000);
            }
            
        }


        void parseBlock(UInt32 blockHeight)
        {

            dynamic dBlockResult = null;

            string filename = "blocks\\block" + blockHeight + ".dat";
            if (File.Exists(filename))
            {
                string data = File.ReadAllText(filename);
                dBlockResult = JsonConvert.DeserializeObject<dynamic>(data);
            }
            else
            {
                string blockHash = rpcExec("{\"jsonrpc\": \"1.0\", \"id\":\"1\", \"method\": \"getblockhash\", \"params\": [" + blockHeight + "] }");

                dynamic dHashResult = JsonConvert.DeserializeObject<dynamic>(blockHash)["result"];

                string block = rpcExec("{\"jsonrpc\": \"1.0\", \"id\":\"1\", \"method\": \"getblock\", \"params\": [\"" + dHashResult + "\", 2] }");

                dBlockResult = JsonConvert.DeserializeObject<dynamic>(block)["result"];

                File.WriteAllText(filename, JsonConvert.SerializeObject(dBlockResult));
            }

            uint timestamp = dBlockResult["time"];
            Global.lastBlockTimestamp = timestamp;

            foreach (var tx in dBlockResult["tx"])
            {
                string from = "";
                foreach (var vin in tx["vin"])
                {

                    if (vin.ContainsKey("coinbase"))
                        from = "coinbase";
                    else
                    {
                        string vinTXID = vin["txid"];
                        int vout = vin["vout"];
                        string key = vinTXID + vout.ToString();
                        if (Global.txList.ContainsKey(key))
                            from = Global.txList[key].address;
                        else
                            Console.WriteLine("ERROR TX vin not found " + key);
                    }

                    if (vin.ContainsKey("txid"))
                    {
                        if (Global.useDatabase)
                            Database.spendTransaction(vin["txid"].ToString(), Convert.ToInt32(vin["vout"]));
                        else 
                            Global.spendTransaction(vin["txid"].ToString(), Convert.ToInt32(vin["vout"]));
                    }

                }
                foreach (var vout in tx["vout"])
                    if (!vout["scriptPubKey"]["asm"].ToString().StartsWith("OP_RETURN"))
                    {
                        bool ok = true;
                        
                        if (!vout["scriptPubKey"].ContainsKey("address"))
                            ok = false;
                        
                        if (ok)
                        {
                            if (Global.useDatabase)
                            {
                                Database.saveTx(tx["txid"].ToString(), Convert.ToInt32(vout["n"]), Convert.ToDecimal(vout["value"]), vout["scriptPubKey"]["address"].ToString());
                                Database.updateWalletBalance(vout["scriptPubKey"]["address"].ToString(), Convert.ToDecimal(vout["value"]) * 100000000m);
                            }
                            else
                            {
                                Global.saveTx(tx["txid"].ToString(), Convert.ToInt32(vout["n"]), Convert.ToDecimal(vout["value"]), vout["scriptPubKey"]["address"].ToString());
                                Global.updateWalletBalance( vout["scriptPubKey"]["address"].ToString(), Convert.ToDecimal(vout["value"]) * 100000000m);
                                Global.addWalletHistory(from, vout["scriptPubKey"]["address"].ToString(), timestamp, Convert.ToDecimal(vout["value"]) * 100000000m);
                            }
                        }
                    }
            }

        }


        UInt32 getCurrentHeight()
        {
            string result = rpcExec("{\"jsonrpc\": \"1.0\", \"id\":\"1\", \"method\": \"getblockcount\", \"params\": [] }");

            dynamic dResult = JsonConvert.DeserializeObject<dynamic>(result)["result"];

            //UInt32 returnVal = Convert.ToInt32(dResult[0].ToString());

            return dResult;

        }

        public static bool HasProperty(dynamic obj, string name)
        {
            Type objType = obj.GetType();

            if (objType == typeof(ExpandoObject))
            {
                return ((IDictionary<string, object>)obj).ContainsKey(name);
            }

            return objType.GetProperty(name) != null;
        }

        string rpcExec(string command)
        {
            webRequest = (HttpWebRequest)WebRequest.Create(Global.FullNodeRPC());
            webRequest.KeepAlive = false;

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
    }
}
