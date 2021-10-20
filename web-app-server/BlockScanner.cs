using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
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

            try
            {
                UInt32 lastBlock;
                if (Global.useDatabase)
                    lastBlock = (UInt32)Convert.ToInt32(Database.getSetting("last_block"));
                else
                {
                    if (File.Exists("last_checkpoint.txt"))
                    {
                        lastBlock = Convert.ToUInt32(File.ReadAllText("last_checkpoint.txt"));
                        //lastBlock = Convert.ToUInt32(Database.getSetting("last_dyn_checkpoint"));
                    }
                    else
                    {
                        lastBlock = 0;
                        File.WriteAllText("last_checkpoint.txt", "0");
                    }

                    if (!File.Exists("wallet.dat"))
                    {
                        Global.walletList = new Dictionary<string, Global.Wallet>();
                        Global.txList = new Dictionary<string, Global.TX>();
                        writeDATFiles();
                    }

                    byte[] _ByteArray = File.ReadAllBytes("wallet.dat");
                    System.IO.MemoryStream _MemoryStream = new System.IO.MemoryStream(_ByteArray);
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter _BinaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    Global.walletList = (Dictionary<string, Global.Wallet>)_BinaryFormatter.Deserialize(_MemoryStream);

                    Global.clearAllPendingSpend();


                    _ByteArray = File.ReadAllBytes("tx.dat");
                    _MemoryStream = new System.IO.MemoryStream(_ByteArray);
                    _BinaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    Global.txList = (Dictionary<string, Global.TX>)_BinaryFormatter.Deserialize(_MemoryStream);

                    _MemoryStream.Close();
                    _MemoryStream.Dispose();
                    _MemoryStream = null;
                    _ByteArray = null;
                }

                while (!Global.Shutdown)
                {
                    int currentHeight = getCurrentHeight();
                    if (currentHeight != -1)
                    {
                        if (Global.Verbose()) Log.log("currentHeight: " + currentHeight);
                        Global.currentBlockHeight = (int)currentHeight;
                        if (lastBlock < currentHeight - 3)
                        {
                            while ((lastBlock < currentHeight - 3) && (!Global.Shutdown))
                            {
                                lastBlock++;
                                if (Global.Verbose()) Log.log("lastBock: " + lastBlock);
                                parseBlock(lastBlock);
                                if (lastBlock % 100 == 0)
                                    Log.log("Parsing block: " + lastBlock);
                                if (Global.useDatabase)
                                    Database.setSetting("last_block", lastBlock.ToString());
                                if (lastBlock % 5000 == 0)
                                    if (lastBlock > Convert.ToUInt32(File.ReadAllText("last_checkpoint.txt")))
                                    {
                                        if (Global.Verbose()) Log.log("Writing DAT file");
                                        writeDATFiles();

                                        File.WriteAllText("last_checkpoint.txt", lastBlock.ToString());
                                        //Database.setSetting("last_dyn_checkpoint", lastBlock.ToString());
                                    }

                            }
                        }
                    }
                    Thread.Sleep(5000);
                }
            }
            catch (Exception ex)
            {
                Log.log("Error in BlockScanner.run, exiting: " + ex.Message);
            }
            
        }


        void writeDATFiles()
        {
            System.IO.MemoryStream _MemoryStream = new System.IO.MemoryStream();
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter _BinaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            _BinaryFormatter.Serialize(_MemoryStream, Global.walletList);
            byte[] _ByteArray = _MemoryStream.ToArray();
            System.IO.FileStream _FileStream = new System.IO.FileStream("wallet.dat", System.IO.FileMode.Create, System.IO.FileAccess.Write);
            _FileStream.Write(_ByteArray.ToArray(), 0, _ByteArray.Length);
            _FileStream.Close();
            _MemoryStream.Close();
            _MemoryStream.Dispose();
            _MemoryStream = null;
            _ByteArray = null;

            _MemoryStream = new System.IO.MemoryStream();
            _BinaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            _BinaryFormatter.Serialize(_MemoryStream, Global.txList);
            _ByteArray = _MemoryStream.ToArray();
            _FileStream = new System.IO.FileStream("tx.dat", System.IO.FileMode.Create, System.IO.FileAccess.Write);
            _FileStream.Write(_ByteArray.ToArray(), 0, _ByteArray.Length);
            _FileStream.Close();
            _MemoryStream.Close();
            _MemoryStream.Dispose();
            _MemoryStream = null;
            _ByteArray = null;
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
                string blockHash = BlockScanner.rpcExec("{\"jsonrpc\": \"1.0\", \"id\":\"1\", \"method\": \"getblockhash\", \"params\": [" + blockHeight + "] }");

                dynamic dHashResult = JsonConvert.DeserializeObject<dynamic>(blockHash)["result"];

                string block = BlockScanner.rpcExec("{\"jsonrpc\": \"1.0\", \"id\":\"1\", \"method\": \"getblock\", \"params\": [\"" + dHashResult + "\", 2] }");

                dBlockResult = JsonConvert.DeserializeObject<dynamic>(block)["result"];

                File.WriteAllText(filename, JsonConvert.SerializeObject(dBlockResult));
            }

            uint timestamp = dBlockResult["time"];
            Global.lastBlockTimestamp = timestamp;

            bool isCoinbase = true;
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
                            Log.log("ERROR TX vin not found " + key);
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
                {
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
                                //bool isCoinbase = Convert.ToInt32(vout["n"]) < 3;
                                Global.saveTx(tx["txid"].ToString(), Convert.ToInt32(vout["n"]), Convert.ToDecimal(vout["value"]), vout["scriptPubKey"]["address"].ToString(), isCoinbase, (int)blockHeight);
                                Global.updateWalletBalance(vout["scriptPubKey"]["address"].ToString(), Convert.ToDecimal(vout["value"]) * 100000000m);
                                Global.addWalletHistory(from, vout["scriptPubKey"]["address"].ToString(), timestamp, Convert.ToDecimal(vout["value"]) * 100000000m);
                                Swap.processWalletTX(from, vout["scriptPubKey"]["address"].ToString(), Convert.ToDecimal(vout["value"]) * 100000000m, blockHeight);
                            }
                        }
                    }
                }
                isCoinbase = false; //first transaction only 
            }

            if (Global.SwapEnabled())
                if (blockHeight > Convert.ToUInt32(Database.getSetting("last_dyn_swap_block")))
                    Database.setSetting("last_dyn_swap_block", blockHeight.ToString());

        }


        int getCurrentHeight()
        {
            string result = BlockScanner.rpcExec("{\"jsonrpc\": \"1.0\", \"id\":\"1\", \"method\": \"getblockcount\", \"params\": [] }");

            Log.log("get block count result: " + result);

            int iResult = -1;

            try
            {
                dynamic dResult = JsonConvert.DeserializeObject<dynamic>(result)["result"];
                iResult = dResult;
            }
            catch (Exception ex ) {
            }


            return iResult;

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

        public static string rpcExec(string command)
        {
            string submitResponse = "";

            try
            {
                lock (Global.rpcExecLoc)
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

                    submitResponse = new StreamReader(webresponse.GetResponseStream()).ReadToEnd();

                    webresponse.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.log("BlockScanner.rpcExec error: " + ex.Message);
                submitResponse = "Error: " + ex.Message;
            }


            return submitResponse;
        }
    }
}
