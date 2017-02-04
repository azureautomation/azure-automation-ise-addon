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

using AutomationISE.Model;
using Microsoft.Azure.Management.Storage.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for StorageAccountForModulesDialog.xaml
    /// </summary>
    public partial class StorageAccountForModulesDialog : Window
    {
        IEnumerable<StorageAccount> storageAccounts;
        public String storageAccountName;
        public String storageSubID;
        public String storageResourceGroupName;
        public String region;
        public String authority = null;
        public bool createNewStorageAccount = false;

        public StorageAccountForModulesDialog()
        {
            InitializeComponent();
        }

        private async void subscriptionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var subObject = (AutomationISEClient.SubscriptionObject) subscriptionComboBox.SelectedItem;
            var azureARMAuthResult = AuthenticateHelper.RefreshTokenByAuthority(subObject.Authority);
            authority = subObject.Authority;
            var authToken = azureARMAuthResult.AccessToken;
            var token = new Microsoft.Rest.TokenCredentials(authToken);
            var storageManagementClient = new Microsoft.Azure.Management.Storage.StorageManagementClient(token);
            storageManagementClient.SubscriptionId = subObject.SubscriptionId;
            resourceGroupcomboBox.Items.Clear();
            try
            {
                storageAccounts = storageManagementClient.StorageAccounts.List();
                foreach (var storageAccount in storageAccounts)
                {
                    var startPosition = storageAccount.Id.IndexOf("/resourceGroups/");
                    var endPosition = storageAccount.Id.IndexOf("/", startPosition + 16);
                    var resourceGroup = storageAccount.Id.Substring(startPosition + 16, endPosition - startPosition - 16);
                    if (resourceGroupcomboBox.Items.IndexOf(resourceGroup) == -1)
                    {
                        resourceGroupcomboBox.Items.Add(resourceGroup);
                    }
                }
                var cloudtoken = AuthenticateHelper.RefreshTokenByAuthority(authority);
                var subscriptionCreds = new TokenCloudCredentials(((AutomationISEClient.SubscriptionObject)subscriptionComboBox.SelectedItem).SubscriptionId, cloudtoken.AccessToken);
                var resourceManagementClient = new Microsoft.Azure.Subscriptions.SubscriptionClient(subscriptionCreds);
                CancellationToken cancelToken = new CancellationToken();
                var subscriptionRegions = await resourceManagementClient.Subscriptions.ListLocationsAsync(((AutomationISEClient.SubscriptionObject)subscriptionComboBox.SelectedItem).SubscriptionId, cancelToken);
                regionComboBox.ItemsSource = subscriptionRegions.Locations;
                regionComboBox.DisplayMemberPath = "Name";

            }
            catch (Exception Ex)
            {
                throw Ex;
            }
        }

        private void resourceGroupcomboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            CollectionView storageAccountcomboBoxView = (CollectionView)CollectionViewSource.GetDefaultView(storageAccounts);
            storageAccountcomboBox.ItemsSource = storageAccounts;
            storageAccountcomboBox.DisplayMemberPath = "Name";
            storageAccountcomboBoxView.Filter = item =>
            {
                StorageAccount storageItem = item as StorageAccount;
                if (storageItem == null || resourceGroupcomboBox.SelectedItem == null) return false;
                return storageItem.Id.Contains("/" + resourceGroupcomboBox.SelectedItem.ToString() + "/");
            };
        }

        private async void OKbutton_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (!String.IsNullOrEmpty(subscriptionComboBox.Text) && !String.IsNullOrEmpty(resourceGroupcomboBox.Text) && !String.IsNullOrEmpty(storageAccountcomboBox.Text))
                {
                    if (storageAccountcomboBox.SelectedItem == null)
                    {
                        var subObject = (AutomationISEClient.SubscriptionObject)subscriptionComboBox.SelectedItem;
                        var azureARMAuthResult = AuthenticateHelper.RefreshTokenByAuthority(subObject.Authority);
                        var authToken = azureARMAuthResult.AccessToken;
                        var token = new Microsoft.Rest.TokenCredentials(authToken);
                        var storageManagementClient = new Microsoft.Azure.Management.Storage.StorageManagementClient(token);
                        storageManagementClient.SubscriptionId = subObject.SubscriptionId;
                        var result = await storageManagementClient.StorageAccounts.CheckNameAvailabilityAsync(storageAccountcomboBox.Text);
                        if (!(result.NameAvailable.Value))
                        {
                            var messageBoxResult = System.Windows.Forms.MessageBox.Show(
                            "Storage account name is not available in this subscription. Please choose another name", "Name not available",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                        }
                        else
                        {
                            createNewStorageAccount = true;
                            storageSubID = ((AutomationISEClient.SubscriptionObject)subscriptionComboBox.SelectedItem).SubscriptionId;
                            storageResourceGroupName = resourceGroupcomboBox.Text;
                            storageAccountName = storageAccountcomboBox.Text;
                            region = regionComboBox.Text;
                            this.DialogResult = true;
                            this.Close();
                        }
                    }
                    else
                    {
                        storageSubID = ((AutomationISEClient.SubscriptionObject)subscriptionComboBox.SelectedItem).SubscriptionId;
                        storageResourceGroupName = resourceGroupcomboBox.SelectedItem.ToString();
                        storageAccountName = storageAccountcomboBox.Text;
                        this.DialogResult = true;
                        this.Close();
                    }
                }
            }
            catch (Exception Ex)
            {
                throw Ex;
            }
        }

        private void storageAccountcomboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            regionComboBox.IsEnabled = true;

            if (storageAccountcomboBox.SelectedItem != null)
            {
                regionComboBox.Text = ((Microsoft.Azure.Management.Storage.Models.StorageAccount)storageAccountcomboBox.SelectedItem).PrimaryLocation;
                regionComboBox.IsEnabled = false;
            }
        }
    }
}
