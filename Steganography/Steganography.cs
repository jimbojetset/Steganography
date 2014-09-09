using System;
using System.Collections.Generic;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// 9 bits = jump value
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
    internal enum Stealthiness { Maximum = 0xF8, Medium = 0xFC, Minimum = 0xFE }

    public static Bitmap embedFileIntoImage(Bitmap bitmapImage, List<byte[]> filedata, int capacity = -1, string password = "", ToolStripStatusLabel toolStripStatusLabel = null, bool stealthy = true, Stealthiness stealthValue = Stealthiness.Maximum)
    {
        try
        {
            int imagewidth = bitmapImage.Width - 1;
            int imageheight = bitmapImage.Height - 1;
            byte[] filebytes = filedata[1];
            string filehash = "";
            writeOut(toolStripStatusLabel, "Encoding file into image - Please wait...");
            // Calculate pre-encoded SHA1 Hash of File Data
            using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
                filehash = Convert.ToBase64String(sha1.ComputeHash(filebytes));
            // Encrypt File Data (bytes)
            if (!String.IsNullOrEmpty(password))
                filebytes = encryptData(filebytes, password);
            // Encrypt Filename (bytes)
            byte[] filenamebytes = filedata[0];
            if (!String.IsNullOrEmpty(password))
                filenamebytes = encryptData(filenamebytes, password);
            // Convert Filename to binary
            string filename = string.Empty;
            foreach (byte b in filenamebytes)
                filename += Convert.ToString(b, 2).PadLeft(8, '0');
            // Convert length of filename (in bytes) into binary
            string filenamelength = Convert.ToString(filename.Length / 8, 2).PadLeft(8, '0');
            // Convert length of File Data (in bytes) into binary
            string filelength = Convert.ToString(filebytes.Length, 2).PadLeft(24, '0');
            // Build binary string
            StringBuilder sfinal = new StringBuilder();
            sfinal.Append(filenamelength);
            sfinal.Append(filelength);
            sfinal.Append(filename);
            foreach (byte b in filebytes)
                sfinal.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
            sfinal.Append("000");//buffer
            // Check binary data will fit
            int msgLen = (sfinal.Length + 16) / 8;
            int pixCnt = capacity;
            if(capacity < 0)
                pixCnt = (int)Math.Floor((double)((GetImageCapacity(bitmapImage, stealthy, stealthValue) * 3) / 8));
            // Calculate bit distribution throughout image (jump value)
            //if(pixCnt)
            int jmp = 0;
            if (pixCnt > msgLen)
                jmp = (int)Math.Floor((double)(pixCnt / msgLen));
            if (jmp > 500) jmp = 500;
            if (jmp == 0) jmp = 1;
            string jump = Convert.ToString(jmp, 2).PadLeft(9, '0');
            // Insert 'jump' value into first three pixels
            for (int x = 0; x < 9; x+=3)
            {
                Color pixel = bitmapImage.GetPixel(x, 0);
                string r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(0, 7) + jump[x];
                string g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(0, 7) + jump[x+1];
                string b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(0, 7) + jump[x+2];
                bitmapImage.SetPixel(x, 0, Color.FromArgb(Convert.ToInt32(r, 2), Convert.ToInt32(g, 2), Convert.ToInt32(b, 2)));
            }
            int streamPos = 0;
            int pixelPos = 0;
            // Insert all binary data into image
            if (pixCnt > msgLen)
            {
                for (int y = 1; y < imageheight - 1; y++)
                {
                    for (int x = 1; x < imagewidth - 1; x ++)
                    {
                        if (streamPos < sfinal.Length - 3)
                        {
                            if (IsValidPixel(bitmapImage, x, y, stealthy, stealthValue))
                            {
                                if (pixelPos % jmp == 0)
                                {
                                    Color pixel = bitmapImage.GetPixel(x, y);
                                    string r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[streamPos];
                                    string g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[streamPos + 1];
                                    string b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(0, 7) + sfinal[streamPos + 2];
                                    bitmapImage.SetPixel(x, y, Color.FromArgb(Convert.ToInt32(r, 2), Convert.ToInt32(g, 2), Convert.ToInt32(b, 2)));
                                    streamPos += 3;
                                }
                                pixelPos++;
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
            // Extract data from image (and decrypt if necessary)
            List<byte[]> checkdata = extractFileFromImage(bitmapImage, password, toolStripStatusLabel, stealthy, stealthValue);
            string checkhash = "";
            // Calculate SHA1 hash of extracted data
            if (checkdata != null && checkdata.Count == 2)
                using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
                    checkhash = Convert.ToBase64String(sha1.ComputeHash(checkdata[1]));
            // Check extracted SHA1 hash with pre-encoded SHA1 hash
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
            throw new Exception(ex.Message);
        }
        catch (Exception ex)
        {
            throw new SteganoException("Stegano: " + ex.Message);
        }
    }

    public static List<byte[]> extractFileFromImage(Bitmap sourceImage, string password = "", ToolStripStatusLabel toolStripStatusLabel = null, bool stealthy = true, Stealthiness stealthValue = Stealthiness.Maximum)
    {
        try
        {
            // Extract jump value from first 3 pixels
            StringBuilder sb = new StringBuilder();
            for (int x = 0; x < 9; x += 3)
            {
                Color pixel = sourceImage.GetPixel(x, 0);
                string r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(7, 1);
                string g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(7, 1);
                string b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(7, 1);
                sb.Append(r + g + b);
            }
            int jump = Convert.ToInt32(sb.ToString(0, 9), 2);
            int imagewidth = sourceImage.Width - 1;
            int imageheight = sourceImage.Height - 1;
            StringBuilder sbfinal = new StringBuilder();
            int pixelPos = 0;
            // Extract ALL lsb's from image using jump value
            for (int y = 1; y < imageheight - 1; y++)
            {
                for (int x = 1; x < imagewidth - 1; x++)
                {
                    if (IsValidPixel(sourceImage, x, y, stealthy, stealthValue))
                    {
                        if (pixelPos % jump == 0)
                        {
                            Color pixel = sourceImage.GetPixel(x, y);
                            string r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(7, 1);
                            string g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(7, 1);
                            string b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(7, 1);
                            sbfinal.Append(r + g + b);
                        }
                        pixelPos++;
                    }
                }
            }
            // Get filename length (in bytes) value from first 8 bits
            int filenamelength = Convert.ToInt32(sbfinal.ToString(0, 8), 2);
            // Get file data length (in bytes) value from next 24 bits
            int filesize = Convert.ToInt32(sbfinal.ToString(8, 24), 2);
            // Get filename
            byte[] filenamebytes = GetBytesFromBinaryString(sbfinal.ToString(32, filenamelength * 8));
            // Decrypt filename (if necessary)
            if (!String.IsNullOrEmpty(password))
                filenamebytes = decryptData(filenamebytes, password);
            // Check for valid filename
            if (!IsASCII(filenamebytes))
                throw new SteganoException("Bad Decrypt");
            // Extract remaining file data
            byte[] filebytes = GetBytesFromBinaryString(sbfinal.ToString(32 + (filenamelength * 8), filesize * 8));
            // Decrypt file data (if necessary)
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
            throw new Exception(ex.Message);
        }
        catch
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

    private static bool IsValidPixel(Bitmap bitmap, int x, int y, bool stealthy = true, Stealthiness stealthValue = Stealthiness.Maximum)
    {
        Application.DoEvents();
        if (stealthy)
        {
            // looks above, below, left & right of current pixel and returns false if 5 msb's of all 5 pixels match (ie all virtually the same color)
            int msb = (int)stealthValue;
            Color cpixel = Color.FromArgb((int)(bitmap.GetPixel(x, y).R & msb), (int)(bitmap.GetPixel(x, y).G & msb), (int)(bitmap.GetPixel(x, y).B & msb));
            Color upixel = Color.FromArgb((int)(bitmap.GetPixel(x, y - 1).R & msb), (int)(bitmap.GetPixel(x, y - 1).G & msb), (int)(bitmap.GetPixel(x, y - 1).B & msb));
            Color dpixel = Color.FromArgb((int)(bitmap.GetPixel(x, y + 1).R & msb), (int)(bitmap.GetPixel(x, y + 1).G & msb), (int)(bitmap.GetPixel(x, y + 1).B & msb));
            Color lpixel = Color.FromArgb((int)(bitmap.GetPixel(x - 1, y).R & msb), (int)(bitmap.GetPixel(x - 1, y).G & msb), (int)(bitmap.GetPixel(x - 1, y).B & msb));
            Color rpixel = Color.FromArgb((int)(bitmap.GetPixel(x + 1, y).R & msb), (int)(bitmap.GetPixel(x + 1, y).G & msb), (int)(bitmap.GetPixel(x + 1, y).B & msb));

            //return !((cpixel.GetHue() == upixel.GetHue()) && (cpixel.GetHue() == dpixel.GetHue()) && (cpixel.GetHue() == lpixel.GetHue()) && (cpixel.GetHue() == rpixel.GetHue()));
            return !(cpixel.Equals(upixel) && cpixel.Equals(dpixel) && cpixel.Equals(lpixel) && cpixel.Equals(rpixel));
        }
        else { return true; }
    }

    public static int GetImageCapacity(Bitmap bitmap, bool stealthy = true, Stealthiness stealthValue = Stealthiness.Maximum)
    {
        int cnt = -1;
        try
        {
            for (int y = 1; y < bitmap.Height - 1; y++)
            {
                for (int x = 1; x < bitmap.Width - 1; x++)
                    if (IsValidPixel(bitmap, x, y, stealthy, stealthValue))
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