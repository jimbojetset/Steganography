using System;
using System.Windows.Forms;

namespace Steganography
{
    public partial class PasswordForm : Form
    {
        private string pass = "";

        public PasswordForm()
        {
            InitializeComponent();
            passwordTextBox.Focus();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            pass = passwordTextBox.Text;
            this.Close();
        }

        public string password
        {
            get { return pass; }
        }
    }
}