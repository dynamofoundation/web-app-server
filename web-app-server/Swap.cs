﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace web_app_server
{


    public class Swap
    {
        static HttpWebRequest webRequest;

        public void run()
        {

            Thread t1 = new Thread(new ThreadStart(RunBSCScanner));
            t1.Start();

            Thread t2 = new Thread(new ThreadStart(ProcessSwapEvents));
            t2.Start();

            while (!Global.Shutdown)
            {

                try
                {

                    Thread.Sleep(1500);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error in BSC scan: " + e.Message);
                    Thread.Sleep(1000);
                }

                /*
                //get new BSC blocks
                //if any block contains inbound WDYN, send out DYN

                int lastBSCBlock = Convert.ToInt32(Database.getSetting("last_bsc_swap_block"));
                try
                {
                    int currentBSCBlock = getCurrentBSCBlock();

                    while (lastBSCBlock < currentBSCBlock)
                    {
                        lastBSCBlock++;
                        processBlockTransactions(lastBSCBlock);
                        Database.setSetting("last_bsc_swap_block", lastBSCBlock.ToString());
                        Console.WriteLine("Getting BSC block " + lastBSCBlock);
                        Thread.Sleep(300);
                    }
                    Thread.Sleep(1500);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error in BSC scan: " + e.Message);
                    Thread.Sleep(1000);
                }
                */
            }

        }

        public void RunBSCScanner()
        {
            /*
            while (true)
            {

                Database.log("Swap.RunBSCScanner", @"/c node js\scan_bsc.js");
                Process process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = @"/c node js\scan_bsc.js";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                //string output = process.StandardOutput.ReadToEnd();
                //Database.log("Swap.RunBSCScanner", output);
                string err = process.StandardError.ReadToEnd();
                if (err.Length > 0)
                    Database.log("Swap.RunBSCScanner", err);
                process.WaitForExit();

                Thread.Sleep(1000);
            }
            */
        }

        public void ProcessSwapEvents()
        {
            while(true)
            {
                List<Dictionary<string, string>> result = Database.GetPendingSwapEvents();

                foreach (Dictionary<string, string> row in result) {
                    dynamic data = JsonConvert.DeserializeObject(row["swap_event_data"]);
                    string from = data.returnValues.from;
                    decimal amt = Convert.ToDecimal(data.returnValues.value);

                    int id = Database.findSwapWDYNtoDYN(from, amt);
                    if (id != -1)
                    {

                        string dynAddr = Database.getSwapWDYNtoDYNDestination(id);
                        if (dynAddr.Length > 0)
                        {


                            try
                            {
                                string strUtxo = WebWorker.getUTXO("dy1qm5rf4suzfplu9dwtzkmegt763akn0qcypyut5r", (ulong)amt, false);
                                string utxoList = strUtxo.Replace("\n", "~");

                                Database.log("Swap.processBlockTransactions", @"/c node js\send_dyn.js " + dynAddr + " " + amt + " " + utxoList);
                                Process process = new Process();
                                process.StartInfo.FileName = "cmd.exe";
                                process.StartInfo.Arguments = @"/c node js\send_dyn.js " + dynAddr + " " + amt + " " + utxoList;
                                process.StartInfo.UseShellExecute = false;
                                process.StartInfo.RedirectStandardOutput = true;
                                process.StartInfo.RedirectStandardError = true;
                                process.Start();
                                //* Read the output (or the error)
                                string output = process.StandardOutput.ReadToEnd();
                                Database.log("Swap.processBlockTransactions", output);
                                string err = process.StandardError.ReadToEnd();
                                if (err.Length > 0)
                                    Database.log("Swap.processBlockTransactions", err);
                                process.WaitForExit();
                                output = output.Replace("\n", "");

                                string command = "{\"jsonrpc\": \"1.0\", \"id\":\"1\", \"method\": \"sendrawtransaction\", \"params\": [\"" + output + "\"] }";
                                Database.log("Swap.getBlockTransactions", command);
                                string strResult = BlockScanner.rpcExec(command);
                                Database.log("Swap.getBlockTransactions", strResult);
                            }
                            catch (Exception e)
                            {
                                Database.log("Swap.getBlockTransactions", "Error: " + e.Message);
                            }

                            Database.completeSwap(id);
                        }
                    }
                    else
                    {
                        Database.log("Swap.processBlockTransactions", "swap not found: " + from + ", " + amt);
                        //todo - auto refund (if not self account - would create inf loop)
                    }


                    Database.MarkSwapEventProcessed(Convert.ToInt32(row["swap_event_id"]));
                }

                Thread.Sleep(1000);
            }
        }


        /*
        public void processBlockTransactions ( int blockNum )
        {


            string strResult = bscExec("https://api.bscscan.com/api?module=account&action=tokentx&address=0x32f626a088b49bd5ffa3895fece48800f30fc5d3&startblock=" + blockNum + "&endblock=" + blockNum + "&sort=asc&apikey=295MP1ZP6N17C5IYU4APMHJKFPRU97S4C2&contractAddress=0x7e1739A437666AdB28dd30bC8B5E182C5c5953D5");

            dynamic jRPCResult = JObject.Parse(strResult);

            int count = jRPCResult.result.Count;

            if (count > 0)
            {
                Database.log("Swap.getBlockTransactions", strResult);
                foreach (JObject o in jRPCResult.result)
                {
                    if (o["to"].ToString() == "0x32f626a088b49bd5ffa3895fece48800f30fc5d3")
                    {
                        string from = o["from"].ToString();
                        decimal amt = Convert.ToDecimal(o["value"].ToString());

                        int id = Database.findSwapWDYNtoDYN(from, amt);
                        if (id != -1)
                        {

                            string dynAddr = Database.getSwapWDYNtoDYNDestination(id);
                            if (dynAddr.Length > 0)
                            {


                                try
                                {
                                    string strUtxo = WebWorker.getUTXO("dy1qm5rf4suzfplu9dwtzkmegt763akn0qcypyut5r", (ulong)amt, false);
                                    string utxoList = strUtxo.Replace("\n", "~");

                                    Database.log("Swap.processBlockTransactions", @"/c node js\send_dyn.js " + dynAddr + " " + amt + " " + utxoList);
                                    Process process = new Process();
                                    process.StartInfo.FileName = "cmd.exe";
                                    process.StartInfo.Arguments = @"/c node js\send_dyn.js " + dynAddr + " " + amt + " " + utxoList;
                                    process.StartInfo.UseShellExecute = false;
                                    process.StartInfo.RedirectStandardOutput = true;
                                    process.StartInfo.RedirectStandardError = true;
                                    process.Start();
                                    //* Read the output (or the error)
                                    string output = process.StandardOutput.ReadToEnd();
                                    Database.log("Swap.processBlockTransactions", output);
                                    string err = process.StandardError.ReadToEnd();
                                    if (err.Length > 0)
                                        Database.log("Swap.processBlockTransactions", err);
                                    process.WaitForExit();
                                    output = output.Replace("\n", "");

                                    string command = "{\"jsonrpc\": \"1.0\", \"id\":\"1\", \"method\": \"sendrawtransaction\", \"params\": [\"" + output + "\"] }";
                                    Database.log("Swap.getBlockTransactions", command);
                                    string result = BlockScanner.rpcExec(command);
                                    Database.log("Swap.getBlockTransactions", result);
                                }
                                catch (Exception e)
                                {
                                    Database.log("Swap.getBlockTransactions", "Error: " + e.Message);
                                }

                                Database.completeSwap(id);
                            }
                        }
                        else
                        {
                            Database.log("Swap.processBlockTransactions", "swap not found: " + from + ", " + amt);
                            //todo - auto refund (if not self account - would create inf loop)
                        }

                    }
                }
            }

        }

        public int getCurrentBSCBlock()
        {

            long tick = DateTimeOffset.Now.ToUnixTimeSeconds() - 10;

            string strResult = bscExec("https://api.bscscan.com/api?module=block&action=getblocknobytime&timestamp=" + tick + "&closest=before&apikey=" + Global.BSCScanAPIKey());

            dynamic jRPCResult = JObject.Parse(strResult);
            string result = jRPCResult.result;


            return Convert.ToInt32(result);

        }

        public string bscExec(string command)
        {
            webRequest = (HttpWebRequest)WebRequest.Create(command);
            webRequest.KeepAlive = false;
            webRequest.Timeout = 300000;

            var data = Encoding.ASCII.GetBytes(command);

            webRequest.Method = "GET";

            var webresponse = (HttpWebResponse)webRequest.GetResponse();

            string submitResponse = new StreamReader(webresponse.GetResponseStream()).ReadToEnd();

            webresponse.Dispose();

            return submitResponse;
        }
        */

        public static void processWalletTX (string from, string to, decimal amt, uint blockHeight)
        {
            //if any block contains inbound DYN, send out WDYN and match off swap

            if (!Global.SwapEnabled())
                return;

            if (blockHeight > Convert.ToUInt32(Database.getSetting("last_dyn_swap_block")))
            {
                if (to == "dy1qm5rf4suzfplu9dwtzkmegt763akn0qcypyut5r")
                {
                    int id = Database.findSwapDYNtoWDYN(from, amt);
                    if (id != -1)
                    {

                        string bscAddr = Database.getSwapDYNtoWDYNDestination(id);
                        if (bscAddr.Length > 0)
                        {

                            SendBEP sb = new SendBEP();
                            sb.amt = amt;
                            sb.bscAddr = bscAddr;
                            sb.id = id;
                            Thread t1 = new Thread(new ThreadStart(sb.send));
                            t1.Start();

                            /*
                            Database.log("Swap.processWalletTX", @"/c node js\send_bep20.js " + bscAddr + " " + amt);
                            Process process = new Process();
                            process.StartInfo.FileName = "cmd.exe";
                            process.StartInfo.Arguments = @"/c node js\send_bep20.js " + bscAddr + " " + amt;
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.Start();
                            string output = process.StandardOutput.ReadToEnd();
                            Database.log("Swap.processWalletTX", output);
                            string err = process.StandardError.ReadToEnd();
                            if (err.Length > 0)
                                Database.log("Swap.processWalletTX", err);
                            process.WaitForExit();

                            Database.completeSwap(id);
                            */

                        }
                    }
                    else
                    {
                        Database.log("Swap.processWalletTX", "swap not found: " + from + ", " + amt);
                        //todo - auto refund (if not t5r account - would create inf loop)
                    }
                }
            }

        }


    }
}
