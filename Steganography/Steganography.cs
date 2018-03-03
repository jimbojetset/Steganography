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
    private static readonly SymmetricAlgorithm _algorithm = new RijndaelManaged();

    public static Bitmap embedFileIntoImage(Bitmap bitmapImage, List<byte[]> filedata, int capacity = -1,
        string password = "", ToolStripStatusLabel toolStripStatusLabel = null, bool stealthy = true,
        Stealthiness stealthValue = Stealthiness.Maximum)
    {
        try
        {
            var imagewidth = bitmapImage.Width - 1;
            var imageheight = bitmapImage.Height - 1;
            var filebytes = filedata[1];
            var filehash = "";
            writeOut(toolStripStatusLabel, "Encoding file into image - Please wait...");
            // Calculate pre-encoded SHA1 Hash of File Data
            using (var sha1 = new SHA1CryptoServiceProvider())
            {
                filehash = Convert.ToBase64String(sha1.ComputeHash(filebytes));
            }

            // Encrypt File Data (bytes)
            if (!string.IsNullOrEmpty(password))
                filebytes = encryptData(filebytes, password);
            // Encrypt Filename (bytes)
            var filenamebytes = filedata[0];
            if (!string.IsNullOrEmpty(password))
                filenamebytes = encryptData(filenamebytes, password);
            // Convert Filename to binary
            var filename = string.Empty;
            foreach (var b in filenamebytes)
                filename += Convert.ToString(b, 2).PadLeft(8, '0');
            // Convert length of filename (in bytes) into binary
            var filenamelength = Convert.ToString(filename.Length / 8, 2).PadLeft(8, '0');
            // Convert length of File Data (in bytes) into binary
            var filelength = Convert.ToString(filebytes.Length, 2).PadLeft(24, '0');
            // Build binary string
            var sfinal = new StringBuilder();
            sfinal.Append(filenamelength);
            sfinal.Append(filelength);
            sfinal.Append(filename);
            foreach (var b in filebytes)
                sfinal.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
            sfinal.Append("000"); //buffer
            // Check binary data will fit
            var msgLen = (sfinal.Length + 16) / 8;
            var pixCnt = capacity;
            if (capacity < 0)
                pixCnt = (int) Math.Floor((double) (GetImageCapacity(bitmapImage, stealthy, stealthValue) * 3 / 8));
            // Calculate bit distribution throughout image (jump value)
            //if(pixCnt)
            var jmp = 0;
            if (pixCnt > msgLen)
                jmp = (int) Math.Floor((double) (pixCnt / msgLen));
            if (jmp > 500) jmp = 500;
            if (jmp == 0) jmp = 1;
            var jump = Convert.ToString(jmp, 2).PadLeft(9, '0');
            // Insert 'jump' value into first three pixels
            for (var x = 0; x < 9; x += 3)
            {
                var pixel = bitmapImage.GetPixel(x, 0);
                var r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(0, 7) + jump[x];
                var g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(0, 7) + jump[x + 1];
                var b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(0, 7) + jump[x + 2];
                bitmapImage.SetPixel(x, 0,
                    Color.FromArgb(Convert.ToInt32(r, 2), Convert.ToInt32(g, 2), Convert.ToInt32(b, 2)));
            }

            var streamPos = 0;
            var pixelPos = 0;
            // Insert all binary data into image
            if (pixCnt > msgLen)
            {
                for (var y = 1; y < imageheight - 1; y++)
                {
                    for (var x = 1; x < imagewidth - 1; x++)
                        if (streamPos < sfinal.Length - 3)
                            if (IsValidPixel(bitmapImage, x, y, stealthy, stealthValue))
                            {
                                if (pixelPos % jmp == 0)
                                {
                                    var pixel = bitmapImage.GetPixel(x, y);
                                    var r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(0, 7) +
                                            sfinal[streamPos];
                                    var g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(0, 7) +
                                            sfinal[streamPos + 1];
                                    var b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(0, 7) +
                                            sfinal[streamPos + 2];
                                    bitmapImage.SetPixel(x, y,
                                        Color.FromArgb(Convert.ToInt32(r, 2), Convert.ToInt32(g, 2),
                                            Convert.ToInt32(b, 2)));
                                    streamPos += 3;
                                }

                                pixelPos++;
                            }

                    Application.DoEvents();
                }

                writeOut(toolStripStatusLabel, "Encoding Complete.");
            }
            else
            {
                writeOut(toolStripStatusLabel, "Image is too small to hold the specified file!");
                throw new SteganoException("Image is too small to hold the specified file!");
            }

            writeOut(toolStripStatusLabel, "Checking integrity of embedded file - Please wait...");
            // Extract data from image (and decrypt if necessary)
            var checkdata = extractFileFromImage(bitmapImage, password, toolStripStatusLabel, stealthy, stealthValue);
            var checkhash = "";
            // Calculate SHA1 hash of extracted data
            if (checkdata != null && checkdata.Count == 2)
                using (var sha1 = new SHA1CryptoServiceProvider())
                {
                    checkhash = Convert.ToBase64String(sha1.ComputeHash(checkdata[1]));
                }

            // Check extracted SHA1 hash with pre-encoded SHA1 hash
            if (checkhash == filehash && checkhash != "" && filehash != "")
            {
                writeOut(toolStripStatusLabel, "Integrity check PASSED.");
                return bitmapImage;
            }

            writeOut(toolStripStatusLabel, "Integrity check FAILED!");
            throw new SteganoException("Failed integrity check!");
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

    public static List<byte[]> extractFileFromImage(Bitmap sourceImage, string password = "",
        ToolStripStatusLabel toolStripStatusLabel = null, bool stealthy = true,
        Stealthiness stealthValue = Stealthiness.Maximum)
    {
        try
        {
            // Extract jump value from first 3 pixels
            var sb = new StringBuilder();
            for (var x = 0; x < 9; x += 3)
            {
                var pixel = sourceImage.GetPixel(x, 0);
                var r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(7, 1);
                var g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(7, 1);
                var b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(7, 1);
                sb.Append(r + g + b);
            }

            var jump = Convert.ToInt32(sb.ToString(0, 9), 2);
            var imagewidth = sourceImage.Width - 1;
            var imageheight = sourceImage.Height - 1;
            var sbfinal = new StringBuilder();
            var pixelPos = 0;
            // Extract ALL lsb's from image using jump value
            for (var y = 1; y < imageheight - 1; y++)
            for (var x = 1; x < imagewidth - 1; x++)
                if (IsValidPixel(sourceImage, x, y, stealthy, stealthValue))
                {
                    if (pixelPos % jump == 0)
                    {
                        var pixel = sourceImage.GetPixel(x, y);
                        var r = Convert.ToString(pixel.R, 2).PadLeft(8, '0').Substring(7, 1);
                        var g = Convert.ToString(pixel.G, 2).PadLeft(8, '0').Substring(7, 1);
                        var b = Convert.ToString(pixel.B, 2).PadLeft(8, '0').Substring(7, 1);
                        sbfinal.Append(r + g + b);
                    }

                    pixelPos++;
                }

            // Get filename length (in bytes) value from first 8 bits
            var filenamelength = Convert.ToInt32(sbfinal.ToString(0, 8), 2);
            // Get file data length (in bytes) value from next 24 bits
            var filesize = Convert.ToInt32(sbfinal.ToString(8, 24), 2);
            // Get filename
            var filenamebytes = GetBytesFromBinaryString(sbfinal.ToString(32, filenamelength * 8));
            // Decrypt filename (if necessary)
            if (!string.IsNullOrEmpty(password))
                filenamebytes = decryptData(filenamebytes, password);
            // Check for valid filename
            if (!IsASCII(filenamebytes))
                throw new SteganoException("Bad Decrypt");
            // Extract remaining file data
            var filebytes = GetBytesFromBinaryString(sbfinal.ToString(32 + filenamelength * 8, filesize * 8));
            // Decrypt file data (if necessary)
            if (!string.IsNullOrEmpty(password))
                filebytes = decryptData(filebytes, password);
            var data = new List<byte[]>();
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

    private static byte[] GetBytesFromBinaryString(string binary)
    {
        var list = new List<byte>();
        for (var i = 0; i < binary.Length; i += 8)
        {
            var t = binary.Substring(i, 8);
            list.Add(Convert.ToByte(t, 2));
        }

        return list.ToArray();
    }

    private static byte[] encryptData(byte[] data, string password)
    {
        getKey(password);
        var encryptor = _algorithm.CreateEncryptor();
        var cryptoData = encryptor.TransformFinalBlock(data, 0, data.Length);
        return cryptoData;
    }

    private static byte[] decryptData(byte[] cryptoData, string password)
    {
        getKey(password);
        var decryptor = _algorithm.CreateDecryptor();
        var data = decryptor.TransformFinalBlock(cryptoData, 0, cryptoData.Length);
        return data;
    }

    private static void getKey(string password)
    {
        var salt = new byte[8];
        var passwordBytes = Encoding.ASCII.GetBytes(password);
        var length = Math.Min(passwordBytes.Length, salt.Length);
        for (var i = 0; i < length; i++)
            salt[i] = passwordBytes[i];
        var key = new Rfc2898DeriveBytes(password, salt);
        _algorithm.Key = key.GetBytes(_algorithm.KeySize / 8);
        _algorithm.IV = key.GetBytes(_algorithm.BlockSize / 8);
    }

    private static bool IsASCII(byte[] bytes)
    {
        var value = Encoding.UTF8.GetString(bytes);
        return Encoding.UTF8.GetByteCount(value) == value.Length;
    }

    private static bool IsValidPixel(Bitmap bitmap, int x, int y, bool stealthy = true,
        Stealthiness stealthValue = Stealthiness.Maximum)
    {
        Application.DoEvents();
        if (stealthy)
        {
            // looks above, below, left & right of current pixel and returns false if 5 msb's of all 5 pixels match (ie all virtually the same color)
            var msb = (int) stealthValue;
            var cpixel = Color.FromArgb(bitmap.GetPixel(x, y).R & msb, bitmap.GetPixel(x, y).G & msb,
                bitmap.GetPixel(x, y).B & msb);
            var upixel = Color.FromArgb(bitmap.GetPixel(x, y - 1).R & msb, bitmap.GetPixel(x, y - 1).G & msb,
                bitmap.GetPixel(x, y - 1).B & msb);
            var dpixel = Color.FromArgb(bitmap.GetPixel(x, y + 1).R & msb, bitmap.GetPixel(x, y + 1).G & msb,
                bitmap.GetPixel(x, y + 1).B & msb);
            var lpixel = Color.FromArgb(bitmap.GetPixel(x - 1, y).R & msb, bitmap.GetPixel(x - 1, y).G & msb,
                bitmap.GetPixel(x - 1, y).B & msb);
            var rpixel = Color.FromArgb(bitmap.GetPixel(x + 1, y).R & msb, bitmap.GetPixel(x + 1, y).G & msb,
                bitmap.GetPixel(x + 1, y).B & msb);

            //return !((cpixel.GetHue() == upixel.GetHue()) && (cpixel.GetHue() == dpixel.GetHue()) && (cpixel.GetHue() == lpixel.GetHue()) && (cpixel.GetHue() == rpixel.GetHue()));
            return !(cpixel.Equals(upixel) && cpixel.Equals(dpixel) && cpixel.Equals(lpixel) && cpixel.Equals(rpixel));
        }

        return true;
    }

    public static int GetImageCapacity(Bitmap bitmap, bool stealthy = true,
        Stealthiness stealthValue = Stealthiness.Maximum)
    {
        var cnt = -1;
        try
        {
            for (var y = 1; y < bitmap.Height - 1; y++)
            {
                for (var x = 1; x < bitmap.Width - 1; x++)
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

    internal enum Stealthiness
    {
        Maximum = 0xF8,
        Medium = 0xFC,
        Minimum = 0xFE
    }
}