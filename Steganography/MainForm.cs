using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Steganography.Properties;

namespace Steganography
{
    public partial class MainForm : Form
    {
        private readonly string[] SizeSuffixes = {"bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"};
        private string currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private string sourceImageFileName = "";
        private Stegano.Stealthiness stealthValue = Stegano.Stealthiness.Maximum;
        private bool stealthy = true;
        private bool validData;

        public MainForm()
        {
            InitializeComponent();
            comboBox1.DataSource = Enum.GetValues(typeof(Stegano.Stealthiness));
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void addButton_Click(object sender, EventArgs e)
        {
            try
            {
                toolStripStatusLabel.Text = "Select source image.";
                var fileData = new List<byte[]>();
                Bitmap sourceImage = null;
                Bitmap outImage = null;
                buttonPanel.Enabled = false;
                var imageFile = loadFile("Select Source Image File",
                    "Image Files (*.jpg, *.bmp, *.jpg, *.png)|*.jpg;*.bmp;*.jpg;*.png");
                if (string.IsNullOrEmpty(imageFile))
                {
                    toolStripStatusLabel.Text = "Operation aborted.";
                    buttonPanel.Enabled = true;
                    return;
                }

                sourceImage = new Bitmap(imageFile);
                pictureBox.Image = sourceImage;
                toolStripStatusLabel.Text = "Calculating storage capacity - Please wait...";
                statusStrip.Refresh();
                Thread.Sleep(50);
                loadingPicture.Visible = true;
                var capacity = Stegano.GetImageCapacity(sourceImage, stealthy, stealthValue);
                loadingPicture.Visible = false;

                if (capacity == -1)
                {
                    toolStripStatusLabel.Text = "ERROR: No stealthy pixels available!!";
                    throw new SteganoException("ERROR: Operation Aborted.");
                }

                capacity = capacity * 3 / 8;
                using (new CenterWinDialog(this))
                {
                    MessageBox.Show("This image can hold up to " + SizeSuffix(capacity) + " of information.",
                        "Image Capacity", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                pictureBox.Image = sourceImage;
                toolStripStatusLabel.Text = "Select file to embed into image.";
                var embedFile = loadFile("Select File To Embed into Image", "All Files (*.*)|*.*");
                if (string.IsNullOrEmpty(embedFile))
                {
                    toolStripStatusLabel.Text = "Operation aborted.";
                    buttonPanel.Enabled = true;
                    pictureBox.Image = Resources.corner_curl;
                    return;
                }

                var size = Convert.ToInt32(new FileInfo(embedFile).Length);
                if (size > capacity)
                {
                    toolStripStatusLabel.Text = "ERROR: Selected file will not fit!!";
                    throw new SteganoException("ERROR: Operation Aborted.");
                }

                fileData.Add(Encoding.ASCII.GetBytes(Path.GetFileName(embedFile)));
                fileData.Add(File.ReadAllBytes(embedFile));
                toolStripStatusLabel.Text = "Enter Password (No password = no encryption)";
                var password = getPassword();
                toolStripStatusLabel.Text = "Embedding file into image.";
                loadingPicture.Visible = true;
                outImage = Stegano.embedFileIntoImage(sourceImage, fileData, capacity, password, toolStripStatusLabel,
                    stealthy, stealthValue);
                loadingPicture.Visible = false;
                if (outImage != null)
                {
                    pictureBox.Image = outImage;
                    saveImageFile(outImage, imageFile);
                    toolStripStatusLabel.Text = "Task complete.";
                }

                buttonPanel.Enabled = true;
                pictureBox.Image = Resources.corner_curl;
            }
            catch (SteganoException ex)
            {
                loadingPicture.Visible = false;
                using (new CenterWinDialog(this))
                {
                    MessageBox.Show(this, ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                buttonPanel.Enabled = true;
                pictureBox.Image = Resources.corner_curl;
            }
            catch (Exception ex)
            {
                loadingPicture.Visible = false;
                using (new CenterWinDialog(this))
                {
                    MessageBox.Show(this, ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                buttonPanel.Enabled = true;
                pictureBox.Image = Resources.corner_curl;
            }
        }

        private void extractButton_Click(object sender, EventArgs e)
        {
            try
            {
                toolStripStatusLabel.Text = "Enter Password (No password = no encryption)";
                var fileData = new List<byte[]>();
                Bitmap sourceImage = null;
                buttonPanel.Enabled = false;
                if (string.IsNullOrEmpty(sourceImageFileName))
                    sourceImageFileName = loadFile("Open Steganographic Image File", "Image Files (*.png)|*.png");
                if (string.IsNullOrEmpty(sourceImageFileName))
                {
                    toolStripStatusLabel.Text = "Operation aborted.";
                    buttonPanel.Enabled = true;
                    pictureBox.Image = Resources.corner_curl;
                    return;
                }

                sourceImage = new Bitmap(sourceImageFileName);
                pictureBox.Image = sourceImage;
                Refresh();
                var password = getPassword();
                toolStripStatusLabel.Text = "Extracting data from image - Please wait...";
                statusStrip.Refresh();
                loadingPicture.Visible = true;
                fileData = Stegano.extractFileFromImage(sourceImage, password, toolStripStatusLabel, stealthy,
                    stealthValue);
                loadingPicture.Visible = false;
                if (fileData != null && fileData.Count == 2)
                {
                    var filebytes = fileData[1];
                    var filenamebytes = fileData[0];
                    var filename = Encoding.ASCII.GetString(filenamebytes);
                    if (filebytes != null)
                    {
                        toolStripStatusLabel.Text = "Task complete.";
                        saveDataFile("Save Extracted Data File", filename, filebytes);
                    }
                    else
                    {
                        toolStripStatusLabel.Text = "Data extraction failed!";
                    }
                }
                else
                {
                    toolStripStatusLabel.Text = "Data extraction failed!";
                }

                statusStrip.Refresh();
                sourceImageFileName = "";
                pictureBox.Image = Resources.corner_curl;
                buttonPanel.Enabled = true;
            }
            catch (SteganoException ex)
            {
                sourceImageFileName = "";
                loadingPicture.Visible = false;
                using (new CenterWinDialog(this))
                {
                    MessageBox.Show(this, ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                buttonPanel.Enabled = true;
                pictureBox.Image = Resources.corner_curl;
            }
            catch (Exception ex)
            {
                sourceImageFileName = "";
                loadingPicture.Visible = false;
                using (new CenterWinDialog(this))
                {
                    MessageBox.Show(this, ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                buttonPanel.Enabled = true;
                pictureBox.Image = Resources.corner_curl;
            }
        }

        private string loadFile(string title, string filter)
        {
            openFileDialog.FileName = "";
            openFileDialog.Filter = filter;
            openFileDialog.Title = title;
            openFileDialog.InitialDirectory = currentPath;
            using (new CenterWinDialog(this))
            {
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    currentPath = Path.GetDirectoryName(openFileDialog.FileName);
                    return openFileDialog.FileName;
                }
            }

            return null;
        }

        private void saveImageFile(Bitmap image, string filename)
        {
            saveFileDialog.FileName = Path.GetFileNameWithoutExtension(filename) + ".png";
            saveFileDialog.Filter = "*.png|*.png";
            saveFileDialog.Title = "Save Stegonographic Image";
            saveFileDialog.InitialDirectory = currentPath;
            using (new CenterWinDialog(this))
            {
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    image.Save(saveFileDialog.FileName, ImageFormat.Png);
                    currentPath = Path.GetDirectoryName(saveFileDialog.FileName);
                }
            }
        }


        private void saveDataFile(string title, string filename, byte[] data)
        {
            saveFileDialog.FileName = filename;
            saveFileDialog.Filter = "*.*|*.*";
            saveFileDialog.Title = title;
            using (new CenterWinDialog(this))
            {
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllBytes(saveFileDialog.FileName, data);
                    currentPath = Path.GetDirectoryName(saveFileDialog.FileName);
                }
            }
        }

        private string getPassword()
        {
            var passform = new PasswordForm();
            using (new CenterWinDialog(this))
            {
                passform.ShowDialog();
            }

            return passform.password;
        }

        private string getMaskedPassword(string password)
        {
            var s = new StringBuilder();
            if (password.Length <= 3)
            {
                for (var x = 0; x < password.Length; x++)
                    s.Append("*");
            }
            else
            {
                s.Append(password[0]);
                for (var x = 1; x < password.Length - 1; x++)
                    s.Append("*");
                s.Append(password[password.Length - 1]);
            }

            return s.ToString();
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            string filename;
            validData = GetFilename(out filename, e);
            e.Effect = DragDropEffects.None;
            if (validData)
                e.Effect = DragDropEffects.Copy;
        }

        protected bool GetFilename(out string filename, DragEventArgs e)
        {
            var ret = false;
            filename = string.Empty;

            if ((e.AllowedEffect & DragDropEffects.Copy) == DragDropEffects.Copy)
            {
                var data = e.Data.GetData("FileName") as Array;
                if (data != null)
                    if (data.Length == 1 && data.GetValue(0) is string)
                    {
                        filename = ((string[]) data)[0];
                        var ext = Path.GetExtension(filename).ToLower();
                        if (ext == ".png")
                            ret = true;
                    }
            }

            return ret;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string filename;
            validData = GetFilename(out filename, e);
            if (validData)
            {
                sourceImageFileName = filename;
                extractButton_Click(null, null);
            }
        }

        private void infoButton_Click(object sender, EventArgs e)
        {
            using (new CenterWinDialog(this))
            {
                MessageBox.Show(
                    "Steganography is the art or practice of concealing a message, image, or file within another message, image, or file.\n\nThis program is designed to hide a single data file of any type inside an image file. If a password is provided the data is encrypted using AES-256 bit encryption, prior to being embedded in the image. The integrity of the embedded file is checked using HMAC(SHA1) before the resultant steganographic image can be saved as a .png file.\n\nData can be extracted from images that were encoded with this program by either dragging and dropping the image onto the form or using the 'Extract File' button. The extracted data (if valid) can be saved under it's original filename.\n\nNOTE: Large images may take a long time to compute.",
                    "About");
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            var url = @"https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=3U8HXWTSQG62A";
            Process.Start(url);
        }

        private string SizeSuffix(long value)
        {
            var mag = (int) Math.Log(value, 1024);
            var adjustedSize = (decimal) value / (1L << (mag * 10));

            return string.Format("{0:n1} {1}", adjustedSize, SizeSuffixes[mag]);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            comboBox1.Enabled = checkBox1.Checked;
            stealthy = checkBox1.Checked;
        }

        private void comboBox1_SelectedValueChanged(object sender, EventArgs e)
        {
            stealthValue = (Stegano.Stealthiness) comboBox1.SelectedItem;
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            comboBox1.SelectedItem = stealthValue;
        }
    }
}