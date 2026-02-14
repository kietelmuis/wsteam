using System;
using System.Security.Cryptography;
using System.Text;

public class DepotDecryptor
{
    public static string DecryptFilename(string encryptedFilename, byte[] depotKey)
    {
        byte[] encryptedBytes = HexStringToBytes(encryptedFilename);

        using Aes aes = Aes.Create();
        aes.Key = depotKey;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        using ICryptoTransform decryptor = aes.CreateDecryptor();
        byte[] decryptedBytes = decryptor.TransformFinalBlock(
            encryptedBytes, 0, encryptedBytes.Length);

        int nullIndex = Array.IndexOf(decryptedBytes, (byte)0);
        if (nullIndex >= 0)
        {
            Array.Resize(ref decryptedBytes, nullIndex);
        }

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    private static byte[] HexStringToBytes(string hex)
    {
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
