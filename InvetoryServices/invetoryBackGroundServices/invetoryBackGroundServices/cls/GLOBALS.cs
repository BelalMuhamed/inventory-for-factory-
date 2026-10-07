using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MATICA_S3300e.CLS
{
    public static class GLOBALS
    {
        #region
        private const string KEY_CONFIG = "89 51 11 EE 37 C1 69 7B 93 FD 87 AF B3 4B 90 DA";
        private static string domainName = string.Empty;
        private static string makerGroup = string.Empty;
        private static string adminGroup = string.Empty;
        private static bool useLog = false;
        private static bool loginStatus = false;
        private static string loginUname = string.Empty;
        private static string loginGroup = string.Empty;
        public static event PropertyChangedEventHandler PropertyChanged;
        public static event PropertyChangedEventHandler ListItemChanged;
        private static string appStartPath = string.Empty;

        private static string branchID = string.Empty;
        private static string branchName = string.Empty;

        private static string sharedDir = string.Empty;
        private static string endOfDay = string.Empty;
        private static string apiToken = string.Empty;
        private static string webAPI = string.Empty;
        private static Dictionary<string, string> bankBranchList = new Dictionary<string, string>();
        private static Dictionary<string, string> bankProductList = new Dictionary<string, string>();
        #endregion

        #region Properties ...
        public static string _KEY_CONFIG { get { return KEY_CONFIG; } }
        public static string _domainName { set { domainName = value; } get { return domainName; } }
        public static string _makerGroup { set { makerGroup = value; } get { return makerGroup; } }
        public static string _adminGroup { set { adminGroup = value; } get { return adminGroup; } }
        public static bool _useLog { set { useLog = value; } get { return useLog; } }
        public static bool _loginStatus { set { loginStatus = value; SendPropertyChanged("LoginStatus"); } get { return loginStatus; } }
        public static string _loginUname { set { loginUname = value; SendPropertyChanged("Username");  } get { return loginUname; } }
        public static string _loginGroup { set { loginGroup = value; SendPropertyChanged("Username"); } get { return loginGroup; } }
        public static string _branchID { set { branchID = value; } get { return branchID; } }
        public static string _branchName { set { branchName = value; } get { return branchName; } }
        public static string _appStartPath { set { appStartPath = value; } get { return appStartPath; } }

        public static string _sharedDir { set { sharedDir = value; } get { return sharedDir; } }
        public static string _endOfDay { set { endOfDay = value; } get { return endOfDay; } }
        public static string _apiToken { set { apiToken = value; } get { return apiToken; } }
        public static string _WebAPI { set { webAPI = value; } get { return webAPI; } }
        public static Dictionary<string, string> _BankBranchList { set { bankBranchList = value; SendListItemPropertyChanged("BranchList"); } get { return bankBranchList; } }
        public static Dictionary<string, string> _BankProductList { set { bankProductList = value; SendListItemPropertyChanged("ProductList"); } get { return bankProductList; } }
        #endregion

        public static bool Load_sharedDirPath(out string _ERR)
        {
            try
            {
                _ERR = string.Empty;
                string bIDPath = appStartPath + @"\SharedDir.txt";
                if (!File.Exists(bIDPath)) { _ERR = "Shared Dir File Not Found!"; return false; }

                sharedDir = File.ReadAllText(bIDPath);
                if (string.IsNullOrEmpty(sharedDir)) { _ERR = "Shared Dir File is Impty!"; return false; }

                return true;
            }
            catch (Exception ex)
            {
                _ERR = ex.Message;
                return false;
            }
        }
        public static bool Load_EndOfDay(out string _ERR)
        {
            try
            {
                _ERR = string.Empty;
                string bIDPath = appStartPath + @"\DTRAUBINI.dll";
                if (!File.Exists(bIDPath)) { _ERR = "EndOfDay File Not Found!"; return false; }

                endOfDay = File.ReadAllText(bIDPath);

                return true;
            }
            catch (Exception ex)
            {
                _ERR = ex.Message;
                return false;
            }
        }
        public static bool Add_Trancsaction(out string _ERR)
        {
            try
            {
                _ERR = string.Empty;

                return true;
            }
            catch (Exception ex)
            {
                _ERR = ex.Message;
                return false;
            }
        }
        public static bool Export_Report(out string _ERR)
        {
            try
            {
                _ERR = string.Empty;

                return true;
            }
            catch (Exception ex)
            {
                _ERR = ex.Message;
                return false;
            }
        }
        private static void SendPropertyChanged(string property)
        {
            if (GLOBALS.PropertyChanged != null)
            {
                GLOBALS.PropertyChanged(null, new PropertyChangedEventArgs(property));
            }
        }
        private static void SendListItemPropertyChanged(string property) {
            if (ListItemChanged != null) ListItemChanged(null, new PropertyChangedEventArgs(property));
        }
    }
    public static class Embossing_Data
    {
        #region Variables ...
        private static int FONT = 0;
        private static int CPI = 0;
        private static int OFFSETX = 0;
        private static int OFFSETY = 0;
        private static string VALUE = string.Empty;
        #endregion

        #region Properties ...
        public static int _FONT { set { FONT = value; } get { return FONT; } }
        public static int _CPI { set { CPI = value; } get { return CPI; } }
        public static int _OFFSETX { set { OFFSETX = value; } get { return OFFSETX; } }
        public static int _OFFSETY { set { OFFSETY = value; } get { return OFFSETY; } }
        public static string _VALUE { set { VALUE = value; } get { return VALUE; } }
        #endregion
    }
    public static class Machine_Status
    {
        #region Variables ...
        private static string status = string.Empty;
        private static string tipStatus = string.Empty;
        private static string errorMessage = string.Empty;
        private static int tipTemp = 0;
        private static bool cardInside = false;
        private static bool coverOpen = false;
        private static bool feeder0pres = false;
        private static bool feeder1pres = false;
        private static bool tipNearEnd = false;
        private static bool tipEndRibn = false;
        #endregion

        #region Properties ...
        public static string _status { set { status = value; } get { return status; } }
        public static string _tipStatus { set { tipStatus = value; } get { return tipStatus; } }
        public static string _errorMessage { set { errorMessage = value; } get { return errorMessage; } }

        public static int _tipTemp { set { tipTemp = value; } get { return tipTemp; } }

        public static bool _cardInside { set { cardInside = value; } get { return cardInside; } }
        public static bool _coverOpen { set { coverOpen = value; } get { return coverOpen; } }
        public static bool _feeder0pres { set { feeder0pres = value; } get { return feeder0pres; } }
        public static bool _feeder1pres { set { feeder1pres = value; } get { return feeder1pres; } }
        public static bool _tipNearEnd { set { tipNearEnd = value; } get { return tipNearEnd; } }
        public static bool _tipEndRibn { set { tipEndRibn = value; } get { return tipEndRibn; } }
        #endregion
    }

}
