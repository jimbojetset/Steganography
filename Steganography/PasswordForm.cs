using System;
using System.Windows.Forms;

namespace Steganography
{
    public partial class PasswordForm : Form
    {
        public PasswordForm()
        {
            InitializeComponent();
            passwordTextBox.Focus();
        }

        public string password { get; private set; } = "";

        private void okButton_Click(object sender, EventArgs e)
        {
            password = passwordTextBox.Text;
            Close();
        }
    }
}