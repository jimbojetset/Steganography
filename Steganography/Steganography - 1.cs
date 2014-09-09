using System;
using System.Collections.Generic;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;

// 18 bits = jump value
// 8 bits = filename length in bytes
// 24 bits = file size in bytes
// x bits = filename
// x bits = file
public class SteganoException : Exception
{
    public SteganoException()
    {
    }

    public SteganoException(string message)
        : base(message)
    {
    }

    public SteganoException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

internal static class Stegano
{
    private static SymmetricAlgorithm _algorithm = new RijndaelManaged();

    public static Bitmap embedFileIntoImage(Bitmap bitmapImage, List<byte[]> filedata, string password = "")
    {
        try
        {
            int imagewidth = bitmapImage.Width - 1;
            int imageheight = bitmapImage.Height - 1;
            byte[] filebytes = filedata[1];
            string filehash = "";
            using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
                filehash = Convert.ToBase64String(sha1.ComputeHash(filebytes));
            if (!String.IsNullOrEmpty(password))
                filebytes = encryptData(filebytes, password);
            string filename = string.Empty;
            byte[] filenamebytes = filedata[0];
            if (!String.IsNullOrEmpty(password))
                filenamebytes = encryptData(filenamebytes, password);
            foreach (byte b in filenamebytes)
                filename += Convert.ToString(b, 2).PadLeft(8, '0');
            string filenamelength = Convert.ToString(filename.Length / 8, 2);
            while (filenamelength.Length < 8)
                filenamelength = "0" + filenamelength;
            string filelength = Convert.ToString(filebytes.Length, 2);
            while (filelength.Length < 24)
                filelength = "0" + filelength;
            StringBuilder sb = new StringBuilder();
            sb.Append(filenamelength);
            sb.Append(filelength);
            sb.Append(filename);
            foreach (byte b in filebytes)
                sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
            sb.Append("000");//buffer
            int msgLen = (sb.Length + 16) / 3;
            int pixCnt = (bitmapImage.Width - 2) * (bitmapImage.Height - 2);
            if (pixCnt > msgLen)
            {
                int jmpx = (pixCnt / msgLen) - 1;
                if (jmpx == 0) jmpx = 1;
                string cntstr = Convert.ToString(jmpx, 2);
                while (cntstr.Length < 18)
                    cntstr = "0" + cntstr;
                StringBuilder sfinal = new StringBuilder();
                sfinal.Append(cntstr);
                sfinal.Append(sb);
                int pos = 0;
                for (int x = 1; x < 7; x++)
                {
                    Color pixel = bitmapImage.GetPixel(x, 1);
                    string r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[pos];
                    string g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[pos + 1];
                    string b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[pos + 2];
                    bitmapImage.SetPixel(x, 1, Color.FromArgb(Convert.ToInt32(r, 2), Convert.ToInt32(g, 2), Convert.ToInt32(b, 2)));
                    pos += 3;
                }
                bool jump = true;
                int cnt = 41;
                for (int y = 1; y < imageheight; y++)
                {
                    for (int x = 1; x < imagewidth; x++)
                    {
                        if (jump) { jump = false; x = cnt; }
                        if (pos < sfinal.Length - 3 && cnt % jmpx == 0)
                        {
                            Color pixel = bitmapImage.GetPixel(x, y);
                            string r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[pos];
                            string g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[pos + 1];
                            string b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[pos + 2];
                            bitmapImage.SetPixel(x, y, Color.FromArgb(Convert.ToInt32(r, 2), Convert.ToInt32(g, 2), Convert.ToInt32(b, 2)));
                            pos += 3;
                        }
                        cnt++;
                    }
                }
            }
            else { throw new SteganoException("Image is too small to hold the specified file!"); }
            List<byte[]> checkdata = extractFileFromImage(bitmapImage, password);
            string checkhash = "";
            if (checkdata != null && checkdata.Count == 2)
                using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
                    checkhash = Convert.ToBase64String(sha1.ComputeHash(checkdata[1]));
            if (checkhash == filehash && checkhash != "" && filehash != "")
                return bitmapImage;
            else
                throw new SteganoException("Failed integrity check!"); ;
        }
        catch (SteganoException ex)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SteganoException("Stegano: " + ex.Message);
        }
    }

