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

                Log.log(request.RawUrl);

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
                    bool sendMax = false;
                    if (args.ContainsKey("max"))
                        sendMax = true;

                    string result = WebWorker.getUTXO(address, targetAmount, sendMax);
                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;
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
                        Log.log(e.Message);
                        Log.log(e.StackTrace);
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
                        Log.log(e.Message);
                        Log.log(e.StackTrace);
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
                        Log.log(e.Message);
                        Log.log(e.StackTrace);
                    }

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }

                else if (path[0].StartsWith("get_nft_asset_list_by_owner"))
                {

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string addr = args["addr"];


                    string result = "error";

                    /*
                    string command = "{ \"id\": 0, \"method\" : \"getnft\", \"params\" : [ \"get-class\", \"" + classHash + "\" ] }";

                    try
                    {
                        string rpcResult = rpcExec(command);
                        dynamic jRPCResult = JObject.Parse(rpcResult);
                        result = jRPCResult.result;
                    }
                    catch (Exception e)
                    {
                        Log.log(e.Message);
                        Log.log(e.StackTrace);
                    }
                    */

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }

                else if (path[0].StartsWith("nchw_create_nft_asset_class"))
                {

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string passphrase = args["passphrase"];
                    string from_addr = args["from_addr"];
                    string metadata = WebUtility.UrlDecode(args["metadata"]);
                    string maxcount = args["maxcount"];

                    Dictionary<string, string> data = Database.ReadNCHW(from_addr);
                    string pw_hash = data["nchw_password_hash"];
                    string enc_wallet = data["nchw_encrypted_wallet"];
                    string iv = data["nchw_iv"];

                    string[] pw_split = pw_hash.Split(".");
                    string HashedPassword = Global.CreateHash(passphrase, pw_split[0]);

                    string decryptedData = "";
                    string xprv = "";

                    string result = "error";


                    if (pw_hash == HashedPassword)
                    {
                        SymmetricAlgorithm crypt = Aes.Create();
                        HashAlgorithm hash = SHA256.Create();
                        crypt.KeySize = 256;
                        crypt.Mode = CipherMode.CBC;
                        crypt.Key = hash.ComputeHash(Encoding.UTF8.GetBytes(passphrase));
                        crypt.IV = Global.HexToByteArray(iv);

                        byte[] encData = Global.HexToByteArray(enc_wallet);


                        using (MemoryStream ms = new MemoryStream(encData))
                        {
                            using (CryptoStream csDecrypt = new CryptoStream(ms, crypt.CreateDecryptor(), CryptoStreamMode.Read))
                            {
                                csDecrypt.Read(encData, 0, encData.Length);
                            }
                            byte[] bDecrypted = ms.ToArray();
                            decryptedData = System.Text.Encoding.UTF8.GetString(bDecrypted);
                        }

                    }
                    else result = "Incorrect password";

                    if (decryptedData.Length > 0)
                    {
                        string[] sResult = decryptedData.Split(",");
                        xprv = sResult[1];

                        decimal dAmt = 10000m;

                        ulong lAmt = ((ulong)dAmt);
                        string utxo = getUTXO(from_addr, lAmt, false).Replace("\n", "~");

                        if (!utxo.StartsWith("ERROR"))
                        {

                            string opdata = "37373730";

                            string assetClassMetaData = metadata;
                            string ownerAddress = from_addr;

                            int metaDataLen = assetClassMetaData.Length;

                            SHA256 hasher = SHA256.Create();

                            long nftHashLen = metaDataLen + 2 + 8;     //2 bytes for length of metadata, 8 bytes for 64 bit serial
                            byte[] nftRawData = new byte[nftHashLen];

                            nftRawData[0] = (byte)(metaDataLen >> 8);
                            nftRawData[1] = (byte)(metaDataLen & 0xFF);

                            byte[] metaDataBytes = System.Text.Encoding.UTF8.GetBytes(assetClassMetaData);
                            for (int i = 0; i < metaDataLen; i++)
                                nftRawData[i + 2] = metaDataBytes[i];

                            UInt64 maxSerial = Convert.ToUInt64(maxcount);
                            nftRawData[metaDataLen + 2] = (byte)(maxSerial >> 56);
                            nftRawData[metaDataLen + 2 + 1] = (byte)((maxSerial & 0x00FF000000000000) >> 48);
                            nftRawData[metaDataLen + 2 + 2] = (byte)((maxSerial & 0x0000FF0000000000) >> 40);
                            nftRawData[metaDataLen + 2 + 3] = (byte)((maxSerial & 0x000000FF00000000) >> 32);
                            nftRawData[metaDataLen + 2 + 4] = (byte)((maxSerial & 0x00000000FF000000) >> 24);
                            nftRawData[metaDataLen + 2 + 5] = (byte)((maxSerial & 0x0000000000FF0000) >> 16);
                            nftRawData[metaDataLen + 2 + 6] = (byte)((maxSerial & 0x000000000000FF00) >> 8);
                            nftRawData[metaDataLen + 2 + 7] = (byte)((maxSerial & 0x00000000000000FF));

                            string hexNFTRawData = ByteToHex(nftRawData);

                            byte[] hash = hasher.ComputeHash(nftRawData);

                            string strHash = ByteToHex(hash);

                            opdata += strHash;

                            string strTransaction = Global.CreateRawTransactionNFT(from_addr, utxo, lAmt, xprv, opdata).Replace("\n", "");

                            string command = "{ \"id\": 0, \"method\" : \"sendrawtransaction\", \"params\" : [ \"" + strTransaction + "\" ] }";

                            try
                            {
                                string rpcResult = rpcExec(command);
                                dynamic jRPCResult = JObject.Parse(rpcResult);
                                string txID = jRPCResult.result;

                                byte[] byteOwnerAddr = System.Text.Encoding.UTF8.GetBytes(ownerAddress);
                                byte[] byteTXID = HexToByte(txID);

                                hasher.Initialize();
                                byte[] bHash1Buffer = new byte[hash.Length + byteOwnerAddr.Length];
                                System.Buffer.BlockCopy(hash, 0, bHash1Buffer, 0, hash.Length);
                                System.Buffer.BlockCopy(byteOwnerAddr, 0, bHash1Buffer, hash.Length, byteOwnerAddr.Length);
                                byte[] hash1 = hasher.ComputeHash(bHash1Buffer);

                                hasher.Initialize();
                                byte[] bHash2Buffer = new byte[hash1.Length + byteTXID.Length];
                                System.Buffer.BlockCopy(hash1, 0, bHash2Buffer, 0, hash1.Length);
                                System.Buffer.BlockCopy(byteTXID, 0, bHash2Buffer, hash1.Length, byteTXID.Length);
                                byte[] hash2 = hasher.ComputeHash(bHash2Buffer);

                                string nftHash = ByteToHex(hash2);


                                command = "add-class";

                                string rpcAddAssetClass = "{ \"id\": 0, \"method\" : \"submitnft\", \"params\" : [ \"" + command + "\", \"" + hexNFTRawData + "\", \"" + ownerAddress + "\", \"" + txID + "\", \"\" ] }";

                                rpcResult = rpcExec(rpcAddAssetClass);
                                jRPCResult = JObject.Parse(rpcResult);
                                string nftHashVerify = jRPCResult.result;

                                if (nftHash != nftHashVerify)
                                {
                                    result = "NFT hash mismatch";
                                }
                                else
                                    result = nftHash;

                            }
                            catch (Exception e)
                            {
                                Log.log(e.Message);
                                Log.log(e.StackTrace);
                                result = "Error";
                            }

                        }
                        else
                            result = utxo;

                    }


                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }


                else if (path[0].StartsWith("nchw_create_nft_asset"))
                {

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string passphrase = args["passphrase"];
                    string from_addr = args["addr"];
                    string metadata = WebUtility.UrlDecode(args["metadata"]);
                    UInt64 serial = Convert.ToUInt64(args["serial"]);
                    string classhash = args["classhash"];
                    string binary = text;

                    string result = "error";


                    Dictionary<string, string> data = Database.ReadNCHW(from_addr);
                    string pw_hash = data["nchw_password_hash"];
                    string enc_wallet = data["nchw_encrypted_wallet"];
                    string iv = data["nchw_iv"];

                    string[] pw_split = pw_hash.Split(".");
                    string HashedPassword = Global.CreateHash(passphrase, pw_split[0]);

                    string decryptedData = "";
                    string xprv = "";



                    if (pw_hash == HashedPassword)
                    {
                        SymmetricAlgorithm crypt = Aes.Create();
                        HashAlgorithm hash = SHA256.Create();
                        crypt.KeySize = 256;
                        crypt.Mode = CipherMode.CBC;
                        crypt.Key = hash.ComputeHash(Encoding.UTF8.GetBytes(passphrase));
                        crypt.IV = Global.HexToByteArray(iv);

                        byte[] encData = Global.HexToByteArray(enc_wallet);


                        using (MemoryStream ms = new MemoryStream(encData))
                        {
                            using (CryptoStream csDecrypt = new CryptoStream(ms, crypt.CreateDecryptor(), CryptoStreamMode.Read))
                            {
                                csDecrypt.Read(encData, 0, encData.Length);
                            }
                            byte[] bDecrypted = ms.ToArray();
                            decryptedData = System.Text.Encoding.UTF8.GetString(bDecrypted);
                        }

                    }
                    else result = "Incorrect password";

                    if (decryptedData.Length > 0)
                    {
                        string[] sResult = decryptedData.Split(",");
                        xprv = sResult[1];

                        decimal dAmt = 10000m;

                        ulong lAmt = ((ulong)dAmt);
                        string utxo = getUTXO(from_addr, lAmt, false).Replace("\n", "~");

                        if (!utxo.StartsWith("ERROR"))
                        {
                            string opdata = "37373731";

                            string assetClassMetaData = metadata;
                            string ownerAddress = from_addr;

                            int metaDataLen = assetClassMetaData.Length;

                            byte[] binaryNFTData = HexToByte(binary);

                            SHA256 hasher = SHA256.Create();

                            //2 bytes for length of metadata, 8 bytes for 64 bit serial, 3 bytes for binary data length
                            long nftHashLen = metaDataLen + 2 + 8 + binaryNFTData.Length + 3;
                            byte[] nftRawData = new byte[nftHashLen];

                            nftRawData[0] = (byte)(metaDataLen >> 8);
                            nftRawData[1] = (byte)(metaDataLen & 0xFF);

                            byte[] metaDataBytes = System.Text.Encoding.UTF8.GetBytes(assetClassMetaData);
                            for (int i = 0; i < metaDataLen; i++)
                                nftRawData[i + 2] = metaDataBytes[i];

                            nftRawData[metaDataLen + 2] = (byte)(binaryNFTData.Length >> 16);
                            nftRawData[metaDataLen + 2 + 1] = (byte)((binaryNFTData.Length & 0x00FF00) >> 8);
                            nftRawData[metaDataLen + 2 + 2] = (byte)((binaryNFTData.Length & 0x0000FF));

                            for (int i = 0; i < binaryNFTData.Length; i++)
                                nftRawData[metaDataLen + 2 + 3 + i] = binaryNFTData[i];

                            int offset = metaDataLen + 2 + 3 + binaryNFTData.Length;
                            nftRawData[offset] = (byte)(serial >> 56);
                            nftRawData[offset + 1] = (byte)((serial & 0x00FF000000000000) >> 48);
                            nftRawData[offset + 2] = (byte)((serial & 0x0000FF0000000000) >> 40);
                            nftRawData[offset + 3] = (byte)((serial & 0x000000FF00000000) >> 32);
                            nftRawData[offset + 4] = (byte)((serial & 0x00000000FF000000) >> 24);
                            nftRawData[offset + 5] = (byte)((serial & 0x0000000000FF0000) >> 16);
                            nftRawData[offset + 6] = (byte)((serial & 0x000000000000FF00) >> 8);
                            nftRawData[offset + 7] = (byte)((serial & 0x00000000000000FF));

                            string hexNFTRawData = ByteToHex(nftRawData);

                            byte[] bAssetClassID = HexToByte(classhash);
                            byte[] dataToHash = new byte[nftRawData.Length + bAssetClassID.Length];
                            System.Array.Copy(nftRawData, dataToHash, nftRawData.Length);
                            System.Array.Copy(bAssetClassID, 0, dataToHash, nftRawData.Length, bAssetClassID.Length);

                            byte[] hash = hasher.ComputeHash(dataToHash);

                            string strHash = ByteToHex(hash);

                            opdata += strHash;

                            string strTransaction = Global.CreateRawTransactionNFT(from_addr, utxo, lAmt, xprv, opdata).Replace("\n", "");

                            string command = "{ \"id\": 0, \"method\" : \"sendrawtransaction\", \"params\" : [ \"" + strTransaction + "\" ] }";

                            string rpcResult = rpcExec(command);
                            dynamic jRPCResult = JObject.Parse(rpcResult);
                            string txID = jRPCResult.result;

                            byte[] byteOwnerAddr = System.Text.Encoding.UTF8.GetBytes(ownerAddress);
                            byte[] byteTXID = HexToByte(txID);

                            hasher.Initialize();
                            byte[] bHash1Buffer = new byte[hash.Length + byteOwnerAddr.Length];
                            System.Buffer.BlockCopy(hash, 0, bHash1Buffer, 0, hash.Length);
                            System.Buffer.BlockCopy(byteOwnerAddr, 0, bHash1Buffer, hash.Length, byteOwnerAddr.Length);
                            byte[] hash1 = hasher.ComputeHash(bHash1Buffer);

                            hasher.Initialize();
                            byte[] bHash2Buffer = new byte[hash1.Length + byteTXID.Length];
                            System.Buffer.BlockCopy(hash1, 0, bHash2Buffer, 0, hash1.Length);
                            System.Buffer.BlockCopy(byteTXID, 0, bHash2Buffer, hash1.Length, byteTXID.Length);
                            byte[] hash2 = hasher.ComputeHash(bHash2Buffer);

                            string nftHash = ByteToHex(hash2);

                            command = "add-asset";

                            string rpcAddAssetClass = "{ \"id\": 0, \"method\" : \"submitnft\", \"params\" : [ \"" + command + "\", \"" + hexNFTRawData + "\", \"" + ownerAddress + "\", \"" + txID + "\", \"" + classhash + "\" ] }";

                            rpcResult = rpcExec(rpcAddAssetClass);
                            jRPCResult = JObject.Parse(rpcResult);
                            string nftHashVerify = jRPCResult.result;

                            if (nftHash != nftHashVerify)
                            {
                                result = "Hash mismatch";
                            }
                            else
                                result = nftHash;
                        }
                        else
                            result = utxo;

                    }
                    binaryData = Encoding.ASCII.GetBytes(result);
                    processedAPI = true;
                }


                else if (path[0].StartsWith("nchw_send_nft"))
                {

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string passphrase = args["passphrase"];
                    string addr = args["addr"];
                    string hash = args["hash"];


                    string result = "error";

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }


                else if (path[0].StartsWith("nchw_list_nft"))
                {

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string addr = args["addr"];
                    int start_row = Convert.ToInt32(args["start_row"]);
                    int max_count = Convert.ToInt32(args["max_count"]);
                    if (max_count > 25)
                        max_count = 25;


                    string result = Global.ListNFTForAddress(addr, start_row, max_count);

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }

                else if (path[0].StartsWith("nchw_list_class"))
                {

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string addr = args["addr"];
                    int start_row = Convert.ToInt32(args["start_row"]);
                    int max_count = Convert.ToInt32(args["max_count"]);
                    if (max_count > 25)
                        max_count = 25;

                    string result = Global.ListNFTClassForAddress(addr, start_row, max_count);
                    binaryData = Encoding.ASCII.GetBytes(result);
                    processedAPI = true;
                }


                else if (path[0].StartsWith("nchw_get_nft"))
                {

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string hash = args["hash"];

                    string strResponse = GetAsset(hash);
                    if (strResponse == "error-pending-request")
                    {
                        binaryData = Encoding.ASCII.GetBytes("That asset is not available on the server and has been requested from the network.  Please try again in a few minutes");
                    }
                    else
                    {

                        byte[] binaryResponse = HexToByte(strResponse);

                        int offset = 0;
                        string assetHash = ReadString(binaryResponse, ref offset);
                        string assetClassHash = ReadString(binaryResponse, ref offset);
                        string metaData = ReadString(binaryResponse, ref offset);
                        string nftBinary = Global.ByteArrayToHexString(ReadVector(binaryResponse, ref offset));
                        string owner = ReadString(binaryResponse, ref offset);
                        string txnID = ReadString(binaryResponse, ref offset);

                        Dictionary<string, string> dResult = new Dictionary<string, string>();
                        dResult.Add("metadata", metaData);
                        dResult.Add("binary", nftBinary);
                        dResult.Add("owner", owner);

                        binaryData = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(dResult));
                    }
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

                    string command = "{ \"id\": 0, \"method\" : \"submitnft\", \"params\" : [ \"" + nftcommand + "\", \"" + nftRawData + "\", \"" + ownerAddr + "\", \"" + txID + "\", \"" + assetClassID + "\" ] }";


                    try
                    {
                        string rpcResult = rpcExec(command);
                        dynamic jRPCResult = JObject.Parse(rpcResult);
                        result = jRPCResult.result;
                    }
                    catch (Exception e)
                    {
                        Log.log(e.Message);
                        Log.log(e.StackTrace);
                    }

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }
                else if (path[0].StartsWith("submit_swap"))
                {
                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string dynAddr = args["dyn_addr"];
                    string wdynAddr = args["wdyn_addr"];
                    string amount = args["amt"];
                    string action = args["action"];

                    string result = "error";

                    try
                    {
                        result = Database.SaveSwap(dynAddr, wdynAddr, Convert.ToInt64(amount), action).ToString();
                    }
                    catch (Exception e)
                    {
                        Log.log(e.Message);
                        Log.log(e.StackTrace);
                    }

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;
                }

                else if (path[0].StartsWith("get_tx_confirm"))
                {
                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string txid = args["txid"];

                    string result = "error";

                    string getcommand = "{ \"id\": 0, \"method\" : \"getrawtransaction\", \"params\" : [ \"" + txid + "\", true ] }";

                    try
                    {
                        string rpcResult = rpcExec(getcommand);
                        dynamic jRPCResult = JObject.Parse(rpcResult);
                        result = jRPCResult.result["confirmations"].ToString();
                    }
                    catch (Exception e)
                    {
                        //Console.WriteLine(e.Message);
                        //Console.WriteLine(e.StackTrace);
                        result = "0";
                    }
                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }

                else if (path[0].StartsWith("nchw_create_wallet"))
                {
                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string passphrase = args["passphrase"];

                    string result = "error";

                    string HashedPassword = Global.CreateHash(passphrase);

                    string walletData = Global.GenerateWallet();
                    string[] wallet = walletData.Split(",");
                    string words = wallet[0];
                    string xprv = wallet[1];
                    string addr = wallet[2];

                    SymmetricAlgorithm crypt = Aes.Create();
                    HashAlgorithm hash = SHA256.Create();
                    crypt.KeySize = 256;
                    crypt.Mode = CipherMode.CBC;
                    crypt.Key = hash.ComputeHash(Encoding.UTF8.GetBytes(passphrase));

                    RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
                    byte[] iv = new byte[16];
                    provider.GetBytes(iv);
                    crypt.IV = iv;

                    byte[] bytes = Encoding.UTF8.GetBytes(walletData);
                    string encryptedWallet = "";

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, crypt.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(bytes, 0, bytes.Length);
                        }

                        encryptedWallet = Global.ByteArrayToHexString(memoryStream.ToArray());
                    }

                    Database.CreateWallet(HashedPassword, addr, encryptedWallet, Global.ByteArrayToHexString(iv));

                    result = addr;

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }

                else if (path[0].StartsWith("nchw_get_recovery_phrase"))
                {

                    string result = "error";

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string passphrase = args["passphrase"];
                    string addr = args["addr"];

                    Dictionary<string, string> data = Database.ReadNCHW(addr);
                    string pw_hash = data["nchw_password_hash"];
                    string enc_wallet = data["nchw_encrypted_wallet"];
                    string iv = data["nchw_iv"];

                    string[] pw_split = pw_hash.Split(".");
                    string HashedPassword = Global.CreateHash(passphrase, pw_split[0]);

                    string decryptedData = "";

                    if (pw_hash == HashedPassword)
                    {
                        SymmetricAlgorithm crypt = Aes.Create();
                        HashAlgorithm hash = SHA256.Create();
                        crypt.KeySize = 256;
                        crypt.Mode = CipherMode.CBC;
                        crypt.Key = hash.ComputeHash(Encoding.UTF8.GetBytes(passphrase));
                        crypt.IV = Global.HexToByteArray(iv);

                        byte[] encData = Global.HexToByteArray(enc_wallet);


                        using (MemoryStream ms = new MemoryStream(encData))
                        {
                            using (CryptoStream csDecrypt = new CryptoStream(ms, crypt.CreateDecryptor(), CryptoStreamMode.Read))
                            {
                                csDecrypt.Read(encData, 0, encData.Length);
                            }
                            byte[] bDecrypted = ms.ToArray();
                            decryptedData = System.Text.Encoding.UTF8.GetString(bDecrypted);
                        }

                    }

                    if (decryptedData.Length > 0)
                    {
                        string[] sResult = decryptedData.Split(",");
                        result = sResult[0];
                    }

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;

                }

                else if (path[0].StartsWith("nchw_send"))
                {

                    string result = "error";

                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string passphrase = args["passphrase"];
                    string from_addr = args["from_addr"];
                    string to_addr = args["to_addr"];
                    string amt = args["amount"];


                    Dictionary<string, string> data = Database.ReadNCHW(from_addr);
                    string pw_hash = data["nchw_password_hash"];
                    string enc_wallet = data["nchw_encrypted_wallet"];
                    string iv = data["nchw_iv"];

                    string[] pw_split = pw_hash.Split(".");
                    string HashedPassword = Global.CreateHash(passphrase, pw_split[0]);

                    string decryptedData = "";
                    string xprv = "";

                    if (pw_hash == HashedPassword)
                    {
                        SymmetricAlgorithm crypt = Aes.Create();
                        HashAlgorithm hash = SHA256.Create();
                        crypt.KeySize = 256;
                        crypt.Mode = CipherMode.CBC;
                        crypt.Key = hash.ComputeHash(Encoding.UTF8.GetBytes(passphrase));
                        crypt.IV = Global.HexToByteArray(iv);

                        byte[] encData = Global.HexToByteArray(enc_wallet);


                        using (MemoryStream ms = new MemoryStream(encData))
                        {
                            using (CryptoStream csDecrypt = new CryptoStream(ms, crypt.CreateDecryptor(), CryptoStreamMode.Read))
                            {
                                csDecrypt.Read(encData, 0, encData.Length);
                            }
                            byte[] bDecrypted = ms.ToArray();
                            decryptedData = System.Text.Encoding.UTF8.GetString(bDecrypted);
                        }

                    }

                    if (decryptedData.Length > 0)
                    {
                        string[] sResult = decryptedData.Split(",");
                        xprv = sResult[1];

                        decimal dAmt = Convert.ToDecimal(amt) * 100000000;

                        ulong lAmt = ((ulong)dAmt);
                        string utxo = getUTXO(from_addr, lAmt, false).Replace("\n","~");

                        if (!utxo.StartsWith("ERROR"))
                        {
                            string strTransaction = Global.CreateRawTransaction(to_addr, utxo, lAmt, xprv).Replace("\n", "");

                            string command = "{ \"id\": 0, \"method\" : \"sendrawtransaction\", \"params\" : [ \"" + strTransaction + "\" ] }";

                            try
                            {
                                string rpcResult = rpcExec(command);
                                dynamic jRPCResult = JObject.Parse(rpcResult);
                                result = jRPCResult.result;
                            }
                            catch (Exception e)
                            {
                                Log.log(e.Message);
                                Log.log(e.StackTrace);
                            }

                        }
                        else
                            result = utxo;


                    }

                    binaryData = Encoding.ASCII.GetBytes(result);

                    processedAPI = true;
                }

                else if (path[0].StartsWith("nchw_update_password"))
                {
                    Dictionary<string, string> args = ParseArgs(request.Url.Query);
                    string wallet = args["wallet"];
                    string oldpass = args["oldpass"];
                    string newpass = args["newpass"];

                    string result = "error";

                    Dictionary<string, string> data = Database.ReadNCHW(wallet);
                    string pw_hash = data["nchw_password_hash"];
                    string enc_wallet = data["nchw_encrypted_wallet"];
                    string iv = data["nchw_iv"];

                    string[] pw_split = pw_hash.Split(".");
                    string HashedPassword = Global.CreateHash(oldpass, pw_split[0]);

                    string decryptedData = "";

                    if (pw_hash == HashedPassword)
                    {
                        SymmetricAlgorithm crypt = Aes.Create();
                        HashAlgorithm hash = SHA256.Create();
                        crypt.KeySize = 256;
                        crypt.Mode = CipherMode.CBC;
                        crypt.Key = hash.ComputeHash(Encoding.UTF8.GetBytes(oldpass));
                        crypt.IV = Global.HexToByteArray(iv);

                        byte[] encData = Global.HexToByteArray(enc_wallet);


                        using (MemoryStream ms = new MemoryStream(encData))
                        {
                            using (CryptoStream csDecrypt = new CryptoStream(ms, crypt.CreateDecryptor(), CryptoStreamMode.Read))
                            {
                                csDecrypt.Read(encData, 0, encData.Length);
                            }
                            byte[] bDecrypted = ms.ToArray();
                            decryptedData = System.Text.Encoding.UTF8.GetString(bDecrypted);
                        }

                    }

                    if (decryptedData.Length > 0)
                    {

                        string newHashedPassword = Global.CreateHash(newpass);


                        SymmetricAlgorithm crypt = Aes.Create();
                        HashAlgorithm hash = SHA256.Create();
                        crypt.KeySize = 256;
                        crypt.Mode = CipherMode.CBC;
                        crypt.Key = hash.ComputeHash(Encoding.UTF8.GetBytes(newpass));

                        RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
                        byte[] iv1 = new byte[16];
                        provider.GetBytes(iv1);
                        crypt.IV = iv1;

                        byte[] bytes = Encoding.UTF8.GetBytes(decryptedData);
                        string encryptedWallet = "";

                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            using (CryptoStream cryptoStream = new CryptoStream(memoryStream, crypt.CreateEncryptor(), CryptoStreamMode.Write))
                            {
                                cryptoStream.Write(bytes, 0, bytes.Length);
                            }

                            encryptedWallet = Global.ByteArrayToHexString(memoryStream.ToArray());
                        }

                        Database.UpdateWalletData(wallet, newHashedPassword, encryptedWallet, Global.ByteArrayToHexString(iv1));
                        result = "ok";
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
                                Log.log(ex.Message);
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
                Log.log("Error in worker thread:" + e.Message);
                Log.log(e.StackTrace);

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
                Log.log(e.Message);
                Log.log(e.StackTrace);
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

        public static string getUTXO(string address, UInt64 targetAmount, bool sendMax)
        {

            string result = "";
            lock (Global.walletList)
            {
                if (Global.walletList.ContainsKey(address))
                {
                    UInt64 total = 0;
                    int ptr = 0;

                    List<Global.UTXO> outputsSelected = new List<Global.UTXO>();
                    bool transactionOK = true;
                    foreach (Global.UTXO utxo in Global.walletList[address].utxo.Values)
                    {
                        bool outputOK = true;

                        if (utxo.pendingSpend)
                            outputOK = false;

                        if ((utxo.isCoinbase) && (Global.currentBlockHeight - utxo.blockHeight < 10))
                            outputOK = false;

                        if (outputOK)
                        {
                            outputsSelected.Add(utxo);
                            result += utxo.hash + "," + utxo.vout + "," + utxo.amount.ToString("0") + "\n";
                            total += Convert.ToUInt64(utxo.amount);
                            ptr++;
                            if (ptr >= 500)
                            {
                                if (!sendMax)
                                {
                                    result = "ERROR: Too many inputs.";
                                    transactionOK = false;
                                    break;
                                }
                                else
                                    break;
                            }
                            if (total >= targetAmount + 10000m)
                                break;
                        }
                    }

                    if (!sendMax)
                        if (total < targetAmount)
                        {
                            transactionOK = false;
                            result = "ERROR: Insufficient balance.";
                        }


                    Log.log("getUTXO found coins " + total);

                    if (transactionOK)
                    {
                        foreach (Global.UTXO u in outputsSelected)
                            u.pendingSpend = true;
                    }

                }
                else
                    result = "ERROR: wallet not found";
            }

            return result;

        }



        public string ByteToHex(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 2);
            foreach (byte b in data)
            {
                sb.AppendFormat("{0:x2}", b);
            }
            return sb.ToString();
        }


        public string CreateNFTAssetClass(string owner, string metaData, UInt64 maxSerial)
        {


            string assetClassMetaData = metaData;
            string ownerAddress = owner;

            int metaDataLen = assetClassMetaData.Length;

            SHA256 hasher = SHA256.Create();

            long nftHashLen = metaDataLen + 2 + 8;     //2 bytes for length of metadata, 8 bytes for 64 bit serial
            byte[] nftRawData = new byte[nftHashLen];

            nftRawData[0] = (byte)(metaDataLen >> 8);
            nftRawData[1] = (byte)(metaDataLen & 0xFF);

            byte[] metaDataBytes = System.Text.Encoding.UTF8.GetBytes(assetClassMetaData);
            for (int i = 0; i < metaDataLen; i++)
                nftRawData[i + 2] = metaDataBytes[i];

            nftRawData[metaDataLen + 2] = (byte)(maxSerial >> 56);
            nftRawData[metaDataLen + 2 + 1] = (byte)((maxSerial & 0x00FF000000000000) >> 48);
            nftRawData[metaDataLen + 2 + 2] = (byte)((maxSerial & 0x0000FF0000000000) >> 40);
            nftRawData[metaDataLen + 2 + 3] = (byte)((maxSerial & 0x000000FF00000000) >> 32);
            nftRawData[metaDataLen + 2 + 4] = (byte)((maxSerial & 0x00000000FF000000) >> 24);
            nftRawData[metaDataLen + 2 + 5] = (byte)((maxSerial & 0x0000000000FF0000) >> 16);
            nftRawData[metaDataLen + 2 + 6] = (byte)((maxSerial & 0x000000000000FF00) >> 8);
            nftRawData[metaDataLen + 2 + 7] = (byte)((maxSerial & 0x00000000000000FF));

            string hexNFTRawData = ByteToHex(nftRawData);

            byte[] hash = hasher.ComputeHash(nftRawData);

            string strHash = ByteToHex(hash);
            string nftCommand = "00" + strHash;     //add asset class opcode

            string rpcAddAssetClass = "{ \"id\": 0, \"method\" : \"sendtoaddress\", \"params\" : [ \"" + ownerAddress + "\" , 0.0001 ], \"nft_command\" : \"" + nftCommand + "\"  }";

            string rpcResult = rpcExec(rpcAddAssetClass);
            dynamic jRPCResult = JObject.Parse(rpcResult);
            string txID = jRPCResult.result;

            byte[] byteOwnerAddr = System.Text.Encoding.UTF8.GetBytes(ownerAddress);
            byte[] byteTXID = HexToByte(txID);

            hasher.Initialize();
            byte[] bHash1Buffer = new byte[hash.Length + byteOwnerAddr.Length];
            System.Buffer.BlockCopy(hash, 0, bHash1Buffer, 0, hash.Length);
            System.Buffer.BlockCopy(byteOwnerAddr, 0, bHash1Buffer, hash.Length, byteOwnerAddr.Length);
            byte[] hash1 = hasher.ComputeHash(bHash1Buffer);

            hasher.Initialize();
            byte[] bHash2Buffer = new byte[hash1.Length + byteTXID.Length];
            System.Buffer.BlockCopy(hash1, 0, bHash2Buffer, 0, hash1.Length);
            System.Buffer.BlockCopy(byteTXID, 0, bHash2Buffer, hash1.Length, byteTXID.Length);
            byte[] hash2 = hasher.ComputeHash(bHash2Buffer);

            string nftHash = ByteToHex(hash2);


            string command = "add-class";

            rpcAddAssetClass = "{ \"id\": 0, \"method\" : \"submitnft\", \"params\" : [ \"" + command + "\", \"" + hexNFTRawData + "\", \"" + ownerAddress + "\", \"" + txID + "\", \"\" ] }";

            rpcResult = rpcExec(rpcAddAssetClass);
            jRPCResult = JObject.Parse(rpcResult);
            string nftHashVerify = jRPCResult.result;

            if (nftHash != nftHashVerify)
            {
                Console.WriteLine("hash mismatch");
                return "error";
            }
            else
                return nftHash;
        }


    }
}
