using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Text;

namespace web_app_server
{
    class Database
    {

        static string strConn = "datasource=localhost;port=3306;username=" + Global.dbUser + ";password=" + Global.dbPassword + ";database=" + Global.dbSchema;

        public static void setSetting(string name, string value)
        {
            string strSQL = "update setting set setting_value = @1 where setting_name = @2";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.Parameters.AddWithValue("@1", value);
            cmd.Parameters.AddWithValue("@2", name);
            cmd.ExecuteNonQuery();
            conn.Close();
        }


        public static string getSetting(string name)
        {
            string strSQL = "select setting_value from setting where setting_name = @1";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.Parameters.AddWithValue("@1", name);
            string result = cmd.ExecuteScalar().ToString();
            conn.Close();
            return result;
        }


        public static int SaveSwap(string dynAddr, string wdynAddr, Int64 amount, string action)
        {
            string strSQL = "insert into swap (swap_action, swap_amt, swap_timestamp, swap_dyn_addr, swap_wdyn_addr, swap_completed, swap_cancelled ) values (@1, @2, @3, @4, @5, 0, 0);select last_insert_id();";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.Parameters.AddWithValue("@1", action);
            cmd.Parameters.AddWithValue("@2", amount);
            cmd.Parameters.AddWithValue("@3", DateTimeOffset.Now.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@4", dynAddr);
            cmd.Parameters.AddWithValue("@5", wdynAddr);
            int result = Convert.ToInt32(cmd.ExecuteScalar());
            conn.Close();

            return result;

        }

        public static void saveTx(string txID, int n, decimal amount, string address)
        {
            amount *= 100000000m;

            string strSQL = "insert into tx (tx_id, tx_vout, tx_amount, tx_addr, tx_spent) values (@1, @2, @3, @4, 0)";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.Parameters.AddWithValue("@1", txID);
            cmd.Parameters.AddWithValue("@2", n);
            cmd.Parameters.AddWithValue("@3", amount);
            cmd.Parameters.AddWithValue("@4", address);
            cmd.ExecuteNonQuery();
            conn.Close();

        }

        public static void spendTransaction(string txid, int vout)
        {
            string strSQL = "select tx_addr, tx_amount from tx where tx_id = @1 and tx_vout = @2";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.Parameters.AddWithValue("@1", txid);
            cmd.Parameters.AddWithValue("@2", vout);
            MySqlDataReader reader = cmd.ExecuteReader();
            if (reader.HasRows)
            {
                reader.Read();
                updateWalletBalance(reader.GetString(0), -1m * Convert.ToDecimal(reader.GetString(1)));
                strSQL = "update tx set tx_spent = 1 where tx_id = @1 and tx_vout = @2";
                conn.Close();
                conn = new MySqlConnection(strConn);
                conn.Open();
                cmd = new MySqlCommand(strSQL, conn);
                cmd.Parameters.AddWithValue("@1", txid);
                cmd.Parameters.AddWithValue("@2", vout);
                cmd.ExecuteNonQuery();
            }
            conn.Close();


        }

        public static void updateWalletBalance(string address, decimal amount)
        {


            string strSQL;
            if (walletExists(address))
            {
                strSQL = "update addr set addr_balance = addr_balance + @1 where addr_id = @2";
                MySqlConnection conn = new MySqlConnection(strConn);
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(strSQL, conn);
                cmd.Parameters.AddWithValue("@1", amount);
                cmd.Parameters.AddWithValue("@2", address);
                cmd.ExecuteNonQuery();
                conn.Close();
            }
            else
            {
                strSQL = "insert into addr (addr_id, addr_balance) values (@1, @2)";
                MySqlConnection conn = new MySqlConnection(strConn);
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(strSQL, conn);
                cmd.Parameters.AddWithValue("@1", address);
                cmd.Parameters.AddWithValue("@2", amount);
                cmd.ExecuteNonQuery();
                conn.Close();
            }
        }

        static bool walletExists(string address)
        {
            string strSQL = "select count(1) from addr where addr_id = @1";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.Parameters.AddWithValue("@1", address);
            int result = Convert.ToInt32(cmd.ExecuteScalar().ToString());
            conn.Close();
            return result > 0;

        }

        public static string getTopWallets()
        {

            string result = "<html><body><table border=\"1\">";

            string strSQL = "select addr_id, addr_balance/ 100000000 from addr order by addr_balance desc limit 20";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result += "<tr>";
                result += "<td>" + reader.GetString(0) + "</td>";
                result += "<td>" + reader.GetString(1) + "</td>";
                result += "</tr>";
            }
            conn.Close();

            result += "</table></body></html>";

            return result;

        }


