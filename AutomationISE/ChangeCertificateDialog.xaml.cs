
// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace AutomationISE
{
    /// <summary>
    /// Dialog window to get the udpated certificate from the user
    /// </summary>
    public partial class ChangeCertificateDialog : Window
    {
        private string _updatedThumbprint = null;

        public string updatedThumbprint { get { return _updatedThumbprint; } }

        public ChangeCertificateDialog(string thumbprint)
        {
            InitializeComponent();
            _updatedThumbprint = thumbprint;
            browseCertificateButton.Focus();
        }

        private void OKbutton_Click(object sender, RoutedEventArgs e)
        {
            _updatedThumbprint = ThumbprinttextBox.Text;
            this.DialogResult = true;
        }

        private void browseCertificateButton_Click(object sender, RoutedEventArgs e)
        {
            var userStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            userStore.Open(OpenFlags.ReadOnly);
            var selectedCertificate = X509Certificate2UI.SelectFromCollection(
                userStore.Certificates,
                "Current user certificate store",
                "Select certificate to use",
                X509SelectionFlag.SingleSelection);
            if (selectedCertificate.Count > 0)
            {
                ThumbprinttextBox.Text = selectedCertificate[0].Thumbprint;
            }
        }
    }
}
