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
using System.IO;
using System.Threading;
using System.Windows.Threading;
//using System.Windows.Forms;

using System.Diagnostics;
using System.Timers;

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
                
                assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetVariable);
		        assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetCredential);
                //assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetCertificate);
                //assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetConnection);

                RefreshRunbookList.IsEnabled = false;
                RefreshAssetList.IsEnabled = false;

                startContinualGet();
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

        public void startContinualGet() {
            var myTimer = new System.Timers.Timer();

            // Set timer interval to 30 seconds
            myTimer.Interval = 30000;

            // Set the function to run when timer fires
            myTimer.Elapsed += new ElapsedEventHandler(refresh);

            myTimer.Start();
        }

        public void refresh(object source, ElapsedEventArgs e) {
            this.Dispatcher.Invoke((Action)(() =>
            {
                refreshAssets();
            }));
        }
        
        public async void refreshAssets()
        {
            try
            {
                string selectedAssetType = (string)assetsComboBox.SelectedValue;
                if (selectedAssetType == null)
                {
                    selectedAssetType = "";
                }

                if (selectedAssetType == AutomationISE.Model.Constants.assetVariable)
                {
                    assetsListView.ItemsSource = await iseClient.GetAssetsOfType("AutomationVariable");
                }
                else if (selectedAssetType == AutomationISE.Model.Constants.assetCredential)
                {
                    assetsListView.ItemsSource = await iseClient.GetAssetsOfType("AutomationCredential");
                }
                else if (selectedAssetType == AutomationISE.Model.Constants.assetConnection)
                {
                    assetsListView.ItemsSource = await iseClient.GetAssetsOfType("AutomationConnection");
                }
                else if (selectedAssetType == AutomationISE.Model.Constants.assetCertificate)
                {
                    assetsListView.ItemsSource = await iseClient.GetAssetsOfType("AutomationCertificate");
                }
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

        private async void loginButton_Click(object sender, RoutedEventArgs e)
        {
            try {
		        //TODO: probably refactor this a little
                String UserName = userNameTextBox.Text;
                Properties.Settings.Default["ADUserName"] = UserName;
                Properties.Settings.Default.Save();

                UpdateStatusBox(configurationStatusTextBox, "Launching login window");
                iseClient.azureADAuthResult = AutomationISE.Model.AuthenticateHelper.GetInteractiveLogin(UserName);

                UpdateStatusBox(configurationStatusTextBox, Properties.Resources.RetrieveSubscriptions);
                IList<Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse.Subscription> subscriptions = await iseClient.GetSubscriptions();
                //TODO: what if there are no subscriptions? Does this still work?
                if (subscriptions.Count > 0)
                {
                    UpdateStatusBox(configurationStatusTextBox, Properties.Resources.FoundSubscriptions);
                    subscriptionComboBox.ItemsSource = subscriptions;
                    subscriptionComboBox.DisplayMemberPath = "SubscriptionName";
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
                iseClient.currSubscription = (Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse.Subscription)subscriptionComboBox.SelectedValue;
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
                    /* Update Status */
                    UpdateStatusBox(configurationStatusTextBox, "Selected automation account: " + account.Name);
                    UpdateStatusBox(configurationStatusTextBox, "Getting runbook data...");
                    /* Update Runbooks */
                    ISet<AutomationRunbook> runbooks = await AutomationRunbookManager.GetAllRunbookMetadata(iseClient.automationManagementClient, 
                        iseClient.workspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
                    /* Update UI */
                    RunbooksListView.ItemsSource = runbooks;
                    UpdateStatusBox(configurationStatusTextBox, "Done getting runbook data");
                    RefreshAssetList.IsEnabled = true;

                    //TODO: what's the reasoning here?
                    if (!iseClient.AccountWorkspaceExists())
                    {
                        UpdateStatusBox(configurationStatusTextBox, "Downloading assets..."); 
                        await iseClient.DownloadAllAssets();
                        UpdateStatusBox(configurationStatusTextBox, "Assets downloaded"); 
                    }
                    //TODO: and here?
                    refresh(null, null);
                }
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }

        }

        private async void assetsListView_SelectionChanged(object sender, SelectionChangedEventArgs e) { } 

        private void assetsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            refreshAssets();
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
                case "feedbackTab":
                    surveyBrowserControl.Navigate(new Uri(Constants.feedbackURI));
                    break;
                default:
                    Debug.WriteLine("Couldn't find tab handler with name: " + selectedTab.Name);
                    return;
            }
        }

        private void userNameTextBox_TextChanged(object sender, TextChangedEventArgs e) { }

        private void DownloadAsset_Click(object sender, RoutedEventArgs e) { }

        private void RefreshAssetList_Click(object sender, RoutedEventArgs e)
        {
            refreshAssets();
        }

        private void RefreshRunbookList_Click(object sender, RoutedEventArgs e) { }

        private bool ConfirmRunbookDownload()
        {
            String message = "Are you sure you want to import the cloud's copy of this runbook?\nAny changes you have made to it locally will be overwritten.";
            String header = "Download Runbook";
            System.Windows.Forms.DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(message, header, System.Windows.Forms.MessageBoxButtons.YesNo);
            if (dialogResult == System.Windows.Forms.DialogResult.Yes)
            {
                return true;
            }
            return false;
        }

        //Issues with this...debug
        private void CloseRunbookInISE(AutomationRunbook runbook)
        {
            ISEFileCollection currentlyOpenFiles = HostObject.CurrentPowerShellTab.Files;
            //If the user already has a local copy of that file open, then close it
            for (int i = 0; i < currentlyOpenFiles.Count; i++)
            {
                if (currentlyOpenFiles[i].FullPath.Equals(runbook.localFileInfo.FullName))
                {
                    currentlyOpenFiles.RemoveAt(i);
                    break; //note that the file can only be open once (ISE behavior)
                }
            }
        }

        private async void DownloadRunbook_Click(object sender, RoutedEventArgs e)
        {
            DownloadRunbook.IsEnabled = false;
            DownloadRunbook.Content = "Downloading...";
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            if (selectedRunbook.localFileInfo != null && File.Exists(selectedRunbook.localFileInfo.FullName) && !ConfirmRunbookDownload())
            {
                DownloadRunbook.IsEnabled = true;
                return;
            }
            //CloseRunbookInISE(selectedRunbook);
            await AutomationRunbookManager.DownloadRunbook(selectedRunbook, iseClient.automationManagementClient,
                        iseClient.workspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
            RunbooksListView.Items.Refresh(); //Proper binding might be better
            DownloadRunbook.Content = "Download";
            DownloadRunbook.IsEnabled = true;
            OpenRunbook.IsEnabled = true;
            PublishRunbook.IsEnabled = true;
        }

        private void RunbooksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            if (selectedRunbook.localFileInfo != null && File.Exists(selectedRunbook.localFileInfo.FullName))
            {
                OpenRunbook.IsEnabled = true;
                PublishRunbook.IsEnabled = true;
            }
            DownloadRunbook.IsEnabled = true;
        }

        private void OpenRunbook_Click(object sender, RoutedEventArgs e)
        {
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            ISEFileCollection currentlyOpenFiles = HostObject.CurrentPowerShellTab.Files;
            currentlyOpenFiles.Add(selectedRunbook.localFileInfo.FullName);
        }

        private async void PublishRunbook_Click(object sender, RoutedEventArgs e)
        {
            PublishRunbook.IsEnabled = false;
            DownloadRunbook.IsEnabled = false;
            UploadRunbook.IsEnabled = false;
            StartRunbook.IsEnabled = false;
            PublishRunbook.Content = "Publishing...";
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            //TODO (?): Check if you are overwriting draft content in the cloud
            await AutomationRunbookManager.UploadRunbookAsDraft(selectedRunbook, iseClient.automationManagementClient,
                        iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
            await AutomationRunbookManager.PublishRunbook(selectedRunbook, iseClient.automationManagementClient,
                        iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
            PublishRunbook.IsEnabled = true;
            DownloadRunbook.IsEnabled = true;
            UploadRunbook.IsEnabled = true;
            StartRunbook.IsEnabled = true;
            PublishRunbook.Content = "Publish";
        }
    }
}
