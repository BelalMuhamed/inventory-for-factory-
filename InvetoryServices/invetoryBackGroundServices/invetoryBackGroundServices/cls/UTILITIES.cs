using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace MATICA_S3300e.CLS
{
    public static class UTILITIES
    {
        #region Conversion Functions ...
        public static string ByteArrayToString(out string err, byte[] data)
        {
            try
            {
                err = string.Empty;
                StringBuilder sDataOut;
                if (data != null)
                {
                    sDataOut = new StringBuilder(data.Length * 2);
                    for (int nI = 0; nI < data.Length; nI++)
                        sDataOut.AppendFormat("{0:X02}", data[nI]);
                }
                else
                    sDataOut = new StringBuilder();
                return sDataOut.ToString();
            }
            catch (Exception Ex)
            {
                err = Ex.Message;
                return null;
            }
        }
        public static byte[] StringToByteArray(out string err, string data)
        {
            try
            {
                err = string.Empty;
                byte[] rData = new byte[(data.Length / 2)];
                for (int i = 0; i < data.Length / 2; i++) rData[i] = Convert.ToByte(data.Substring(i * 2, 2), 16);
                return rData;
            }
            catch (Exception Ex)
            {
                err = Ex.Message;
                return null;
            }
        }
        public static string HexStringTOArabic(out string err, byte[] data)
        {
            try
            {
                err = string.Empty;
                string arabic = Encoding.GetEncoding(1256).GetString(data);
                return arabic.Trim();
            }
            catch (Exception ex)
            {
                err = ex.Message;
                return null;
            }
        }
        public static byte[] ArabicToHexString(out string err, string data)
        {
            try
            {
                err = string.Empty;
                byte[] hex = Encoding.GetEncoding(1256).GetBytes(data);
                return hex;
            }
            catch (Exception ex)
            {
                err = ex.Message;
                return null;
            }
        }
        public static string ArabicToHexString(out string err, string data, int length)
        {
            try
            {
                err = string.Empty;
                byte[] hex = Encoding.GetEncoding(1256).GetBytes(data);
                string hexData = ByteArrayToString(out err, hex);
                if (hexData == null) return null;
                int dataLen = data.Length;
                if (dataLen > length) { return hexData; }
                for (int i = 0; i < length - dataLen; i++)
                {
                    hexData = hexData + "20";
                }

                return hexData;
            }
            catch (Exception ex)
            {
                err = ex.Message;
                return null;
            }
        }
        public static bool CheckNID(out string _ERR, string NID)
        {
            try
            {
                _ERR = string.Empty;

                if (NID.Length != 14) return false;
                if (NID.Substring(0, 1) != "2" && NID.Substring(0, 1) != "3") return false;
                string bDate = NID.Substring(1, 6);
                if (NID.Substring(0, 1) == "2") bDate = "19" + bDate;
                else if (NID.Substring(0, 1) == "3") bDate = "20" + bDate;
                else return false;
                bDate = bDate.Substring(0, 4) + "/" + bDate.Substring(4, 2) + "/" + bDate.Substring(6, 2);
                DateTime date = new DateTime();
                if (!DateTime.TryParse(bDate, out date)) return false;
                if ((DateTime.Now.Year - 19) < date.Year) return false;
                string govcode = NID.Substring(7, 2);
                switch (govcode)
                {
                    case "01":
                    case "02":
                    case "03":
                    case "04":
                    case "11":
                    case "12":
                    case "13":
                    case "14":
                    case "15":
                    case "16":
                    case "17":
                    case "18":
                    case "19":
                    case "21":
                    case "22":
                    case "23":
                    case "24":
                    case "25":
                    case "26":
                    case "27":
                    case "28":
                    case "29":
                    case "31":
                    case "32":
                    case "33":
                    case "34":
                    case "35":
                    case "88":
                        break;
                    default:
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _ERR = ex.Message;
                return false;
            }
        }
        public static string FormatIP(out string _ERR,string IP)
        {
            try
            {
                _ERR = string.Empty;
                string[] _ip = IP.Split('.');
                string retIP = string.Empty;

                foreach(string i in _ip)
                {
                    retIP += Convert.ToInt32(i).ToString() + ".";
                }

                return retIP.Substring(0, retIP.Length - 1);
            }
            catch (Exception ex)
            {
                _ERR = ex.Message;
                return null;
            }
        }
        #endregion

        #region
        public static string FormatHexString(string hexString, out bool isValidHex)
        {
            hexString = hexString.Replace("0x", string.Empty);
            hexString = hexString.Replace(" ", string.Empty);
            hexString = hexString.ToUpper();

            isValidHex = IsValidHexString(hexString);

            // Fills a space in between every 2 characters
            hexString = Regex.Replace(hexString, ".{2}", "$0 ").TrimEnd();

            return hexString;
        }
        public static string BytesToHexString(byte[] bytes)
        {
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        public static byte[] AsciiToBytes(string input)
        {
            return Encoding.ASCII.GetBytes(input);
        }
        public static byte[] HexStringToBytes(string hexString)
        {
            hexString = hexString.Replace("0x", String.Empty);
            hexString = hexString.Replace(" ", String.Empty);
            hexString = hexString.ToUpper();

            if (!IsValidHexString(hexString))
                throw new Exception("The input is not a valid hex string");

            if (1 == (hexString.Length % 2))
                throw new Exception("The hex string cannot have an odd number of digits");

            byte[] arr = new byte[hexString.Length >> 1];

            for (int i = 0; i < hexString.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hexString[i << 1]) << 4) + (GetHexVal(hexString[(i << 1) + 1])));
            }

            return arr;
        }
        private static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            //return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
        public static bool IsValidHexString(string hexString)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(hexString, @"\A\b[0-9a-fA-F]+\b\Z");
        }
        public static string GetFileNameFromPath(string fileWithPath, bool isExtensionNeeded = true)
        {
            try
            {
                if (isExtensionNeeded)
                {
                    return Path.GetFileName(fileWithPath);
                }
                else
                {
                    return Path.GetFileNameWithoutExtension(fileWithPath);
                }
            }
            catch (Exception e)
            {
                return "<<Invalid Path>>" + e.Message;
            }
        }
        public static bool ValidateByteArrayInput(string byteArrayInput, int minByteLength, int maxByteLength)
        {
            bool validInput = true;

            byteArrayInput = byteArrayInput.Replace(" ", "");

            if ((byteArrayInput.Length % 2) != 0)
            {
                validInput = false;
            }
            else if (byteArrayInput.Length < (minByteLength * 2))
            {
                validInput = false;
            }
            else if (byteArrayInput.Length > (maxByteLength * 2))
            {
                validInput = false;
            }
            else if (!IsValidHexString(byteArrayInput))
            {
                validInput = false;
            }
            return validInput;
        }
        public static bool ValidateNumericInput(string numericInput, int minValue, int maxValue)
        {
            bool validInput = true;

            numericInput = Regex.Replace(numericInput, "[^0-9]+", string.Empty);
            if (string.Empty == numericInput)
            {
                validInput = false;
            }
            else
            {
                int inputValue = int.Parse(numericInput);
                if ((inputValue < minValue) || (inputValue > maxValue))
                {
                    validInput = false;
                }
            }
            return validInput;
        }
        public static byte[] GetRandomBytes(int byteLen)
        {
            byte[] random = new byte[byteLen];

            //RNGCryptoServiceProvider is an implementation of a random number generator.
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            // The array is now filled with cryptographically strong random bytes.
            rng.GetBytes(random);

            return random;
        }
        public static uint StrToUInt32(string input)
        {
            return Convert.ToUInt32(input);
        }
        public static byte[] UIntToBytes(uint input)
        {
            byte[] bytes = BitConverter.GetBytes(input);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }
        public static byte[] UIntToBytes(UInt64 input)
        {
            byte[] bytes = BitConverter.GetBytes(input);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }
        public static ushort GetUInt16(byte[] input)
        {
            if (input.Length < 2)
            {
                throw new Exception("The input is not a valid short");
            }
            if (BitConverter.IsLittleEndian)
                Array.Reverse(input);

            return BitConverter.ToUInt16(input, 0);
        }
        public static uint GetUInt32(byte[] input)
        {
            if (input.Length < 4)
            {
                throw new Exception("The input is not a valid integer");
            }
            if (BitConverter.IsLittleEndian)
                Array.Reverse(input);

            return BitConverter.ToUInt32(input, 0);
        }
        public static UInt64 GetUInt64(byte[] input)
        {
            if (input.Length < 8)
            {
                throw new Exception("The input is not a valid 64 bit integer");
            }
            if (BitConverter.IsLittleEndian)
                Array.Reverse(input);

            return BitConverter.ToUInt64(input, 0);
        }
        public static byte[] ConcatByteArrays(params byte[][] arrays)
        {
            return arrays.SelectMany(x => x).ToArray();
        }
        public static byte[] NumStrToBytes(string input)
        {
            return UTILITIES.UIntToBytes(UTILITIES.StrToUInt32(input));
        }
        public static bool ContainsSpecialCharacters(string input)
        {
            var regexItem = new Regex("^[a-zA-Z0-9 ]*$");
            return !regexItem.IsMatch(input);
        }

        #endregion
    }
}
