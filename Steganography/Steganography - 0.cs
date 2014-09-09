using System;
using System.Collections.Generic;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

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

    public static Bitmap embedFileIntoImage(Bitmap bitmapImage, List<byte[]> filedata, int capacity = -1, string password = "", ToolStripStatusLabel toolStripStatusLabel = null)
    {
        try
        {
            int imagewidth = bitmapImage.Width - 1;
            int imageheight = bitmapImage.Height - 1;
            byte[] filebytes = filedata[1];
            string filehash = "";
            writeOut(toolStripStatusLabel, "Encoding file into image - Please wait...");
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
            int msgLen = (sb.Length + 16) / 8;
            int pixCnt = capacity;
            if(capacity < 0)
                pixCnt = GetImageCapacity(bitmapImage) / 8;
            if (pixCnt > msgLen)
            {
                int jmpx = (pixCnt / msgLen) - 1;
                if (jmpx == 0) jmpx = 1;
                string cntstr = Convert.ToString(jmpx, 2);
                while (cntstr.Length < 18)
                    cntstr = "0" + cntstr;
                StringBuilder sfinal = new StringBuilder();
                sfinal.Append(sb);
                int pos = 0;
                for (int y = 1; y < imageheight; y+=2)
                {
                    for (int x = 1; x < imagewidth; x+=2)
                    {
                        if (pos < sfinal.Length - 3)
                        {
                            if (IsValidPixel(bitmapImage, x, y))
                            {
                                Color pixel = bitmapImage.GetPixel(x, y);
                                //string r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[pos];
                                string g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[pos];
                                //string b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[pos + 2];
                                bitmapImage.SetPixel(x, y, Color.FromArgb(pixel.R, Convert.ToInt32(g, 2), pixel.B));
                                pos ++;
                            }
                        }
                    }
                    Application.DoEvents();
                }
                writeOut(toolStripStatusLabel, "Encoding Complete.");
            }
            else {
                writeOut(toolStripStatusLabel, "Image is too small to hold the specified file!");
                throw new SteganoException("Image is too small to hold the specified file!"); 
            }
            writeOut(toolStripStatusLabel, "Checking integrity of embedded file - Please wait...");
            List<byte[]> checkdata = extractFileFromImage(bitmapImage, password);
            string checkhash = "";
            if (checkdata != null && checkdata.Count == 2)
                using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
                    checkhash = Convert.ToBase64String(sha1.ComputeHash(checkdata[1]));
            if (checkhash == filehash && checkhash != "" && filehash != "")
            {
                writeOut(toolStripStatusLabel, "Integrity check PASSED.");
                return bitmapImage;
            }
            else
            {
                writeOut(toolStripStatusLabel, "Integrity check FAILED!");
                throw new SteganoException("Failed integrity check!");
            }
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

    public static List<byte[]> extractFileFromImage(Bitmap sourceImage, string password = "", ToolStripStatusLabel toolStripStatusLabel = null)
    {
        try
        {
            int imagewidth = sourceImage.Width - 1;
            int imageheight = sourceImage.Height - 1;
            StringBuilder sb = new StringBuilder();
            StringBuilder sbfinal = new StringBuilder();
            for (int y = 1; y < imageheight; y+=2)
            {
                for (int x = 1; x < imagewidth; x+=2)
                {
                    if (IsValidPixel(sourceImage, x, y))
                    {
                        Color pixel = sourceImage.GetPixel(x, y);
                        //string r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(7, 1);
                        string g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(7, 1);
                        //string b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(7, 1);
                        sbfinal.Append(g);
                    }
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
            toolStripStatusLabel.Text = "Bad Decrypt. Wrong Password???";
            throw;
        }
        catch (Exception ex)
        {
            toolStripStatusLabel.Text = "Bad Decrypt. Wrong Password???";
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

    private static bool IsValidPixel(Bitmap bitmap, int x, int y)
    {
        Color cpixel = Color.FromArgb((int)(bitmap.GetPixel(x, y).R & 0xF8), (int)(bitmap.GetPixel(x, y).G & 0xF8), (int)(bitmap.GetPixel(x, y).B & 0xF8));
        Color upixel = Color.FromArgb((int)(bitmap.GetPixel(x, y - 1).R & 0xF8), (int)(bitmap.GetPixel(x, y - 1).G & 0xF8), (int)(bitmap.GetPixel(x, y - 1).B & 0xF8));
        Color dpixel = Color.FromArgb((int)(bitmap.GetPixel(x, y + 1).R & 0xF8), (int)(bitmap.GetPixel(x, y + 1).G & 0xF8), (int)(bitmap.GetPixel(x, y + 1).B & 0xF8));
        Color lpixel = Color.FromArgb((int)(bitmap.GetPixel(x - 1, y).R & 0xF8), (int)(bitmap.GetPixel(x - 1, y).G & 0xF8), (int)(bitmap.GetPixel(x - 1, y).B & 0xF8));
        Color rpixel = Color.FromArgb((int)(bitmap.GetPixel(x + 1, y).R & 0xF8), (int)(bitmap.GetPixel(x + 1, y).G & 0xF8), (int)(bitmap.GetPixel(x + 1, y).B & 0xF8));
        return !(cpixel.Equals(upixel) && cpixel.Equals(dpixel) && cpixel.Equals(lpixel) && cpixel.Equals(rpixel));
    }

    public static int GetImageCapacity(Bitmap bitmap)
    {
        int cnt = -1;
        try
        {
            for (int y = 1; y < bitmap.Height - 1; y+=2)
            {
                for (int x = 1; x < bitmap.Width - 1; x+=2)
                    if (IsValidPixel(bitmap, x, y))
                        cnt++;
                Application.DoEvents();
            }
        }
        catch 
        { 
        }
        return cnt;
    }

    private static void writeOut(ToolStripStatusLabel toolStripStatusLabel, string text)
    {
        if (toolStripStatusLabel != null)
        {
            toolStripStatusLabel.Text = text;
            Thread.Sleep(50);
            Application.DoEvents();
        }
    }
}