using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AutomationISE.Model;
using System.Security.Cryptography.X509Certificates;
using System.IO;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for NewOrEditCertificateDialog.xaml
    /// </summary>
    public partial class NewOrEditCertificateDialog : Window
    {
        private string _certPath;
        private string _password;
        private string _thumbprint;
        private bool _encrypted;
        private bool _exportable;
        private string _certBase64;

        public string certPath { get { return _certPath; } }
        public string thumbprint { get { return _thumbprint; } }
        public string password { get { return _password; } }
        public bool encrypted { get { return _encrypted; } }
        public bool exportable { get { return _exportable; } }
        public string certBase64 { get { return _certBase64; } }

        public NewOrEditCertificateDialog(AutomationCertificate cert)
        {
            try
            {
                InitializeComponent();
                exportableComboBox.Items.Clear();
                exportableComboBox.Items.Add(true);
                exportableComboBox.Items.Add(false);
                exportableComboBox.SelectedItem = true;

                if (cert != null)
                {
                    PasswordTextbox.Password = cert.getPassword();
                    certificatePathTextbox.Text = cert.getCertPath();
                    exportableComboBox.SelectedItem = cert.getExportable();

                    // If certificate is a .cer file, grey out the password & exportable
                    if (Path.GetExtension(_certPath) == ".cer")
                    {
                        PasswordTextbox.Password = null;
                        exportableComboBox.SelectedItem = false;
                        this.PasswordTextbox.IsEnabled = false;
                        this.exportableComboBox.IsEnabled = false;
                        _encrypted = false;
                    }
                    else
                    {
                        this.PasswordTextbox.IsEnabled = true;
                        this.exportableComboBox.IsEnabled = true;
                        _encrypted = true;
                    }

                    this.Title = "Edit Certificate Asset";
                }
                else
                {
                    this.Title = "New Certificate Asset";
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _password = PasswordTextbox.Password;
            _exportable = bool.Parse(exportableComboBox.SelectedItem.ToString());
            _certPath = certificatePathTextbox.Text;
            _thumbprint = importCertificate();
            this.DialogResult = true;
        }

        private void buttonCertificate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            _certPath = dialog.FileName;

            this.certificatePathTextbox.Text = _certPath;

            // If certificate is a .cer file, grey out the password & exportable
            if (Path.GetExtension(_certPath) == ".cer")
            {
                PasswordTextbox.Password = null;
                exportableComboBox.SelectedItem = exportableComboBox.Items[1];
                this.PasswordTextbox.IsEnabled = false;
                this.exportableComboBox.IsEnabled = false;
                _encrypted = false;
            }
            else
            {
                this.PasswordTextbox.IsEnabled = true;
                this.exportableComboBox.IsEnabled = true;
                _encrypted = true;
            }
        }

        private string importCertificate()
        {
            // Load the certificate into the users current store
            X509Certificate2 cert = new X509Certificate2();
            if (Path.GetExtension(_certPath) == ".cer")
            {
                cert.Import(_certPath);
            }
            else
            {
                if (_exportable) cert.Import(_certPath, _password, X509KeyStorageFlags.Exportable);
                else cert.Import(_certPath, _password, X509KeyStorageFlags.DefaultKeySet);
            }
            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
            store.Close();
            _certBase64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));

            return cert.Thumbprint;
        }
        private void exportableComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
