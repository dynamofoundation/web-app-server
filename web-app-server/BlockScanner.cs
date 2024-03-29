﻿using Newtonsoft.Json;
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
                lastBlock = (UInt32)Convert.ToInt32(Database.getSetting("last_block"));

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
                                try
                                {
                                    parseBlock(lastBlock);
                                }
                                catch (Exception ex)
                                {
                                    Log.log("Error parsing block " + lastBlock + " " + ex.Message);
                                }
                                if (lastBlock % 100 == 0)
                                    Log.log("Parsing block: " + lastBlock);
                                Database.setSetting("last_block", lastBlock.ToString());
                            }
                        }
                    }
                    Thread.Sleep(5000);
                }
            }
            catch (Exception ex)
            {
                Log.log("Error in BlockScanner.run, exiting: " + ex.Message);
                Log.log(ex.StackTrace);
            }
            
        }


        void parseBlock(UInt32 blockHeight)
        {

            dynamic dBlockResult = null;

            string filename = "d:\\blocks\\block" + blockHeight + ".dat";
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
                        from = Global.TXListGetFromAddrByKey(vinTXID, vout);
                        if (from.Length == 0)
                            Log.log("ERROR TX vin not found " + key);
                    }

                    if (vin.ContainsKey("txid"))
                    {
                        Global.SpendTransaction(vin["txid"].ToString(), Convert.ToInt32(vin["vout"]));
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
                            Global.SaveTx(tx["txid"].ToString(), Convert.ToInt32(vout["n"]), Convert.ToDecimal(vout["value"]), vout["scriptPubKey"]["address"].ToString(), isCoinbase, (int)blockHeight);
                            Global.UpdateWalletBalance(vout["scriptPubKey"]["address"].ToString(), Convert.ToDecimal(vout["value"]) * 100000000m);
                            Global.AddWalletHistory(from, vout["scriptPubKey"]["address"].ToString(), timestamp, Convert.ToDecimal(vout["value"]) * 100000000m);
                            Swap.processWalletTX(from, vout["scriptPubKey"]["address"].ToString(), Convert.ToDecimal(vout["value"]) * 100000000m, blockHeight);
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
