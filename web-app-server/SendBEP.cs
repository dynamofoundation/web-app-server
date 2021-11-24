using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace web_app_server
{
    public class SendBEP
    {
        public string bscAddr;
        public decimal amt;
        public int id;

        public void send()
        {
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
        }
    }
}
