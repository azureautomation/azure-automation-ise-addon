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
using System.Windows.Data;
using System.Windows.Media.Animation;
using System.Text;

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
        private ISet<ConnectionType> connectionTypes;
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
        private bool promptShortened;
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
                    String userProfile = Environment.GetEnvironmentVariable("USERPROFILE") + "\\";
                    localWorkspace = System.IO.Path.Combine(userProfile, "AutomationWorkspace");
                    Properties.Settings.Default["localWorkspace"] = localWorkspace;
                    Properties.Settings.Default.Save();
                }
                iseClient.baseWorkspace = localWorkspace;
                promptShortened = false;

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

                assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetConnection);
                assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetCredential);
                assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetVariable);
                //assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetCertificate);

                setCreationButtonStatesTo(false);
                setAllAssetButtonStatesTo(false);
                assetsComboBox.IsEnabled = false;
                setAllRunbookButtonStatesTo(false);

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

        public void setCreationButtonStatesTo(bool enabled)
        {
            ButtonNewAsset.IsEnabled = enabled;
            ButtonCreateRunbook.IsEnabled = enabled;
        }

        public void setAllRunbookButtonStatesTo(bool enabled)
        {
            ButtonDeleteRunbook.IsEnabled = enabled;
            ButtonDownloadRunbook.IsEnabled = enabled;
            ButtonOpenRunbook.IsEnabled = enabled;
            ButtonUploadRunbook.IsEnabled = enabled;
            ButtonTestRunbook.IsEnabled = enabled;
            ButtonPublishRunbook.IsEnabled = enabled;
        }

        public void setAllAssetButtonStatesTo(bool enabled)
        {
            ButtonDownloadAsset.IsEnabled = enabled;
            ButtonEditAsset.IsEnabled = enabled;
            ButtonDeleteAssets.IsEnabled = enabled;
            ButtonUploadAsset.IsEnabled = enabled;
            ButtonInsertAssets.IsEnabled = enabled;
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

        public async Task<ISet<ConnectionType>> getConnectionTypes()
        {
            return await AutomationAssetManager.GetConnectionTypes(iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
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
                refreshAuthTokenTimer.Stop();
                iseClient.RefreshAutomationClientwithNewToken();
                refreshAuthTokenTimer.Start();
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

            connectionTypes = await getConnectionTypes();
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

                    if (subscriptionComboBox.SelectedItem == null)
                    {
                        subscriptionComboBox.SelectedItem = subscriptionComboBox.Items[0];
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
                    var accountList = automationAccounts.OrderBy(x => x.Name);
                    accountsComboBox.ItemsSource = accountList;
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
                    CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(RunbooksListView.ItemsSource);
                    view.Filter = FilterRunbook;
                    assetsListView.ItemsSource = assetListViewModel;
                    // Set credentials assets to be selected
                    assetsComboBox.SelectedItem = assetsComboBox.Items[1];
                    setAllRunbookButtonStatesTo(false);
                    setCreationButtonStatesTo(true);
                    assetsComboBox.IsEnabled = true;
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
                    promptShortened = true;
                    endBackgroundWork("Finished getting data for " + account.Name);
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
            setAllAssetButtonStatesTo(assetsListView.SelectedItems.Count > 0);
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

        private void ButtonInsertAsset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var asset = getSelectedAssets().ElementAt(0);
                HostObject.CurrentPowerShellTab.Files.SelectedFile.Editor.InsertText(asset.getGetCommand());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Asset could not be inserted.\r\nError details: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    if (runbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.LocalOnly ||
                        runbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.InSync)
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
                    catch (OperationCanceledException)
                    {
                        endBackgroundWork("Downloading " + runbook.Name + " timed out.");
                    }
                    catch (Exception ex)
                    {
                        endBackgroundWork("Error downloading runbook " + runbook.Name);
                        MessageBox.Show("The runbook " + runbook.Name + " could not be downloaded.\r\nError details: " + ex.Message);
                    }
                }
                await refreshRunbooks();
                if (count == 1) endBackgroundWork("Downloaded " + name + ".");
                else if (count > 1) endBackgroundWork("Downloaded " + count + " runbooks.");
                else endBackgroundWork();
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error downloading runbooks.");
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetButtonStatesForSelectedRunbook();
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

        private void SetButtonStatesForSelectedRunbook()
        {
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            if (selectedRunbook == null)
            {
                setAllRunbookButtonStatesTo(false);
                ButtonCreateRunbook.IsEnabled = true;
                return;
            }
            ButtonDeleteRunbook.IsEnabled = true;
            ButtonCreateRunbook.IsEnabled = true;
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
            /* Set Test button status */
            if (selectedRunbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.LocalOnly)
            {
                ButtonTestRunbook.IsEnabled = false;
            }
            else
            {
                ButtonTestRunbook.IsEnabled = true;
            }
            /* Set Publish button status */
            if (selectedRunbook.AuthoringState == AutomationRunbook.AuthoringStates.Published || 
                selectedRunbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.LocalOnly)
            {
                ButtonPublishRunbook.IsEnabled = false;
            }
            else
            {
                ButtonPublishRunbook.IsEnabled = true;
            }
        }

        private void RunbooksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetButtonStatesForSelectedRunbook();
        }

        private void ButtonOpenRunbook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (Object obj in RunbooksListView.SelectedItems)
                {
                    AutomationRunbook selectedRunbook = (AutomationRunbook)obj;
                    if (selectedRunbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.CloudOnly)
                    {
                        MessageBox.Show("There is no local copy of " + selectedRunbook.localFileInfo.Name + " to open.",
                                "No Local Runbook", MessageBoxButton.OK, MessageBoxImage.Warning);
                        continue;
                    }
                    var currentFile = HostObject.CurrentPowerShellTab.Files.Where(x => x.FullPath == selectedRunbook.localFileInfo.FullName);
                    if (currentFile.Count() > 0)
                    {
                        try
                        {
                            // If the file is opened but not saved, an exception will be thrown here
                            HostObject.CurrentPowerShellTab.Files.Remove(currentFile.First());
                        }
                        catch
                        {
                            MessageBox.Show("There are unsaved changes to " + selectedRunbook.localFileInfo.Name + ", so it cannot be re-opened.",
                                "Unsaved Runbook Changes", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
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
                await refreshRunbooks();
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error publishing runbooks.");
                MessageBox.Show("Error publishing runbooks.\r\nDetails: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                /* Update UI */
                ButtonPublishRunbook.Content = "Publish Draft";
                SetButtonStatesForSelectedRunbook();
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
            ISet<AutomationRunbook> runbooksToDelete = new SortedSet<AutomationRunbook>();
            foreach (AutomationRunbook curr in runbookListViewModel)
            {
                if (!runbookWithName.ContainsKey(curr.Name))
                {
                    runbooksToDelete.Add(curr);
                    continue;
                }
                curr.AuthoringState = runbookWithName[curr.Name].AuthoringState;
                curr.Parameters = runbookWithName[curr.Name].Parameters;
                curr.Description = runbookWithName[curr.Name].Description;
                curr.LastModifiedCloud = runbookWithName[curr.Name].LastModifiedCloud;
                curr.LastModifiedLocal = runbookWithName[curr.Name].LastModifiedLocal;
                curr.UpdateSyncStatus();
                runbookWithName.Remove(curr.Name);
            }
            foreach (AutomationRunbook runbook in runbooksToDelete)
            {
                runbookListViewModel.Remove(runbook);
            }
            foreach (String name in runbookWithName.Keys)
            {
                runbookListViewModel.Add(runbookWithName[name]);
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
                await refreshRunbooks();
                if (count == 1) endBackgroundWork("Uploaded " + name);
                else endBackgroundWork("Uploaded " + count + " runbooks.");
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error uploading runbooks.");
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetButtonStatesForSelectedRunbook();
            }
        }

        private async void ButtonTestRunbook_Click(object sender, RoutedEventArgs e)
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
                ButtonTestRunbook.IsEnabled = false;
                AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
                if (selectedRunbook.AuthoringState == AutomationRunbook.AuthoringStates.Published)
                {
                    beginBackgroundWork();
                    await AutomationRunbookManager.CheckOutRunbook(selectedRunbook, iseClient.automationManagementClient,
                        iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount);
                }
                JobOutputWindow jobWindow = new JobOutputWindow(selectedRunbook.Name, iseClient);
                jobWindow.Show();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetButtonStatesForSelectedRunbook();
                endBackgroundWork();
            }
        }

        /// <summary>
        /// Checks if a file is unsaved in the ISE and shows a dialog to ask user to confirm if they want to continue
        /// </summary>
        /// <param name="runbook"></param>
        /// <returns>false if the user clicks cancel or else returns true to continue with upload of unsaved file</returns>
        private Boolean checkIfFileIsSaved(AutomationRunbook runbook)
        {
            var currentFile = HostObject.CurrentPowerShellTab.Files.Where(x => x.FullPath == runbook.localFileInfo.FullName);
            if (currentFile.Count() != 0)
                {
                if (currentFile.First().IsSaved == false)
                {
                    String message = "The file " + runbook.localFileInfo.Name + " has unsaved changes.";
                    message += "\r\nPlease save your changes before uploading.";
                    System.Windows.Forms.DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(message, "Upload Warning",
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return false;
                }
            }
            return true;
        }

        private async Task createOrUpdateCredentialAsset(string credentialAssetName, AutomationCredential credToEdit, bool newAsset = false)
        {
            if (newAsset)
            {
                var asset = await AutomationAssetManager.GetAsset(credentialAssetName, Constants.AssetType.Credential, iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint());
                if (asset != null) throw new Exception("Credential with that name already exists");
            }

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

        private async Task createOrUpdateVariableAsset(string variableAssetName, AutomationVariable variableToEdit, bool newAsset = false)
        {
            if (newAsset)
            {
                // Check if variable already exists before creating one.
                var asset = await AutomationAssetManager.GetAsset(variableAssetName, Constants.AssetType.Variable, iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint());
                if (asset != null) throw new Exception("Variable with that name already exists");
            }

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

        private async Task createOrUpdateConnectionAsset(string connectionAssetName, AutomationConnection connectionToEdit, bool newAsset = false)
        {
            if (newAsset)
            {
                // Check if connection already exists before creating one.
                var asset = await AutomationAssetManager.GetAsset(connectionAssetName, Constants.AssetType.Connection, iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint());
                if (asset != null) throw new Exception("Connection with that name already exists");
            }

            var dialog = new NewOrEditConnectionDialog(connectionToEdit, connectionTypes);

            if (dialog.ShowDialog() == true)
            {
                var assetsToSave = new List<AutomationAsset>();

                var newConnection = new AutomationConnection(connectionAssetName, dialog.connectionFields, dialog.connectionType);
                assetsToSave.Add(newConnection);
                AutomationAssetManager.SaveLocally(iseClient.currWorkspace, assetsToSave, getEncryptionCertificateThumbprint());
                await refreshAssets();
            }
        }

        private async void ButtonNewAsset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ChooseNewAssetTypeDialog();

                if (dialog.ShowDialog() == true)
                {
                    if (dialog.newAssetType == AutomationISE.Model.Constants.assetVariable)
                    {
                        await createOrUpdateVariableAsset(dialog.newAssetName, null);
                        assetsComboBox.SelectedItem = assetsComboBox.Items[0];
                    }
                    else if (dialog.newAssetType == AutomationISE.Model.Constants.assetCredential)
                    {
                        await createOrUpdateCredentialAsset(dialog.newAssetName, null,true);
                        assetsComboBox.SelectedItem = assetsComboBox.Items[1];
                    }
                    else if (dialog.newAssetType == AutomationISE.Model.Constants.assetConnection)
                    {
                        await createOrUpdateConnectionAsset(dialog.newAssetName, null,true);
                        assetsComboBox.SelectedItem = assetsComboBox.Items[2];
                    }
                    else if (dialog.newAssetType == AutomationISE.Model.Constants.assetCertificate)
                    {

                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonEditAsset_Click(object sender, RoutedEventArgs e)
        {
            try
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
                else if (asset is AutomationConnection)
                {
                    await createOrUpdateConnectionAsset(asset.Name, (AutomationConnection)asset);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void PortalButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StringBuilder url = new StringBuilder();
                url.Append(Constants.portalURL);
                // Deep linking only works if there is one subscription at the moment.
                // Will just go to the portal home page if there is more than one subscription until
                // this is supported in the portal
                if (iseClient.currAccount != null && subscriptionComboBox.Items.Count == 1)
                { 
                    url.Append(iseClient.currSubscription.SubscriptionId);
                    url.Append("/resourceGroups/");
                    url.Append(iseClient.accountResourceGroups[iseClient.currAccount].Name);
                    url.Append("/providers/Microsoft.Automation/automationAccounts/");
                    url.Append(iseClient.currAccount.Name);
                }
                Process.Start(url.ToString());
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Could not launch portal", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void togglePromptButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (promptShortened)
                {
                    HostObject.CurrentPowerShellTab.Invoke("cd '" + iseClient.currWorkspace + "'" + ";function prompt {'PS ' + $(Get-Location) + '> '}");
                    promptShortened = false;
                }
                else
                {
                    //TODO: factor this into the iseClient
                    string pathHint = Path.GetPathRoot(iseClient.currWorkspace) + "..." + Path.DirectorySeparatorChar + Path.GetFileName(iseClient.currWorkspace);
                    HostObject.CurrentPowerShellTab.Invoke("cd '" + iseClient.currWorkspace + "'" + ";function prompt {'PS " + pathHint + "> '}");
                    promptShortened = true;
                }
            }
            catch { }
        }

        private async void ButtonCreateRunbook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonCreateRunbook.IsEnabled = false;
                beginBackgroundWork("Creating new runbook...");
                CreateRunbookDialog createOptionsWindow = new CreateRunbookDialog();
                bool? result = createOptionsWindow.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    AutomationRunbookManager.CreateLocalRunbook(createOptionsWindow.runbookName, iseClient.currWorkspace, createOptionsWindow.runbookType);
                    await refreshRunbooks();
                    /* Now, select and open the newly-created runbook */
                    foreach (AutomationRunbook runbook in runbookListViewModel)
                    {
                        if (runbook.Name.Equals(createOptionsWindow.runbookName))
                        {
                            RunbooksListView.SelectedItem = runbook;
                            ButtonOpenRunbook_Click(null, null);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not create the new runbook.\r\nError details: " + ex.Message);
            } 
            finally
            {
                endBackgroundWork();
                SetButtonStatesForSelectedRunbook();
            }
        }

        private async void ButtonDeleteRunbook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                beginBackgroundWork("Deleting selected runbooks...");
                foreach (Object obj in RunbooksListView.SelectedItems)
                {
                    AutomationRunbook runbook = (AutomationRunbook)obj;
                    if (runbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.CloudOnly)
                    {
                        String message = "Are you sure you wish to delete the cloud copy of " + runbook.Name + "?  ";
                        message += "There is no local copy.";
                        MessageBoxResult result = MessageBox.Show(message, "Confirm Runbook Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            await AutomationRunbookManager.DeleteCloudRunbook(runbook, iseClient.automationManagementClient,
                                iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
                        }
                    }
                    else if (runbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.LocalOnly)
                    {
                        String message = "Are you sure you wish to delete the local copy of " + runbook.Name + "?  ";
                        message += "There is no cloud copy.";
                        MessageBoxResult result = MessageBox.Show(message, "Confirm Runbook Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            if (runbook.localFileInfo != null && File.Exists(runbook.localFileInfo.FullName))
                                AutomationRunbookManager.DeleteLocalRunbook(runbook);
                        }
                    }
                    else
                    {
                        DeleteRunbookDialog deleteOptionsWindow = new DeleteRunbookDialog();
                        bool? result = deleteOptionsWindow.ShowDialog();
                        if (result.HasValue && result.Value)
                        {
                            if (deleteOptionsWindow.deleteLocalOnly)
                            {
                                if (runbook.localFileInfo != null && File.Exists(runbook.localFileInfo.FullName))
                                    AutomationRunbookManager.DeleteLocalRunbook(runbook);
                            }
                            else
                            {
                                await AutomationRunbookManager.DeleteCloudRunbook(runbook, iseClient.automationManagementClient,
                                    iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
                                if (runbook.localFileInfo != null && File.Exists(runbook.localFileInfo.FullName))
                                    AutomationRunbookManager.DeleteLocalRunbook(runbook);
                            }
                        }
                    }
                }
                await refreshRunbooks();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not delete the selected runbook(s).\r\nError details: " + ex.Message);
            }
            finally
            {
                endBackgroundWork();
            }
        }

        private void RunbooksListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                AutomationRunbook runbook = ((FrameworkElement)e.OriginalSource).DataContext as AutomationRunbook;
                if (runbook != null)
                {
                    ButtonOpenRunbook_Click(null, null);
                }
            }
            catch { }
        }

        private void assetsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                AutomationAsset item = ((FrameworkElement)e.OriginalSource).DataContext as AutomationAsset;
                if (item != null)
                {
                    ButtonEditAsset_Click(null, null);
                }
            }
            catch { }
        }

        private void RunbookFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                CollectionViewSource.GetDefaultView(RunbooksListView.ItemsSource).Refresh();
            }
            catch { }
        }

        private bool doBasicFiltering(object item)
        {
            bool authoringStateMatch = ((item as AutomationRunbook).AuthoringState.IndexOf(RunbookFilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            bool syncStatusMatch = ((item as AutomationRunbook).SyncStatus.IndexOf(RunbookFilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            bool nameMatch = ((item as AutomationRunbook).Name.IndexOf(RunbookFilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            return (authoringStateMatch || syncStatusMatch || nameMatch);
        }

        private bool doAdvancedFiltering(object item)
        {
            string[] queries = RunbookFilterTextBox.Text.Split(null);
            string nameQuery = null;
            string statusQuery = null;
            string syncStatusQuery = null;
            string nameQueryPrefix = "name:";
            string statusQueryPrefix = "status:";
            string syncStatusQueryPrefix = "syncStatus:";
            foreach (string query in queries)
            {
                if (String.IsNullOrEmpty(query)) continue;
                int nameQueryPrefixStart = query.IndexOf(nameQueryPrefix, StringComparison.OrdinalIgnoreCase);
                int statusQueryPrefixStart = query.IndexOf(statusQueryPrefix, StringComparison.OrdinalIgnoreCase);
                int syncStatusQueryPrefixStart = query.IndexOf(syncStatusQueryPrefix, StringComparison.OrdinalIgnoreCase);
                if (nameQueryPrefixStart >= 0)
                {
                    nameQuery = query.Substring(nameQueryPrefixStart + nameQueryPrefix.Length);
                }
                else if (syncStatusQueryPrefixStart >= 0)
                {
                    syncStatusQuery = query.Substring(syncStatusQueryPrefixStart + syncStatusQueryPrefix.Length);
                }
                else if (statusQueryPrefixStart >= 0)
                {
                    statusQuery = query.Substring(statusQueryPrefixStart + statusQueryPrefix.Length);
                }
                else if (nameQuery == null)
                {
                    nameQuery = query;
                }
            }
            bool authoringStateMatch = String.IsNullOrEmpty(statusQuery) ? true : false;
            bool syncStatusMatch = String.IsNullOrEmpty(syncStatusQuery) ? true : false;
            bool nameMatch = String.IsNullOrEmpty(nameQuery) ? true : false;
            if (!String.IsNullOrEmpty(statusQuery) && statusQuery.Length > 1)
                authoringStateMatch = ((item as AutomationRunbook).AuthoringState.IndexOf(statusQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!String.IsNullOrEmpty(syncStatusQuery) && syncStatusQuery.Length > 1)
                syncStatusMatch = ((item as AutomationRunbook).SyncStatus.IndexOf(syncStatusQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!String.IsNullOrEmpty(nameQuery) && nameQuery.Length > 1)
                nameMatch = ((item as AutomationRunbook).Name.IndexOf(nameQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            
            return (authoringStateMatch && syncStatusMatch && nameMatch);
        }

        private bool FilterRunbook(object item)
        {
            if (String.IsNullOrEmpty(RunbookFilterTextBox.Text) || RunbookFilterTextBox.Text.Length < 2)
                return true;
            if (RunbookFilterTextBox.Text.IndexOf(':') >= 0)
                return doAdvancedFiltering(item);
            else
                return doBasicFiltering(item);
        }
    }
}
