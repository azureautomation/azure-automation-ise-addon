using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AutomationISE.Model;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for ChooseNewAssetTypeDialog.xaml
    /// </summary>
    public partial class ChooseNewAssetTypeDialog : Window
    {
        private string _newAssetType;
        private string _newAssetName;

        public string newAssetType { get { return _newAssetType; } }
        public string newAssetName { get { return _newAssetName; } }

        public ChooseNewAssetTypeDialog()
        {
            InitializeComponent();
            
            assetTypeComboBox.Items.Add(AutomationISE.Model.Constants.assetVariable);
            assetTypeComboBox.Items.Add(AutomationISE.Model.Constants.assetCredential);
            assetTypeComboBox.Items.Add(AutomationISE.Model.Constants.assetConnection);
            //assetTypeComboBox.Items.Add(AutomationISE.Model.Constants.assetCertificate);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _newAssetType = (string)assetTypeComboBox.SelectedValue;
            _newAssetName = assetName.Text;
            this.DialogResult = true;
        }
    }
}
