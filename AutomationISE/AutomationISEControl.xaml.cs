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
using System.IO;
using System.Threading;
using System.Windows.Threading;
using System.Linq;
using System.Diagnostics;
using System.Timers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using System.ComponentModel;
using System.Windows.Media.Animation;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for AutomationISEControl.xaml
    /// </summary>
    public partial class AutomationISEControl : UserControl, IAddOnToolHostObject
    {
        private System.Timers.Timer refreshAccountDataTimer;
        private System.Timers.Timer refreshAuthTokenTimer;
        private AutomationISEClient iseClient;
        private ObservableCollection<AutomationRunbook> runbookListViewModel;
        private ObservableCollection<AutomationAsset> assetListViewModel;
        private ListSortDirection runbookCurrSortDir;
        private string runbookCurrSortProperty;
        private ListSortDirection assetCurrSortDir;
        private string assetCurrSortProperty;
        private int numBackgroundTasks = 0;
        private Object backgroundWorkLock;
        private Storyboard progressSpinnerStoryboard;
        private Storyboard progressSpinnerStoryboardReverse;
        private Storyboard miniProgressSpinnerStoryboard;
        private Storyboard miniProgressSpinnerStoryboardReverse;
        private string certificateThumbprint;
        public ObjectModelRoot HostObject { get; set; }

        public AutomationISEControl()
        {
            try
            {
                InitializeComponent();
                iseClient = new AutomationISEClient();
                /* Spinner animation stuff */
                backgroundWorkLock = new Object();
                progressSpinnerStoryboard = (Storyboard)FindResource("bigGearRotationStoryboard");
                progressSpinnerStoryboardReverse = (Storyboard)FindResource("bigGearRotationStoryboardReverse");
                miniProgressSpinnerStoryboard = (Storyboard)FindResource("smallGearRotationStoryboard");
                miniProgressSpinnerStoryboardReverse = (Storyboard)FindResource("smallGearRotationStoryboardReverse");

                /* Determine working directory */
                String localWorkspace = Properties.Settings.Default["localWorkspace"].ToString();
                if (localWorkspace == "")
                {
                    String systemDrive = Environment.GetEnvironmentVariable("SystemDrive") + "\\";
                    localWorkspace = System.IO.Path.Combine(systemDrive, "AutomationWorkspace");
                    Properties.Settings.Default["localWorkspace"] = localWorkspace;
                    Properties.Settings.Default.Save();
                }
                iseClient.baseWorkspace = localWorkspace;

                /* Initialize Timers */
                refreshAccountDataTimer = new System.Timers.Timer();
                refreshAccountDataTimer.Interval = 30000; //30 seconds
                refreshAccountDataTimer.Elapsed += new ElapsedEventHandler(refreshAccountData);

                refreshAuthTokenTimer = new System.Timers.Timer();
                refreshAuthTokenTimer.Interval = Constants.tokenRefreshInterval * 60000;
                refreshAuthTokenTimer.Elapsed += new ElapsedEventHandler(refreshAuthToken);

                /* Update UI */
                workspaceTextBox.Text = iseClient.baseWorkspace;
                userNameTextBox.Text = Properties.Settings.Default["ADUserName"].ToString();
                subscriptionComboBox.IsEnabled = false;
                accountsComboBox.IsEnabled = false;

                assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetVariable);
                assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetCredential);
                //assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetCertificate);
                //assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetConnection);

                setRunbookAndAssetNonSelectionButtonState(false);
                setAssetSelectionButtonState(false);
                assetsComboBox.IsEnabled = false;
                setRunbookSelectionButtonState(false);

                // Generate self-signed certificate for encrypting local assets in the current user store Cert:\CurrentUser\My\
                var certObj = new AutomationSelfSignedCertificate();
                certificateThumbprint = certObj.CreateSelfSignedCertificate();
                certificateTextBox.Text = certificateThumbprint;
                UpdateStatusBox(configurationStatusTextBox, "Thumbprint of certificate used to encrypt local assets: " + certificateThumbprint);

                // Load feedback and help page preemptively
                surveyBrowserControl.Navigate(new Uri(Constants.feedbackURI));
                helpBrowserControl.Navigate(new Uri(Constants.helpURI));
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

        public String getEncryptionCertificateThumbprint()
        {
            return certificateThumbprint;
        }

        public void setRunbookAndAssetNonSelectionButtonState(bool enabled)
        {
            ButtonRefreshAssetList.IsEnabled = enabled;
            ButttonNewAsset.IsEnabled = enabled;
        }

        public void setRunbookSelectionButtonState(bool enabled)
        {
            ButtonDownloadRunbook.IsEnabled = enabled;
            ButtonOpenRunbook.IsEnabled = enabled;
            ButtonUploadRunbook.IsEnabled = enabled;
            ButtonTestRunbook.IsEnabled = enabled;
            ButtonPublishRunbook.IsEnabled = enabled;
        }

        public void setAssetSelectionButtonState(bool enabled)
        {
            ButtonDownloadAsset.IsEnabled = enabled;
            ButtonEditAsset.IsEnabled = enabled;
            ButtonDeleteAssets.IsEnabled = enabled;
            ButtonUploadAsset.IsEnabled = enabled;
        }

        public IList<AutomationAsset> getSelectedAssets()
        {
            IList items = (System.Collections.IList)assetsListView.SelectedItems;
            return items.Cast<AutomationAsset>().ToList<AutomationAsset>();
        }

        public void setSelectedAssets(IList<AutomationAsset> assetsToSelect)
        {
             var assetsToSelectSet = new HashSet<string>();
             
             foreach (AutomationAsset asset in assetsToSelect) {
                assetsToSelectSet.Add(asset.Name);
            }

            foreach (AutomationAsset asset in assetListViewModel)
            {
                if(assetsToSelectSet.Contains(asset.Name)) {
                    assetsListView.SelectedItems.Add(asset);
                }
            }
        }

        public async Task<SortedSet<AutomationAsset>> getAssetsInfo()
        {
            return (SortedSet<AutomationAsset>)await AutomationAssetManager.GetAll(iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint());
        }

        public async Task<SortedSet<AutomationAsset>> getAssetsOfType(String type)
        {
            var assets = await getAssetsInfo();

            var assetsOfType = new SortedSet<AutomationAsset>();
            foreach (var asset in assets)
            {
                if (asset.GetType().Name == type)
                {
                    assetsOfType.Add(asset);
                }
            }

            return assetsOfType;
        }

        public async Task downloadAllAssets()
        {
            await AutomationAssetManager.DownloadAllFromCloud(iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint());
        }

        public void downloadAssets(ICollection<AutomationAsset> assetsToDownload)
        {
            AutomationAssetManager.DownloadFromCloud(assetsToDownload, iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint());
        }

        public async Task uploadAssets(ICollection<AutomationAsset> assetsToUpload)
        {
            await AutomationAssetManager.UploadToCloud(assetsToUpload, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);

            // Since the cloud assets uploaded will have a last modified time of now, causing them to look newer than their local counterparts,
            // download the assets after upload to force last modified time between local and cloud to be the same, showing them as in sync (which they are)
            downloadAssets(assetsToUpload);
        }

        public void deleteAssets(ICollection<AutomationAsset> assetsToDelete)
        {
            bool deleteLocally = true;
            bool deleteFromCloud = true;

            // when asset is only local or only in cloud, we know where they want to delete it from. But when asset is both local and cloud,
            // they may not have meant to delete it from cloud, so ask them 
            foreach (var assetToDelete in assetsToDelete)
            {
                if (assetToDelete.LastModifiedCloud != null && assetToDelete.LastModifiedLocal != null)
                {
                    var messageBoxResult = System.Windows.MessageBox.Show(
                        "At least some of the selected assets have both local and cloud versions. Do you want to also delete the cloud versions of these assets?",
                        "Delete Confirmation",
                        System.Windows.MessageBoxButton.YesNo
                    );

                    if (messageBoxResult == MessageBoxResult.No)
                    {
                        deleteFromCloud = false;
                    }
                    else if (messageBoxResult == MessageBoxResult.Cancel)
                    {
                        deleteFromCloud = false;
                        deleteLocally = false;
                    }

                    break;
                }
            }

            AutomationAssetManager.Delete(assetsToDelete, iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, deleteLocally, deleteFromCloud, getEncryptionCertificateThumbprint());
        }

        public void refreshAuthToken(object source, ElapsedEventArgs e)
        {
            try
            {
                iseClient.RefreshAutomationClientwithNewToken();
            }
            catch (Exception exception)
            {
                refreshAuthTokenTimer.Stop();
                MessageBox.Show("Your session expired and could not be refreshed. Please sign in again./r/nDetails: " + exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void refreshAccountData(object source, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    Task t = refreshAssets();
                    t = refreshRunbooks();
                }
                catch (Exception exception)
                {
                    refreshAccountDataTimer.Stop();
                    int tokenExpiredResult = -2146233088;
                    if (exception.HResult == tokenExpiredResult)
                    {
                        MessageBox.Show("Your session has expired. Please sign in again.", "Session Expired", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });
        }

        public async Task refreshAssets()
        {
            var selectedAssets = getSelectedAssets();
            string selectedAssetType = (string)assetsComboBox.SelectedValue;
            if (selectedAssetType == null) return;
            if (selectedAssetType == AutomationISE.Model.Constants.assetVariable)
            {
                mergeAssetListWith(await getAssetsOfType("AutomationVariable"));
            }
            else if (selectedAssetType == AutomationISE.Model.Constants.assetCredential)
            {
                mergeAssetListWith(await getAssetsOfType("AutomationCredential"));
            }
            else if (selectedAssetType == AutomationISE.Model.Constants.assetConnection)
            {
                mergeAssetListWith(await getAssetsOfType("AutomationConnection"));
            }
            else if (selectedAssetType == AutomationISE.Model.Constants.assetCertificate)
            {
                mergeAssetListWith(await getAssetsOfType("AutomationCertificate"));
            }
            setSelectedAssets(selectedAssets);
        }

        private void mergeAssetListWith(ICollection<AutomationAsset> newAssetCollection)
        {
            assetListViewModel.Clear();
            
            foreach (AutomationAsset asset in newAssetCollection) {
                assetListViewModel.Add(asset);
            }
        }

        private async void loginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatusBox(configurationStatusTextBox, "Launching login window...");
                iseClient.azureADAuthResult = AutomationISE.Model.AuthenticateHelper.GetInteractiveLogin(userNameTextBox.Text);
                refreshAccountDataTimer.Stop();

                beginBackgroundWork(Properties.Resources.RetrieveSubscriptions);
                userNameTextBox.Text = iseClient.azureADAuthResult.UserInfo.DisplayableId;
                UpdateStatusBox(configurationStatusTextBox, "Logged in user: " + userNameTextBox.Text);
                Properties.Settings.Default["ADUserName"] = userNameTextBox.Text;
                Properties.Settings.Default.Save();

                IList<Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse.Subscription> subscriptions = await iseClient.GetSubscriptions();
                if (subscriptions.Count > 0)
                {
                    endBackgroundWork(Properties.Resources.FoundSubscriptions);
                    subscriptionComboBox.ItemsSource = subscriptions;
                    subscriptionComboBox.DisplayMemberPath = "SubscriptionName";
                    foreach (Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse.Subscription selectedSubscription in subscriptionComboBox.Items)
                    {
                        if (selectedSubscription.SubscriptionId == Properties.Settings.Default.lastSubscription.ToString())
                        {
                            subscriptionComboBox.SelectedItem = selectedSubscription;
                        }
                    }

                    subscriptionComboBox.IsEnabled = false;
                    refreshAuthTokenTimer.Start();
                }
                else
                {
                    endBackgroundWork(Properties.Resources.NoSubscriptions);
                }
            }
            catch (Microsoft.IdentityModel.Clients.ActiveDirectory.AdalServiceException Ex)
            {
                int userCancelResult = -2146233088;
                if (Ex.HResult == userCancelResult)
                    UpdateStatusBox(configurationStatusTextBox, Properties.Resources.CancelSignIn);
                else
                    UpdateStatusBox(configurationStatusTextBox, "Sign-in error: " + Ex.ErrorCode.ToString() + " Error Message: " + Ex.Message);
            }
            catch (Microsoft.IdentityModel.Clients.ActiveDirectory.AdalException Ex)
            {
                int userCancelResult = -2146233088;
                if (Ex.HResult == userCancelResult)
                    UpdateStatusBox(configurationStatusTextBox, Properties.Resources.CancelSignIn);
                else
                    UpdateStatusBox(configurationStatusTextBox, "Sign-in error: " + Ex.ErrorCode.ToString() + " Error Message: " + Ex.Message);
            }
            catch (Exception Ex)
            {
                endBackgroundWork("Couldn't retrieve subscriptions.");
                var detailsDialog = System.Windows.Forms.MessageBox.Show(Ex.Message);
            }
        }

        private async void SubscriptionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                subscriptionComboBox.IsEnabled = false;
                accountsComboBox.IsEnabled = false;
                assetsComboBox.IsEnabled = false;
                refreshAccountDataTimer.Stop();

                // Save the last selected subscription so we default to this one next time the ISE is openend
                var selectedSubscription = (Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse.Subscription)subscriptionComboBox.SelectedValue;
                if (selectedSubscription != null)
                {
                    Properties.Settings.Default.lastSubscription = selectedSubscription.SubscriptionId;
                    Properties.Settings.Default.Save();
                }

                iseClient.currSubscription = (Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse.Subscription)subscriptionComboBox.SelectedValue;
                if (iseClient.currSubscription != null)
                {
                    beginBackgroundWork(Properties.Resources.RetrieveAutomationAccounts);
                    IList<AutomationAccount> automationAccounts = await iseClient.GetAutomationAccounts();
                    accountsComboBox.ItemsSource = automationAccounts;
                    accountsComboBox.DisplayMemberPath = "Name";
                    if (accountsComboBox.HasItems)
                    {
                        endBackgroundWork(Properties.Resources.FoundAutomationAccounts);
                        accountsComboBox.SelectedItem = accountsComboBox.Items[0];
                        accountsComboBox.IsEnabled = true;
                    }
                    else
                    {
                        endBackgroundWork(Properties.Resources.NoAutomationAccounts);
                    }
                }
            }
            catch (Exception exception)
            {
                endBackgroundWork("Couldn't retrieve Automation Accounts.");
                subscriptionComboBox.IsEnabled = true;
                assetsComboBox.IsEnabled = false;
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

        private async void accountsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                AutomationAccount account = (AutomationAccount)accountsComboBox.SelectedValue;
                iseClient.currAccount = account;
                refreshAccountDataTimer.Stop();
                if (account != null)
                {
                    /* Update Status */
                    UpdateStatusBox(configurationStatusTextBox, "Selected automation account: " + account.Name);
                    if (iseClient.AccountWorkspaceExists())
                        accountPathTextBox.Text = iseClient.currWorkspace;
                    /* Update Runbooks */
                    beginBackgroundWork("Getting account data");
                    beginBackgroundWork("Getting runbook data for " + account.Name);
                    if (runbookListViewModel != null) runbookListViewModel.Clear();
                    if (assetListViewModel != null) assetListViewModel.Clear();
                    runbookListViewModel = new ObservableCollection<AutomationRunbook>(await AutomationRunbookManager.GetAllRunbookMetadata(iseClient.automationManagementClient, 
                          iseClient.currWorkspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name));
                    endBackgroundWork("Done getting runbook data");
                    /* Update Assets */
                    beginBackgroundWork("Downloading assets for " + account.Name);
                    //TODO: this is not quite checking what we need it to check
                    if (!iseClient.AccountWorkspaceExists())
                    {
                        await downloadAllAssets();
                    }
                    assetListViewModel = new ObservableCollection<AutomationAsset>();
                    await refreshAssets(); //populates the viewmodel
                    endBackgroundWork("Assets downloaded.");
                    /* Update PowerShell Module */
                    try
                    {
                        PSModuleConfiguration.UpdateModuleConfiguration(iseClient.currWorkspace);
                    }
                    catch
                    {
                        string message = "Could not configure the " + PSModuleConfiguration.ModuleData.ModuleName + " module.\r\n";
                        message += "This module is required for your runbooks to run locally.\r\n";
                        message += "Make sure it exists in your module path (env:PSModulePath).";
                        MessageBox.Show(message);
                    }
                    /* Update UI */
                    RunbooksListView.ItemsSource = runbookListViewModel;
                    assetsListView.ItemsSource = assetListViewModel;
                    // Set credentials assets to be selected
                    assetsComboBox.SelectedItem = assetsComboBox.Items[1];
                    setRunbookSelectionButtonState(false);
                    setRunbookAndAssetNonSelectionButtonState(true);
                    assetsComboBox.IsEnabled = true;
                    ButtonRefreshAssetList.IsEnabled = true;
                    subscriptionComboBox.IsEnabled = true;
                    /* Enable source control sync in Azure Automation if it is set up for this automation account */
                    bool isSourceControlEnabled = await AutomationSourceControl.isSourceControlEnabled(iseClient.automationManagementClient,
                        iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
                    if (isSourceControlEnabled)
                    {
                        ButtonSourceControlRunbook.Visibility = Visibility.Visible;
                        ButtonSourceControlRunbook.IsEnabled = true;
                    }
                    else ButtonSourceControlRunbook.Visibility = Visibility.Collapsed;
                    /* Change current directory to new workspace location */
                    accountPathTextBox.Text = iseClient.currWorkspace;
                    string pathHint = Path.GetPathRoot(iseClient.currWorkspace) + "..." + Path.DirectorySeparatorChar + Path.GetFileName(iseClient.currWorkspace);
                    HostObject.CurrentPowerShellTab.Invoke("cd '" + iseClient.currWorkspace + "'" + ";function prompt {'PS " + pathHint + "> '}");
                    endBackgroundWork("Finished getting data for " + account.Name);
                    UpdateStatusBox(configurationStatusTextBox, "Changing to account directory with shortened prompt for usability.");
                    UpdateStatusBox(configurationStatusTextBox, "Run Get-Locaiton for location or below command to get full prompt.");
                    UpdateStatusBox(configurationStatusTextBox, "Function Prompt {'PS ' + $(Get-Location) + '> '}");
                    refreshAccountDataTimer.Start();
                }
            }
            catch (Exception exception)
            {
                endBackgroundWork("Error getting account data");
                var detailsDialog = MessageBox.Show(exception.Message);
            }
        }

        private void assetsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            setAssetSelectionButtonState(assetsListView.SelectedItems.Count > 0);
        }

        private async void assetsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                assetListViewModel.Clear();
                await refreshAssets();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Assets could not be refreshed.\r\nError details: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void workspaceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //TODO: refactor this
            iseClient.baseWorkspace = workspaceTextBox.Text;
            Properties.Settings.Default["localWorkspace"] = iseClient.baseWorkspace;
            Properties.Settings.Default.Save();
        }
        private void workspaceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.SelectedPath = iseClient.baseWorkspace;
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                iseClient.baseWorkspace = dialog.SelectedPath;
                workspaceTextBox.Text = iseClient.baseWorkspace;

                UpdateStatusBox(configurationStatusTextBox, "Saving workspace location: " + iseClient.baseWorkspace);
                Properties.Settings.Default["localWorkspace"] = iseClient.baseWorkspace;
                Properties.Settings.Default.Save();
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

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
                case "helpTab":
                    helpBrowserControl.Navigate(new Uri(Constants.helpURI));
                    break;
                default:
                    Debug.WriteLine("Couldn't find tab handler with name: " + selectedTab.Name);
                    return;
            }
        }

        private async void ButtonDownloadAsset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                beginBackgroundWork("Downloading selected assets...");
                downloadAssets(getSelectedAssets());
                await refreshAssets();
                endBackgroundWork("Assets downloaded.");
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error downloading assets.");
                MessageBox.Show("Assets could not be downloaded.\r\nError details: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonUploadAsset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                beginBackgroundWork("Uploading selected assets...");
                await uploadAssets(getSelectedAssets());
                await refreshAssets();
                endBackgroundWork("Assets uploaded.");
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error uploading assets.");
                MessageBox.Show("Assets could not be uploaded.\r\nError details: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonDeleteAsset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                beginBackgroundWork("Deleting selected assets...");
                deleteAssets(getSelectedAssets());
                await refreshAssets();
                endBackgroundWork("Assets deleted.");
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error deleting assets.");
                MessageBox.Show("Assets could not be deleted.\r\nError details: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonRefreshAssetList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                beginBackgroundWork("Refreshing assets list...");
                await refreshAssets();
                endBackgroundWork("Refreshed assets list.");
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error refreshing assets.");
                MessageBox.Show("Assets could not be refreshed.\r\nError details: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ConfirmRunbookDownload(string name)
        {
            String message = "Are you sure you want to import the cloud's copy of " + name + "?";
            message += "\r\nAny changes you have made to it locally will be overwritten.";
            String header = "Download Warning";
            System.Windows.Forms.DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(message, header, 
                System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning);
            if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                return true;
            return false;
        }

        private async void ButtonDownloadRunbook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonDownloadRunbook.IsEnabled = false;
                /* 
                 * These outer, empty calls to [begin|end]BackgroundWork() prevent the spinner from stopping
                 * every time a runbook download finishes, which can make the UI look jittery.
                 */
                beginBackgroundWork();
                int count = 0;
                string name = "";
                foreach (Object obj in RunbooksListView.SelectedItems)
                {
                    AutomationRunbook runbook = (AutomationRunbook)obj;
                    if (runbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.LocalOnly)
                        continue;
                    if (runbook.localFileInfo != null && File.Exists(runbook.localFileInfo.FullName) && !ConfirmRunbookDownload(runbook.Name))
                        continue;
                    try
                    {
                        beginBackgroundWork("Downloading runbook " + runbook.Name + "...");
                        await AutomationRunbookManager.DownloadRunbook(runbook, iseClient.automationManagementClient,
                                    iseClient.currWorkspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount);
                        endBackgroundWork("Downloaded " + runbook.Name + ".");
                        count++;
                        name = runbook.Name;
                        runbook.UpdateSyncStatus();
                    }
                    catch (Exception ex)
                    {
                        endBackgroundWork("Error downloading runbook " + runbook.Name);
                        MessageBox.Show("The runbook " + runbook.Name + " could not be downloaded.\r\nError details: " + ex.Message);
                    }
                }
                if (count == 1) endBackgroundWork("Downloaded " + name + ".");
                else endBackgroundWork("Downloaded " + count + " runbooks.");
                ButtonOpenRunbook.IsEnabled = true;
                ButtonUploadRunbook.IsEnabled = true;
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error downloading runbooks.");
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ButtonDownloadRunbook.IsEnabled = true;
            }
        }

        private void beginOrResumeClockwiseSpin()
        {
            progressSpinnerStoryboardReverse.Pause(this);
            miniProgressSpinnerStoryboardReverse.Pause(this);
            try
            {
                progressSpinnerStoryboard.GetCurrentState();
                progressSpinnerStoryboard.Resume(this);
                miniProgressSpinnerStoryboard.Resume(this);
            }
            catch
            {
                /*Begin hasn't been called yet*/
                progressSpinnerStoryboard.Begin(this, true);
                miniProgressSpinnerStoryboard.Begin(this, true);
            }
        }

        private void beginOrResumeAntiClockwiseSpin()
        {
            progressSpinnerStoryboard.Pause(this);
            miniProgressSpinnerStoryboard.Pause(this);
            try
            {
                progressSpinnerStoryboardReverse.GetCurrentState();
                progressSpinnerStoryboardReverse.Resume(this);
                miniProgressSpinnerStoryboardReverse.Resume(this);
            }
            catch
            {
                /*Begin hasn't been called yet*/
                progressSpinnerStoryboardReverse.Begin(this, true);
                miniProgressSpinnerStoryboardReverse.Begin(this, true);
            }
        }

        private void pauseAllSpin()
        {
            progressSpinnerStoryboard.Pause(this);
            progressSpinnerStoryboardReverse.Pause(this);
            miniProgressSpinnerStoryboard.Pause(this);
            miniProgressSpinnerStoryboardReverse.Pause(this);
        }

        private void RunbooksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            if (selectedRunbook == null)
            {
                setRunbookSelectionButtonState(false);
                return;
            }
            /* Set Download button status */
            if (selectedRunbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.LocalOnly)
                ButtonDownloadRunbook.IsEnabled = false;
            else
                ButtonDownloadRunbook.IsEnabled = true;
            /* Set Open and Upload button status */
            if (selectedRunbook.localFileInfo != null && File.Exists(selectedRunbook.localFileInfo.FullName))
            {
                ButtonOpenRunbook.IsEnabled = true;
                ButtonUploadRunbook.IsEnabled = true;
            }
            else
            {
                ButtonOpenRunbook.IsEnabled = false;
                ButtonUploadRunbook.IsEnabled = false;
            }
            /* Set Test and Publish button status */
            if (selectedRunbook.AuthoringState == AutomationRunbook.AuthoringStates.Published || selectedRunbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.LocalOnly)
            {
                ButtonTestRunbook.IsEnabled = false;
                ButtonPublishRunbook.IsEnabled = false;
            }
            else
            {
                ButtonTestRunbook.IsEnabled = true;
                ButtonPublishRunbook.IsEnabled = true;
            }
        }

        private void ButtonOpenRunbook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (Object obj in RunbooksListView.SelectedItems)
                {
                    AutomationRunbook selectedRunbook = (AutomationRunbook)obj;
                    HostObject.CurrentPowerShellTab.Files.Add(selectedRunbook.localFileInfo.FullName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("The runbook could not be opened.\r\nError details: " + ex.Message, "Error");
            }
        }

        private async void ButtonPublishRunbook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                /* Update UI */
                ButtonPublishRunbook.IsEnabled = false;
                ButtonDownloadRunbook.IsEnabled = false;
                ButtonUploadRunbook.IsEnabled = false;
                ButtonPublishRunbook.Content = "Publishing...";
                beginBackgroundWork();
                int count = 0;
                string name = "";
                foreach (Object obj in RunbooksListView.SelectedItems)
                {
                    AutomationRunbook selectedRunbook = (AutomationRunbook)obj;
                    if (selectedRunbook.AuthoringState == AutomationRunbook.AuthoringStates.Published)
                        continue;
                    try
                    {
                        beginBackgroundWork("Publishing runbook " + selectedRunbook.Name + "...");
                        await AutomationRunbookManager.PublishRunbook(selectedRunbook, iseClient.automationManagementClient,
                                    iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
                        count++;
                        name = selectedRunbook.Name;
                        endBackgroundWork("Published runbook " + selectedRunbook.Name);
                    }
                    catch (Exception ex)
                    {
                        endBackgroundWork("Error publishing runbook " + selectedRunbook.Name);
                        MessageBox.Show("The runbook could not be published.\r\nDetails: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                if (count == 1) endBackgroundWork("Published " + name);
                else endBackgroundWork("Published " + count + " runbooks.");
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error publishing runbooks.");
                MessageBox.Show("Error publishing runbooks.\r\nDetails: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                /* Update UI */
                ButtonPublishRunbook.IsEnabled = true;
                ButtonDownloadRunbook.IsEnabled = true;
                ButtonUploadRunbook.IsEnabled = true;
                ButtonPublishRunbook.Content = "Publish Draft";
            }
        }

        private async Task refreshRunbooks()
        {
            ISet<AutomationRunbook> runbooks = await AutomationRunbookManager.GetAllRunbookMetadata(iseClient.automationManagementClient,
                                    iseClient.currWorkspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
            IDictionary<String, AutomationRunbook> runbookWithName = new Dictionary<String, AutomationRunbook>(runbooks.Count);
            foreach (AutomationRunbook runbook in runbooks)
            {
                runbookWithName.Add(runbook.Name, runbook);
            }
            foreach (AutomationRunbook curr in runbookListViewModel)
            {
                curr.AuthoringState = runbookWithName[curr.Name].AuthoringState;
                curr.Parameters = runbookWithName[curr.Name].Parameters;
                curr.Description = runbookWithName[curr.Name].Description;
                curr.LastModifiedCloud = runbookWithName[curr.Name].LastModifiedCloud;
                curr.LastModifiedLocal = runbookWithName[curr.Name].LastModifiedLocal;
                curr.UpdateSyncStatus();
                runbookWithName.Remove(curr.Name);
            }
            foreach (String name in runbookWithName.Keys)
            {
                runbookListViewModel.Add(runbookWithName[name]);
            }
        }

        private async void ButtonRefreshRunbookList_Click(object sender, RoutedEventArgs e)
        {
            ButtonRefreshRunbookList.IsEnabled = false;
            ButtonRefreshRunbookList.Content = "Refreshing...";
            try
            {
                beginBackgroundWork("Refreshing runbook data...");
                await refreshRunbooks();
                endBackgroundWork("Refreshed runbook data.");
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error refreshing runbook data.");
                MessageBox.Show("The runbook list could not be refreshed.\r\nError details: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ButtonRefreshRunbookList.IsEnabled = true;
                ButtonRefreshRunbookList.Content = "Refresh";
            }
        }

        private async void ButtonUploadRunbook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonUploadRunbook.IsEnabled = false;
                beginBackgroundWork();
                int count = 0;
                string name = "";
                foreach (Object obj in RunbooksListView.SelectedItems)
                {
                    AutomationRunbook selectedRunbook = (AutomationRunbook)obj;
                    if (selectedRunbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.CloudOnly)
                        continue;
                    try
                    {
                        // If the file is unsaved in the ISE, show warning to user before uploading
                        if (checkIfFileIsSaved(selectedRunbook))
                        {
                            beginBackgroundWork("Uploading runbook " + selectedRunbook.Name + "...");
                            await AutomationRunbookManager.UploadRunbookAsDraft(selectedRunbook, iseClient.automationManagementClient,
                                        iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount);
                            count++;
                            name = selectedRunbook.Name;
                            selectedRunbook.UpdateSyncStatus();
                            endBackgroundWork("Uploaded " + selectedRunbook.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        endBackgroundWork("Error uploading runbook " + selectedRunbook.Name);
                        MessageBox.Show("The runbook " + selectedRunbook.Name + " could not be uploaded.\r\nError details: " + ex.Message);
                    }
                }
                if (count == 1) endBackgroundWork("Uploaded " + name);
                else endBackgroundWork("Uploaded " + count + " runbooks.");
                ButtonPublishRunbook.IsEnabled = true;
                ButtonTestRunbook.IsEnabled = true;
                ButtonDownloadRunbook.IsEnabled = true;
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error uploading runbooks.");
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ButtonUploadRunbook.IsEnabled = true;
            }
        }

        private void ButtonTestRunbook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RunbooksListView.SelectedItems.Count > 1)
                {
                    string message = "Batch creation of test jobs is suppressed for performance reasons.";
                    message += "\r\nPlease create test jobs one at a time, eager beaver!";
                    MessageBox.Show(message, "Test Job Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
                JobOutputWindow jobWindow = new JobOutputWindow(selectedRunbook.Name, iseClient);
                jobWindow.Show();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Checks if a file is unsaved in the ISE and shows a dialog to ask user to confirm if they want to continue
        /// </summary>
        /// <param name="runbook"></param>
        /// <returns>false if the user clicks cancel or else returns true to continue with upload of unsaved file</returns>
        private Boolean checkIfFileIsSaved(AutomationRunbook runbook)
        {
            var iseFiles = HostObject.CurrentPowerShellTab.Files;
            foreach (var file in iseFiles)
            {
                if ((file.DisplayName == (runbook.Name + ".ps1*")) && (file.IsSaved == false))
                {
                    String message = "The file " + runbook.Name + ".ps1 is currently unsaved in the ISE";
                    message += "\r\nCancel and save the file or click OK to upload the unsaved file";
                    String header = "Upload Warning";
                    System.Windows.Forms.DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(message, header,
                        System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Warning);
                    if (dialogResult == System.Windows.Forms.DialogResult.Cancel)
                        return false;
                }
            }
            return true;
        }

        private async Task createOrUpdateCredentialAsset(string credentialAssetName, AutomationCredential credToEdit)
        {
            var dialog = new NewOrEditCredentialDialog(credToEdit);

            if (dialog.ShowDialog() == true)
            {
                var assetsToSave = new List<AutomationAsset>();

                var newCred = new AutomationCredential(credentialAssetName, dialog.username, dialog.password);
                assetsToSave.Add(newCred);

                AutomationAssetManager.SaveLocally(iseClient.currWorkspace, assetsToSave, getEncryptionCertificateThumbprint());
                await refreshAssets();
            }
        }

        private async Task createOrUpdateVariableAsset(string variableAssetName, AutomationVariable variableToEdit)
        {
            var dialog = new NewOrEditVariableDialog(variableToEdit);

            if (dialog.ShowDialog() == true)
            {
                var assetsToSave = new List<AutomationAsset>();

                var newVariable = new AutomationVariable(variableAssetName, dialog.value, dialog.encrypted);
                assetsToSave.Add(newVariable);

                AutomationAssetManager.SaveLocally(iseClient.currWorkspace, assetsToSave, getEncryptionCertificateThumbprint());
                await refreshAssets();
            }
        }

        private async void ButtonNewAsset_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ChooseNewAssetTypeDialog();

            if (dialog.ShowDialog() == true)
            {
                if (dialog.newAssetType == AutomationISE.Model.Constants.assetVariable)
                {
                    await createOrUpdateVariableAsset(dialog.newAssetName, null);
                }
                else if (dialog.newAssetType == AutomationISE.Model.Constants.assetCredential)
                {
                    await createOrUpdateCredentialAsset(dialog.newAssetName, null);
                }
                else if (dialog.newAssetType == AutomationISE.Model.Constants.assetConnection)
                {

                }
                else if (dialog.newAssetType == AutomationISE.Model.Constants.assetCertificate)
                {

                }
            }
        }

        private async void ButtonEditAsset_Click(object sender, RoutedEventArgs e)
        {
            var asset = getSelectedAssets().ElementAt(0);

            if (asset is AutomationCredential)
            {
                await createOrUpdateCredentialAsset(asset.Name, (AutomationCredential)asset);
            }
            else if (asset is AutomationVariable)
            {
                await createOrUpdateVariableAsset(asset.Name, (AutomationVariable)asset);
            }
        }

        private void certificateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var certDialog = new ChangeCertificateDialog(certificateThumbprint);
                certDialog.ShowDialog();
                if (certificateTextBox.Text != certDialog.updatedThumbprint)
                {
                    certificateTextBox.Text = certDialog.updatedThumbprint;
                    /* Strip bad character that appears when you copy/paste from certmgr */
                    String cleanedString = certificateTextBox.Text.Trim(new char[] { '\u200E' });
                    /* Throw exception if the given thumbprint is not a valid certificate */
                    AutomationSelfSignedCertificate.GetCertificateWithThumbprint(cleanedString);
                    AutomationSelfSignedCertificate.SetCertificateInConfigFile(cleanedString);
                    certificateThumbprint = cleanedString;
                    UpdateStatusBox(configurationStatusTextBox, "Updated thumbprint of certificate used to encrypt local assets: " + certificateThumbprint);
                }
            }
            catch (Exception ex)
            {
                certificateTextBox.Text = certificateThumbprint;
                MessageBox.Show("The thumbprint could not be updated:\r\n" + ex.Message + ".", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonSourceControlRunbook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                JobCreateResponse sourceControlJob = await AutomationSourceControl.startSourceControlJob(iseClient.automationManagementClient,
                            iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);

                JobOutputWindow jobWindow = new JobOutputWindow(sourceControlJob.Job.Properties.Runbook.Name, sourceControlJob, iseClient);
                jobWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("The source control job could not be started. " + ex.Message, "Error");
                return;
            }
        }

        private void accountPathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!String.IsNullOrEmpty(accountPathTextBox.Text))
                    Process.Start(@accountPathTextBox.Text);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Couldn't open path", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void beginBackgroundWork(string message = "")
        {
            lock (backgroundWorkLock)
            {
                if (numBackgroundTasks <= 0)
                {
                    numBackgroundTasks = 0;
                    WorkFinishingTextBlock.Text = "";
                    beginOrResumeClockwiseSpin();
                }
                numBackgroundTasks++;
                WorkStartingTextBlock.Text = message;
            }
        }

        private void endBackgroundWork(string message = "")
        {
            lock (backgroundWorkLock)
            {
                numBackgroundTasks--;
                WorkFinishingTextBlock.Text = message;
                if (numBackgroundTasks <= 0)
                {
                    numBackgroundTasks = 0;
                    WorkStartingTextBlock.Text = "";
                    pauseAllSpin();
                }
            }
        }

        /*
         * Sorting logic:
         * Clicking on a column sorts it in ascending order.
         * If the column was already sorted in ascending order, it gets re-sorted into descending order.
         * If a column can have the same value in many different rows (e.g. Status and SyncStatus),
         *   then the list is secondarily sorted by runbook name, ascending.
         */ 
        private void runbookListColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = (GridViewColumnHeader)sender;
            string sortProperty = column.Tag.ToString();
            RunbooksListView.Items.SortDescriptions.Clear();
            if (sortProperty != runbookCurrSortProperty || runbookCurrSortDir == ListSortDirection.Descending)
                runbookCurrSortDir = ListSortDirection.Ascending;
            else
                runbookCurrSortDir = ListSortDirection.Descending;
            runbookCurrSortProperty = sortProperty;
            SortDescription newDescription = new SortDescription(runbookCurrSortProperty, runbookCurrSortDir);
            RunbooksListView.Items.SortDescriptions.Add(newDescription);
            if (runbookCurrSortProperty != "Name")
                RunbooksListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        }
        /*
         * Sorting logic: same as for runbooks.
         */ 
        private void assetListColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = (GridViewColumnHeader)sender;
            string sortProperty = column.Tag.ToString();
            assetsListView.Items.SortDescriptions.Clear();
            if (sortProperty != assetCurrSortProperty || assetCurrSortDir == ListSortDirection.Descending)
                assetCurrSortDir = ListSortDirection.Ascending;
            else
                assetCurrSortDir = ListSortDirection.Descending;
            assetCurrSortProperty = sortProperty;
            SortDescription newDescription = new SortDescription(assetCurrSortProperty, assetCurrSortDir);
            assetsListView.Items.SortDescriptions.Add(newDescription);
            if (assetCurrSortProperty != "Name")
                assetsListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        }
    }
}
