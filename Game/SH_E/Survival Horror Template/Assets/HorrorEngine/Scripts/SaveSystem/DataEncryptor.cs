using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HorrorEngine
{
    public static class DataEncryptor
    {
        public static byte[] Encrypt(byte[] bytesData, string key)
        {
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(key, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytesData, 0, bytesData.Length);
                        cs.Close();
                    }
                    return ms.ToArray();
                }
            }
        }

        public static string Encrypt(string clearText, string key)
        {
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            byte[] encriptedBytes = Encrypt(clearBytes, key);
            clearText = Encoding.Unicode.GetString(encriptedBytes);
            return clearText;
        }

        public static byte[] Decrypt(byte[] cipherBytes, string key)
        {
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(key, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }

                    return ms.ToArray();
                }
            }
        }

        public static string DecryptString(string cipherText, string key)
        {
            cipherText = cipherText.Replace(" ", "+");
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            byte[] decrypted = Decrypt(cipherBytes, key);
            cipherText = Encoding.Unicode.GetString(decrypted);
            return cipherText;
        }
    }
}