    public static List<byte[]> extractFileFromImage(Bitmap sourceImage, string password = "")
    {
        try
        {
            int imagewidth = sourceImage.Width - 1;
            int imageheight = sourceImage.Height - 1;
            StringBuilder sb = new StringBuilder();
            for (int x = 1; x < 7; x++)
            {
                Color pixel = sourceImage.GetPixel(x, 1);
                string r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(7, 1);
                string g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(7, 1);
                string b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(7, 1);
                sb.Append(r + g + b);
            }
            int jmpx = Convert.ToInt32(sb.ToString(), 2);
            bool jump = true;
            sb = null;
            StringBuilder sbfinal = new StringBuilder();
            int cnt = 41;
            for (int y = 1; y < imageheight; y++)
            {
                for (int x = 1; x < imagewidth; x++)
                {
                    if (jump) { jump = false; x = cnt; }
                    if (cnt % jmpx == 0)
                    {
                        Color pixel = sourceImage.GetPixel(x, y);
                        string r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(7, 1);
                        string g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(7, 1);
                        string b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(7, 1);
                        sbfinal.Append(r + g + b);
                    }
                    cnt++;
                }
            }
            int filenamelength = Convert.ToInt32(sbfinal.ToString(0, 8), 2);
            int filesize = Convert.ToInt32(sbfinal.ToString(8, 24), 2);
            byte[] filenamebytes = GetBytesFromBinaryString(sbfinal.ToString(32, filenamelength * 8));
            if (!String.IsNullOrEmpty(password))
                filenamebytes = decryptData(filenamebytes, password);
            if (!IsASCII(filenamebytes))
                throw new SteganoException("Bad Decrypt");
            byte[] filebytes = GetBytesFromBinaryString(sbfinal.ToString(32 + (filenamelength * 8), filesize * 8));
            if (!String.IsNullOrEmpty(password))
                filebytes = decryptData(filebytes, password);
            List<byte[]> data = new List<byte[]>();
            data.Add(filenamebytes);
            data.Add(filebytes);
            return data;
        }
        catch (SteganoException ex)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SteganoException("Bad Decrypt");
        }
    }

    private static Byte[] GetBytesFromBinaryString(String binary)
    {
        var list = new List<Byte>();
        for (int i = 0; i < binary.Length; i += 8)
        {
            String t = binary.Substring(i, 8);
            list.Add(Convert.ToByte(t, 2));
        }
        return list.ToArray();
    }

    private static byte[] encryptData(byte[] data, string password)
    {
        getKey(password);
        ICryptoTransform encryptor = _algorithm.CreateEncryptor();
        byte[] cryptoData = encryptor.TransformFinalBlock(data, 0, data.Length);
        return cryptoData;
    }

    private static byte[] decryptData(byte[] cryptoData, string password)
    {
        getKey(password);
        ICryptoTransform decryptor = _algorithm.CreateDecryptor();
        byte[] data = decryptor.TransformFinalBlock(cryptoData, 0, cryptoData.Length);
        return data;
    }

    private static void getKey(string password)
    {
        byte[] salt = new byte[8];
        byte[] passwordBytes = Encoding.ASCII.GetBytes(password);
        int length = Math.Min(passwordBytes.Length, salt.Length);
        for (int i = 0; i < length; i++)
            salt[i] = passwordBytes[i];
        Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(password, salt);
        _algorithm.Key = key.GetBytes(_algorithm.KeySize / 8);
        _algorithm.IV = key.GetBytes(_algorithm.BlockSize / 8);
    }

    private static bool IsASCII(byte[] bytes)
    {
        string value = Encoding.UTF8.GetString(bytes);
        return Encoding.UTF8.GetByteCount(value) == value.Length;
    }

    private static bool ValidPixel(Bitmap bitmap, int x, int y)
    {
        string c = Get5MSBofRGB(bitmap, x, y);
        string u = Get5MSBofRGB(bitmap, x, y - 1);
        string d = Get5MSBofRGB(bitmap, x, y + 1);
        string l = Get5MSBofRGB(bitmap, x - 1, y);
        string r = Get5MSBofRGB(bitmap, x + 1, y);
        return !(c == u && c == d && c == l && c == r);
    }

    private static string Get5MSBofRGB(Bitmap bitmap, int x, int y)
    {
        Color pixel = bitmap.GetPixel(x, y);
        string r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(0, 5);
        string g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(0, 5);
        string b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(0, 5);
        return r + g + b;
    }
}