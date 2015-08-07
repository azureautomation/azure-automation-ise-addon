using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AutomationISE.Model;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for NewOrEditCredentialDialog.xaml
    /// </summary>
    public partial class NewOrEditCredentialDialog : Window
    {
        private string _username;
        private string _password;

        public string username { get { return _username; } }
        public string password { get { return _password; } }
        
        public NewOrEditCredentialDialog(AutomationCredential cred)
        {
            InitializeComponent();
            
            if (cred != null)
            {
                UsernameTextbox.Text = cred.getUsername();
                PasswordTextbox.Password = cred.getPassword();

                this.Title = "Edit Credential Asset";
            }
            else
            {
                this.Title = "New Credential Asset";
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _username = UsernameTextbox.Text;
            _password = PasswordTextbox.Password;

            this.DialogResult = true;
        }
    }
}
