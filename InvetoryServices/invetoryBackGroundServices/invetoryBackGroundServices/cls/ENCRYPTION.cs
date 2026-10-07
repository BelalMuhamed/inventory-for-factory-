using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Reflection;

namespace MATICA_S3300e.CLS
{
    public static class ENCRYPTION
    {
        #region Variables ...
        private const string mIv = "0000000000000000";
        private const CipherMode mCMode = CipherMode.CBC;
        private const PaddingMode mPMode = PaddingMode.Zeros;
        #endregion

        #region Functions ...
        public static string Enc_TripleDES(out string err, string clearText, string key)
        {
            try
            {
                err = string.Empty;
                byte[] clearByte = Encoding.Unicode.GetBytes(clearText);
                if (clearByte == null) return null;
                byte[] keyByte = UTILITIES.StringToByteArray(out err, key.Replace(" ", ""));
                if (keyByte == null) return null;
                byte[] ivByte = UTILITIES.StringToByteArray(out err, mIv);
                if (ivByte == null) return null;

                TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider();
                des.KeySize = keyByte.Length * 8;
                des.Key = keyByte;
                des.IV = ivByte;
                des.Mode = CipherMode.CBC;
                des.Padding = PaddingMode.Zeros;
                ICryptoTransform ic = des.CreateEncryptor();
                byte[] enc = ic.TransformFinalBlock(clearByte, 0, clearByte.Length);
                des.Clear();
                return UTILITIES.ByteArrayToString(out err, enc);
            }
            catch (Exception ex)
            {
                err = "Enc_TripleDES Exception" + "\r\n" + ex.Message;
                return null;
            }
        }
        public static string Dec_TripleDES(out string err, string cipherText, string key)
        {
            try
            {
                byte[] cipherByte = UTILITIES.StringToByteArray(out err, cipherText.Replace(" ", ""));
                if (cipherByte == null) return null;
                byte[] keyByte = UTILITIES.StringToByteArray(out err, key.Replace(" ", ""));
                if (keyByte == null) return null;
                byte[] ivByte = UTILITIES.StringToByteArray(out err, mIv);
                if (ivByte == null) return null;

                TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider();
                des.KeySize = keyByte.Length * 8;
                des.Key = keyByte;
                des.IV = ivByte;
                des.Mode = CipherMode.CBC;
                des.Padding = PaddingMode.Zeros;
                ICryptoTransform ic = des.CreateDecryptor();
                byte[] enc = ic.TransformFinalBlock(cipherByte, 0, cipherByte.Length);
                des.Clear();
                return Encoding.Unicode.GetString(enc);
            }
            catch (Exception ex)
            {
                err = "Dec_TripleDES Exception" + "\r\n" + ex.Message;
                return null;
            }
        }
        public static string GetRandomKey(out string err, string key)
        {
            try
            {
                err = string.Empty;
                Random rand = new Random();
                int num = rand.Next(10, 99);
                string dateTime = DateTime.Now.ToString("yyyyMMddHHmmss") + num.ToString();
                string enc = _3DES(out err, dateTime, key, mIv, true, CipherMode.CBC, PaddingMode.None);
                if (enc == null) return null;
                string mac = CalcMAC(out err, dateTime, key);
                if (mac == null) return null;
                return enc + mac;
            }
            catch (Exception ex)
            {
                err = "GetRandomKey Exception" + "\r\n" + ex.Message;
                return null;
            }
        }
        public static string CalcMAC(out string err, string clearText, string key)
        {
            try
            {
                byte[] plainByte = UTILITIES.StringToByteArray(out err, clearText);
                byte[] keyByte = UTILITIES.StringToByteArray(out err, key.Replace(" ", ""));
                byte[] ivByte = UTILITIES.StringToByteArray(out err, mIv);
                byte[] k_Left = new byte[8];
                byte[] k_Right = new byte[8];

                for (int i = 0; i < 16; i++)
                {
                    if (i < 8)
                        k_Left[i] = keyByte[i];
                    else
                        k_Right[i - 8] = keyByte[i];
                }
                DES des1 = DES.Create();
                DES des2 = DES.Create();
                des1.Key = k_Left;
                des2.Key = k_Right;
                des1.Mode = des2.Mode = mCMode;
                des1.Padding = des2.Padding = mPMode;
                des1.IV = des2.IV = ivByte;
                byte[] intermediate = des1.CreateEncryptor().TransformFinalBlock(plainByte, 0, plainByte.Length);
                byte[] intermediate2 = des2.CreateDecryptor().TransformFinalBlock(intermediate, intermediate.Length - 8, 8);
                byte[] result = des1.CreateEncryptor().TransformFinalBlock(intermediate2, 0, 8);
                return UTILITIES.ByteArrayToString(out err, result);

            }
            catch (Exception ex)
            {
                err = "CalcMAC Exception" + "\r\n" + ex.Message;
                return null;
            }
        }
        public static string _3DES(out string err, string plainText, string eKey, string eIV, bool encStatus, CipherMode cMode, PaddingMode pMode)
        {
            try
            {
                err = string.Empty;
                byte[] Key = UTILITIES.StringToByteArray(out err, eKey);
                byte[] IV = UTILITIES.StringToByteArray(out err, eIV);
                byte[] plainByte = UTILITIES.StringToByteArray(out err, plainText);
                TripleDESCryptoServiceProvider sm = new TripleDESCryptoServiceProvider();
                sm.Mode = cMode;
                sm.Padding = pMode;
                MethodInfo mi = sm.GetType().GetMethod("_NewEncryptor", BindingFlags.NonPublic | BindingFlags.Instance);
                object[] Par = { Key, sm.Mode, IV, sm.FeedbackSize, Convert.ToInt32(encStatus) };
                ICryptoTransform trans = mi.Invoke(sm, Par) as ICryptoTransform;
                byte[] result = trans.TransformFinalBlock(plainByte, 0, plainByte.Length);
                return UTILITIES.ByteArrayToString(out err, result);
            }
            catch (Exception ex)
            {
                err = "_3DES Exception" + "\r\n" + ex.Message;
                return null;
            }
        }
        #endregion
    }
}