        public static string getOneWallet(string addr)
        {

            string result = "<html><body><table border=\"1\">";

            string strSQL = "select addr_id, addr_balance/ 100000000 from addr where addr_id = @1";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result += "<tr>";
                result += "<td>" + reader.GetString(0) + "</td>";
                result += "<td>" + reader.GetString(1) + "</td>";
                result += "</tr>";
            }
            conn.Close();

            result += "</table></body></html>";

            return result;

        }

        public static string getTotalSupply()
        {
            return getSetting("last_block");
        }


        public static int findSwapDYNtoWDYN (string from, decimal amt)
        {
            int result = -1;

            string strSQL = "select swap_id from swap where swap_dyn_addr = @1 and swap_amt = @2 and swap_action = 'dyn_to_wdyn' and swap_completed = 0 and swap_cancelled = 0";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.Parameters.AddWithValue("@1", from);
            cmd.Parameters.AddWithValue("@2", amt);
            object oResult = cmd.ExecuteScalar();
            if (oResult != null)
                result = Convert.ToInt32(oResult.ToString());
            conn.Close();

            return result;
        }

        public static int findSwapWDYNtoDYN(string from, decimal amt)
        {
            int result = -1;

            string strSQL = "select swap_id from swap where swap_wdyn_addr = @1 and swap_amt = @2 and swap_action = 'wdyn_to_dyn' and swap_completed = 0 and swap_cancelled = 0";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.Parameters.AddWithValue("@1", from);
            cmd.Parameters.AddWithValue("@2", amt);
            object oResult = cmd.ExecuteScalar();
            if (oResult != null)
                result = Convert.ToInt32(oResult.ToString());
            conn.Close();

            return result;
        }


        public static string getSwapDYNtoWDYNDestination(int id)
        {
            string result = "";

            string strSQL = "select swap_wdyn_addr from swap where swap_id = " + id;
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            object oResult = cmd.ExecuteScalar();
            if (oResult != null)
                result = oResult.ToString();
            conn.Close();

            return result;

        }

        public static string getSwapWDYNtoDYNDestination(int id)
        {
            string result = "";

            string strSQL = "select swap_dyn_addr from swap where swap_id = " + id;
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            object oResult = cmd.ExecuteScalar();
            if (oResult != null)
                result = oResult.ToString();
            conn.Close();

            return result;

        }


        public static void completeSwap ( int id )
        {

            long tick = DateTimeOffset.Now.ToUnixTimeSeconds();
            string strSQL = "update swap set swap_completed = " + tick + " where swap_id = " + id;
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.ExecuteNonQuery();
            conn.Close();

        }

        public static void log ( string source, string data )
        {
            string strSQL = "insert into log (log_timestamp, log_source, log_data) values (@1, @2, @3)";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.Parameters.AddWithValue("@1", DateTimeOffset.Now.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@2", source);
            cmd.Parameters.AddWithValue("@3", data);
            cmd.ExecuteScalar();
            conn.Close();


        }


        public static void CreateWallet(string HashedPassword, string addr, string encryptedWallet, string iv)
        {
            string strSQL = "insert into nchw (nchw_password_hash, nchw_encrypted_wallet, nchw_iv, nchw_wallet_addr) values (@1, @2, @3, @4)";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.Parameters.AddWithValue("@1", HashedPassword);
            cmd.Parameters.AddWithValue("@2", encryptedWallet);
            cmd.Parameters.AddWithValue("@3", iv);
            cmd.Parameters.AddWithValue("@4", addr);
            cmd.ExecuteScalar();
            conn.Close();
        }



        public static Dictionary<string,string> ReadNCHW(string addr)
        {

            Dictionary<string, string> result = new Dictionary<string, string>();

            string strSQL = "select * from nchw where nchw_wallet_addr = @1";
            MySqlConnection conn = new MySqlConnection(strConn);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(strSQL, conn);
            cmd.Parameters.AddWithValue("@1", addr);
            MySqlDataReader reader = cmd.ExecuteReader();
            
            if (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                    result.Add(reader.GetName(i), reader[i].ToString());
            }

            conn.Close();


            return result;

        }


    }
}
