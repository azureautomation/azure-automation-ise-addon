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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.PowerShell.Host.ISE;
using Microsoft.Azure.Management.Automation.Models;
using AutomationISE.Model;
using System.Security;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.Subscriptions.Models;
using System.Threading;
using System.Windows.Threading;

using System.Diagnostics;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for AutomationISEControl.xaml
    /// </summary>
    public partial class AutomationISEControl : UserControl, IAddOnToolHostObject
    {
        private AutomationISEClient iseClient;
        private LocalRunbookStore runbookStore;
        public AutomationISEControl()
        {
            try
            {
                InitializeComponent();
                iseClient = new AutomationISEClient();
                runbookStore = new LocalRunbookStore();

                /* Determine working directory */
                String localWorkspace = Properties.Settings.Default["localWorkspace"].ToString();
                if (localWorkspace == "")
                {
                    String systemDrive = Environment.GetEnvironmentVariable("SystemDrive") + "\\";
                    localWorkspace = System.IO.Path.Combine(systemDrive, "AutomationWorkspace");
                    Properties.Settings.Default["localWorkspace"] = localWorkspace;
                    Properties.Settings.Default.Save();
                }
                iseClient.workspace = localWorkspace;

                /* Update UI */
                workspaceTextBox.Text = iseClient.workspace;
		        userNameTextBox.Text = Properties.Settings.Default["ADUserName"].ToString();
                assetsComboBox.Items.Add(AutomationAzure.Constants.assetVariable);
		        assetsComboBox.Items.Add(AutomationAzure.Constants.assetCredential);
                RefreshRunbookList.IsEnabled = false;
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

        public ObjectModelRoot HostObject
        {
            get;
            set;
        }

        private async void loginButton_Click(object sender, RoutedEventArgs e)
        {
            try {
		        //TODO: probably refactor this a little
                String UserName = userNameTextBox.Text;
                Properties.Settings.Default["ADUserName"] = UserName;
                Properties.Settings.Default.Save();

                UpdateStatusBox(configurationStatusTextBox, "Launching login window");
                iseClient.azureADAuthResult = AutomationAzure.AuthenticateHelper.GetInteractiveLogin(UserName);

                UpdateStatusBox(configurationStatusTextBox, Properties.Resources.RetrieveSubscriptions);
                IList<Subscription> subscriptions = await iseClient.GetSubscriptions();
                //TODO: what if there are no subscriptions? Does this still work?
                if (subscriptions.Count > 0)
                {
                    UpdateStatusBox(configurationStatusTextBox, Properties.Resources.FoundSubscriptions);
                    subscriptionComboBox.ItemsSource = subscriptions;
                    subscriptionComboBox.DisplayMemberPath = "DisplayName";
                    subscriptionComboBox.SelectedItem = subscriptionComboBox.Items[0];
                }
                else
                    UpdateStatusBox(configurationStatusTextBox, Properties.Resources.NoSubscriptions);
            }
            catch (Microsoft.IdentityModel.Clients.ActiveDirectory.AdalServiceException)
            {
                UpdateStatusBox(configurationStatusTextBox, Properties.Resources.CancelSignIn);
            }
            catch (Microsoft.IdentityModel.Clients.ActiveDirectory.AdalException)
            {
                UpdateStatusBox(configurationStatusTextBox, Properties.Resources.CancelSignIn);
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

        private void azureADTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private async void SubscriptionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                iseClient.currSubscription = (Subscription)subscriptionComboBox.SelectedValue;
                if (iseClient.currSubscription != null)
                {
                    UpdateStatusBox(configurationStatusTextBox, Properties.Resources.RetrieveAutomationAccounts);
                    IList<AutomationAccount> automationAccounts = await iseClient.GetAutomationAccounts();
                    accountsComboBox.ItemsSource = automationAccounts;
                    accountsComboBox.DisplayMemberPath = "Name";
                    if (accountsComboBox.HasItems)
                    {
                        UpdateStatusBox(configurationStatusTextBox, Properties.Resources.FoundAutomationAccounts);
                        accountsComboBox.SelectedItem = accountsComboBox.Items[0];
                    }
                    else UpdateStatusBox(configurationStatusTextBox, Properties.Resources.NoAutomationAccounts);
                }
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }

        }

        private async void accountsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                AutomationAccount account = (AutomationAccount)accountsComboBox.SelectedValue;
                iseClient.currAccount = account;
                if (account != null)
                {
                    /* Update Runbooks */
                    IList<Runbook> cloudRunbooks = await iseClient.GetRunbooks();
                    runbookStore.UpdateLocalRunbooks(cloudRunbooks);
                    /* Update UI */
                    RunbookslistView.ItemsSource = runbookStore.localRunbooks;
                    UpdateStatusBox(configurationStatusTextBox, "Selected automation account: " + account.Name);
                    RefreshRunbookList.IsEnabled = true;
                }
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }

        }

        private async void assetsListView_SelectionChanged(object sender, SelectionChangedEventArgs e) { } 

        private async void assetsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedAsset = assetsComboBox.SelectedValue;
                var automationAccount = (AutomationAzure.AutomationAccount)accountsComboBox.SelectedValue;
                if (selectedAsset.ToString() == AutomationAzure.Constants.assetVariable)
                {
                   assetsListView.ItemsSource = await automationAccount.GetAssetsOfType("AutomationVariable");
                }
                else if (selectedAsset.ToString() == AutomationAzure.Constants.assetCredential)
                {
                    assetsListView.ItemsSource = await automationAccount.GetAssetsOfType("AutomationCredential");
                }
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

        private void workspaceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //TODO: refactor this
            iseClient.workspace = workspaceTextBox.Text;
            Properties.Settings.Default["localWorkspace"] = iseClient.workspace;
            Properties.Settings.Default.Save();
        }
        private void workspaceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.SelectedPath = iseClient.workspace;
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                iseClient.workspace = dialog.SelectedPath;
                workspaceTextBox.Text = iseClient.workspace;

                UpdateStatusBox(configurationStatusTextBox, "Saving workspace location: " + iseClient.workspace);
                Properties.Settings.Default["localWorkspace"] = iseClient.workspace;
                Properties.Settings.Default.Save();
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

        private void configurationStatusTextBox_TextChanged(object sender, TextChangedEventArgs e) { }

        private void UpdateStatusBox(System.Windows.Controls.TextBox statusTextBox, String Message)
        {
            var dispatchMessage = Dispatcher.BeginInvoke(DispatcherPriority.Send, (SendOrPostCallback)delegate
            {
                statusTextBox.AppendText(Message + "\r\n");
            }, null);

            System.Windows.Forms.Application.DoEvents();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabItem selectedTab = (TabItem)((TabControl)sender).SelectedItem;
            switch (selectedTab.Name)
            {
                case "configurationTab":
                    break;
                case "runbookTab":
                    break;
                case "settingsTab":
                    break;
                default:
                    Debug.WriteLine("Couldn't find tab handler with name: " + selectedTab.Name);
                    return;
            }
        }

        private void userNameTextBox_TextChanged(object sender, TextChangedEventArgs e) { }

        private void RefreshRunbookList_Click(object sender, RoutedEventArgs e) { }
    }
}
