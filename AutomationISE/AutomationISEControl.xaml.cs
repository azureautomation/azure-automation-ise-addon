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
using System.Linq;

using System.Diagnostics;
using System.Timers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for AutomationISEControl.xaml
    /// </summary>
    public partial class AutomationISEControl : UserControl, IAddOnToolHostObject
    {
        private System.Timers.Timer refreshTimer = new System.Timers.Timer();
        private AutomationISEClient iseClient;
        private ObservableCollection<AutomationRunbook> runbookListViewModel;
        private BlockingCollection<RunbookDownloadJob> downloadQueue;
        private Task downloadWorker;
        private bool tokenExpired = false;
        public ObjectModelRoot HostObject { get; set; }

        public AutomationISEControl()
        {
            try
            {
                InitializeComponent();
                iseClient = new AutomationISEClient();
                downloadQueue = new BlockingCollection<RunbookDownloadJob>(new ConcurrentQueue<RunbookDownloadJob>(),50);
                //TODO: refactor
                IProgress<Tuple<string, int>> progress = new Progress<Tuple<string, int>>((report) => {
                    if (String.IsNullOrEmpty(report.Item1))
                    {
                        ProgressLabel.Text = "";
                        JobsRemainingLabel.Text = "";
                    }
                    else
                    {
                        ProgressLabel.Text = "Downloading runbook '" + report.Item1 + "'...";
                        if (downloadQueue.Count > 0)
                        {
                            JobsRemainingLabel.Text = "(" + downloadQueue.Count + " remaining)";
                        }
                        else
                        {
                            JobsRemainingLabel.Text = "";
                        }
                    }
                });
                downloadWorker = Task.Factory.StartNew(() => processJobsFromQueue(progress), TaskCreationOptions.LongRunning);

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

                /* Update UI */
                workspaceTextBox.Text = iseClient.baseWorkspace;
		        userNameTextBox.Text = Properties.Settings.Default["ADUserName"].ToString();
                
                assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetVariable);
		        assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetCredential);
                //assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetCertificate);
                //assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetConnection);

                setRunbookAndAssetNonSelectionButtonState(false);
                setAssetSelectionButtonState(false);
                setRunbookSelectionButtonState(false);

                // Generate self signed certificate for encrypting local assets in the current user store Cert:\CurrentUser\My\
                var certObj = new AutomationSelfSignedCertificate();
                String selfSignedThumbprint = certObj.CreateSelfSignedCertificate();
                certificateTextBox.Text = selfSignedThumbprint;
                UpdateStatusBox(configurationStatusTextBox, "Certificate to use for encrypting local assets is " + selfSignedThumbprint);


                startContinualGet();
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

        public String getEncryptionCertificateThumbprint()
        {
            if (!(certificateTextBox.Text == "" || certificateTextBox.Text == "none"))
            {
                return certificateTextBox.Text;
            }
            else
            {
                return null;
            }
        }

        public void setRunbookAndAssetNonSelectionButtonState(bool enabled) {
            ButtonRefreshAssetList.IsEnabled = enabled;
            ButttonNewAsset.IsEnabled = enabled;
        }

        public void setRunbookSelectionButtonState(bool enabled)
        {
            /*
            ButtonDownloadRunbook.IsEnabled = enabled;
            ButtonOpenRunbook.IsEnabled = enabled;
            ButtonPublishRunbook.IsEnabled = enabled;
            ButtonUploadRunbook.IsEnabled = enabled;
             */
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

        public void startContinualGet() {

            // Set timer interval to 30 seconds
            refreshTimer.Interval = 30000;

            // Set the function to run when timer fires
            refreshTimer.Elapsed += new ElapsedEventHandler(refresh);

            refreshTimer.Start();
        }

        public void refresh(object source, ElapsedEventArgs e) {
            this.Dispatcher.Invoke((Action)(() =>
            {
                refreshAssets();
                // TODO: add refresh runbooks
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
                    assetsListView.ItemsSource = await getAssetsOfType("AutomationVariable");
                }
                else if (selectedAssetType == AutomationISE.Model.Constants.assetCredential)
                {
                    assetsListView.ItemsSource = await getAssetsOfType("AutomationCredential");
                }
                else if (selectedAssetType == AutomationISE.Model.Constants.assetConnection)
                {
                    assetsListView.ItemsSource = await getAssetsOfType("AutomationConnection");
                }
                else if (selectedAssetType == AutomationISE.Model.Constants.assetCertificate)
                {
                    assetsListView.ItemsSource = await getAssetsOfType("AutomationCertificate");
                }

                tokenExpired = false;
            }
            catch (Exception exception)
            {
                var showError = true;
                
                // If the message is not token expired, or if this is the first time we'd show token expired message
                // since previously being connected, show a dialog
                if (exception.HResult == -2146233088)
                {
                    if (tokenExpired)
                    {
                        showError = false;
                    }

                    tokenExpired = true;
                }

                if (showError)
                {
                    var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
                }
            }
        }

        private async void loginButton_Click(object sender, RoutedEventArgs e)
        {
            try {
		        //TODO: probably refactor this a little
                UpdateStatusBox(configurationStatusTextBox, "Launching login window");
                iseClient.azureADAuthResult = AutomationISE.Model.AuthenticateHelper.GetInteractiveLogin(userNameTextBox.Text);

                userNameTextBox.Text = iseClient.azureADAuthResult.UserInfo.DisplayableId;
                Properties.Settings.Default["ADUserName"] = userNameTextBox.Text;
                Properties.Settings.Default.Save();

                UpdateStatusBox(configurationStatusTextBox, Properties.Resources.RetrieveSubscriptions);
                refreshTimer.Start();
                IList<Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse.Subscription> subscriptions = await iseClient.GetSubscriptions();
                //TODO: what if there are no subscriptions? Does this still work?
                if (subscriptions.Count > 0)
                {
                    UpdateStatusBox(configurationStatusTextBox, Properties.Resources.FoundSubscriptions);
                    subscriptionComboBox.ItemsSource = subscriptions;
                    subscriptionComboBox.DisplayMemberPath = "SubscriptionName";
                    subscriptionComboBox.SelectedItem = subscriptionComboBox.Items[0];
                }
                else UpdateStatusBox(configurationStatusTextBox, Properties.Resources.NoSubscriptions);
                refreshTimer.Start();
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
                accountsComboBox.IsEnabled = false;
                iseClient.currSubscription = (Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse.Subscription)subscriptionComboBox.SelectedValue;
                if (iseClient.currSubscription != null)
                {
                    refreshTimer.Stop();
                    UpdateStatusBox(configurationStatusTextBox, Properties.Resources.RetrieveAutomationAccounts);
                    IList<AutomationAccount> automationAccounts = await iseClient.GetAutomationAccounts();
                    accountsComboBox.ItemsSource = automationAccounts;
                    accountsComboBox.DisplayMemberPath = "Name";
                    if (accountsComboBox.HasItems)
                    {
                        UpdateStatusBox(configurationStatusTextBox, Properties.Resources.FoundAutomationAccounts);
                        accountsComboBox.SelectedItem = accountsComboBox.Items[0];
                        accountsComboBox.IsEnabled = true;
                    }
                    else UpdateStatusBox(configurationStatusTextBox, Properties.Resources.NoAutomationAccounts);
                    refreshTimer.Start();
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
                    setRunbookAndAssetNonSelectionButtonState(true);
                    UpdateStatusBox(configurationStatusTextBox,"Workspace location is: " + iseClient.currWorkspace);
                    UpdateStatusBox(configurationStatusTextBox, "Save new runbooks you wish to upload to Azure Automation in this folder");
                    /* Update Runbooks */
                    UpdateStatusBox(configurationStatusTextBox, "Getting runbook data...");
                    runbookListViewModel = new ObservableCollection<AutomationRunbook>(await AutomationRunbookManager.GetAllRunbookMetadata(iseClient.automationManagementClient, 
                          iseClient.currWorkspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name));
                    UpdateStatusBox(configurationStatusTextBox, "Done getting runbook data");
                    /* Update Assets */
                    //TODO: this is not quite checking what we need it to check
                    if (!iseClient.AccountWorkspaceExists())
                    {
                        UpdateStatusBox(configurationStatusTextBox, "Downloading assets...");
                        await downloadAllAssets();
                        UpdateStatusBox(configurationStatusTextBox, "Assets downloaded");
                    }
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
                    assetsComboBox.SelectedValue = AutomationISE.Model.Constants.assetVariable;
                    ButtonRefreshAssetList.IsEnabled = true;

                    //TODO: possibly rename/refactor this
                    refresh(null, null);
                }
            }
            catch (Exception exception)
            {
                var detailsDialog = MessageBox.Show(exception.Message);
            }

        }

        private void assetsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            setAssetSelectionButtonState(assetsListView.SelectedItems.Count > 0);
        } 

        private void assetsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            refreshAssets();
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

        private void ButtonDownloadAsset_Click(object sender, RoutedEventArgs e)
        {
            downloadAssets(getSelectedAssets());
            refreshAssets();
        }

        private async void ButtonUploadAsset_Click(object sender, RoutedEventArgs e)
        {
            await uploadAssets(getSelectedAssets());
            refreshAssets();
        }

        private void ButtonDeleteAsset_Click(object sender, RoutedEventArgs e)
        {
            deleteAssets(getSelectedAssets());
            refreshAssets();
        }

        private void ButtonRefreshAssetList_Click(object sender, RoutedEventArgs e)
        {
            refreshAssets();
        }

        private bool ConfirmRunbookDownload()
        {
            String message = "Are you sure you want to import the cloud's copy of this runbook?\nAny changes you have made to it locally will be overwritten.";
            String header = "Download Runbook";
            System.Windows.Forms.DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(message, header, System.Windows.Forms.MessageBoxButtons.YesNo);
            if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                return true;
            return false;
        }

        private void ButtonDownloadRunbook_Click(object sender, RoutedEventArgs e)
        {
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            if (selectedRunbook == null)
            {
                MessageBox.Show("No runbook selected.");
                return;
            }
            ButtonDownloadRunbook.IsEnabled = false;
            if (selectedRunbook.localFileInfo != null && File.Exists(selectedRunbook.localFileInfo.FullName) && !ConfirmRunbookDownload())
            {
                ButtonDownloadRunbook.IsEnabled = true;
                return;
            }
            //downloadQueue.Add(new RunbookDownloadJob(selectedRunbook)); //blocks if queue is at capacity
            if (downloadQueue.TryAdd(new RunbookDownloadJob(selectedRunbook))) //TryAdd() immediately returns false if queue is at capacity
                JobsRemainingLabel.Text = "(" + downloadQueue.Count + " remaining)";
            ButtonDownloadRunbook.IsEnabled = true;
        }

        private async Task processJobsFromQueue(IProgress<Tuple<string, int>> progress)
        {
            int completed = 0;
            while (true)
            {
                RunbookDownloadJob job = downloadQueue.Take(); //blocks until there is something to take
                progress.Report(Tuple.Create(job.Runbook.Name, ++completed));
                try
                {
                    await AutomationRunbookManager.DownloadRunbook(job.Runbook, iseClient.automationManagementClient,
                                iseClient.currWorkspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The runbook could not be downloaded.\r\nError details: " + ex.Message);
                }
                await Task.Delay(5000); //simulate work taking longer, for testing
                if (downloadQueue.Count == 0)
                {
                    progress.Report(Tuple.Create("", completed));
                    completed = 0;
                }
            }
        }

        private void RunbooksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //TODO: factor into setRunbookSelectionButtonState()
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            if (selectedRunbook != null && selectedRunbook.localFileInfo != null && File.Exists(selectedRunbook.localFileInfo.FullName))
            {
                ButtonOpenRunbook.IsEnabled = true;
                ButtonPublishRunbook.IsEnabled = true;
            }
            else
            {
                ButtonOpenRunbook.IsEnabled = false;
                ButtonPublishRunbook.IsEnabled = false;
            }
            ButtonDownloadRunbook.IsEnabled = true;
        }

        private void ButtonOpenRunbook_Click(object sender, RoutedEventArgs e)
        {
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            if (selectedRunbook == null)
            {
                MessageBox.Show("No runbook selected.");
                return;
            }
            try
            {
                HostObject.CurrentPowerShellTab.Files.Add(selectedRunbook.localFileInfo.FullName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The runbook could not be opened.\r\nError details: " + ex.Message, "Error");
            }
        }

        private async void ButtonPublishRunbook_Click(object sender, RoutedEventArgs e)
        {
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            if (selectedRunbook == null)
            {
                MessageBox.Show("No runbook selected.");
                return;
            }
            try
            {
                /* Update UI */
                ButtonPublishRunbook.IsEnabled = false;
                ButtonDownloadRunbook.IsEnabled = false;
                ButtonUploadRunbook.IsEnabled = false;
                ButtonPublishRunbook.Content = "Publishing...";
                /* Do the uploading */
                //TODO (?): Check if you are overwriting or missing draft content in the cloud
                await AutomationRunbookManager.PublishRunbook(selectedRunbook, iseClient.automationManagementClient,
                            iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The runbook could not be published.\r\nDetails: " + ex.Message, "Error");
            }
            finally
            {
                /* Update UI */
                RunbooksListView.Items.Refresh();
                ButtonPublishRunbook.IsEnabled = true;
                ButtonDownloadRunbook.IsEnabled = true;
                ButtonUploadRunbook.IsEnabled = true;
                ButtonPublishRunbook.Content = "Publish Draft";
            }
        }

        private async void ButtonRefreshRunbookList_Click(object sender, RoutedEventArgs e)
        {
            ButtonRefreshRunbookList.IsEnabled = false;
            ButtonRefreshRunbookList.Content = "Refreshing...";
            try
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
                    Debug.WriteLine(runbookWithName[curr.Name].AuthoringState);
                    curr.Parameters = runbookWithName[curr.Name].Parameters;
                    curr.LastModifiedCloud = runbookWithName[curr.Name].LastModifiedCloud;
                    curr.LastModifiedLocal = runbookWithName[curr.Name].LastModifiedLocal;
                    //TODO: update sync status
                    runbookWithName.Remove(curr.Name);
                }
                foreach (String name in runbookWithName.Keys)
                {
                    runbookListViewModel.Add(runbookWithName[name]);
                    Debug.WriteLine(name);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("The runbook list could not be refreshed.\r\nError details: " + ex.Message, "Error");
            }
            finally
            {
                ButtonRefreshRunbookList.IsEnabled = true;
                ButtonRefreshRunbookList.Content = "Refresh";
            }
        }

        private async void ButtonUploadRunbook_Click(object sender, RoutedEventArgs e)
        {
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            if (selectedRunbook == null)
            {
                MessageBox.Show("No runbook selected.");
                return;
            }
            ButtonUploadRunbook.IsEnabled = false;
            try
            {
                await AutomationRunbookManager.UploadRunbookAsDraft(selectedRunbook, iseClient.automationManagementClient,
                        iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount);
            }
            catch(Exception ex)
            {
                MessageBox.Show("The runbook could not be uploaded.\r\nError details: " + ex.Message, "Error");
            }
            finally
            {
                ButtonUploadRunbook.IsEnabled = true;
            }
        }

        private async void ButtonTestRunbook_Click(object sender, RoutedEventArgs e)
        {
            AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
            if (selectedRunbook == null)
            {
                MessageBox.Show("No runbook selected.");
                return;
            }
            RunbookDraft draft = null;
            try
            {
                draft = await AutomationRunbookManager.GetRunbookDraft(selectedRunbook.Name, iseClient.automationManagementClient,
                            iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
            }
            catch
            {
                MessageBox.Show("Error: couldn't connect to Azure");
                return;
            }
            if (draft.InEdit == false)
            {
                //TODO: verify that it is indeed in the published state
                MessageBox.Show("This runbook has no draft to test because it is in a 'Published' state.");
                return;
            }
            //Job creation parameters
            TestJobCreateParameters jobCreationParams = new TestJobCreateParameters();
            jobCreationParams.RunbookName = selectedRunbook.Name;
            if (draft.Parameters.Count > 0)
            {
                /* User needs to specify values for them */
                RunbookParamDialog paramDialog = new RunbookParamDialog(draft.Parameters);
                if (paramDialog.ShowDialog() == true)
                    jobCreationParams.Parameters = paramDialog.paramValues;
                else
                    return;
            }
            /* start the test job */
            TestJobCreateResponse jobResponse = null;
            try {
                jobResponse = await iseClient.automationManagementClient.TestJobs.CreateAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name, 
                    iseClient.currAccount.Name, jobCreationParams, new CancellationToken());
            } catch {
                MessageBox.Show("The test job could not be submitted to Azure.", "Error");
                return;
            }
            if (jobResponse == null || jobResponse.StatusCode != System.Net.HttpStatusCode.Created)
            {
                MessageBox.Show("The test job could not be created.", "Error");
            }
            else
            {
                try {
                    TestJobOutputWindow jobWindow = new TestJobOutputWindow(jobCreationParams.RunbookName, jobResponse, iseClient);
                    jobWindow.Show();
                } catch (Exception exception)
                {
                    MessageBox.Show(exception.Message, "Error");
                    return;
                }
            }
        }

        private void createOrUpdateCredentialAsset(string credentialAssetName, AutomationCredential credToEdit)
        {
            var dialog = new NewOrEditCredentialDialog(credToEdit);
            
            if (dialog.ShowDialog() == true)
            {
                var assetsToSave = new List<AutomationAsset>();

                var newCred = new AutomationCredential(credentialAssetName, dialog.username, dialog.password);
                assetsToSave.Add(newCred);

                AutomationAssetManager.SaveLocally(iseClient.currWorkspace, assetsToSave, getEncryptionCertificateThumbprint());
                refreshAssets();
            }
        }

        private void createOrUpdateVariableAsset(string variableAssetName, AutomationVariable variableToEdit)
        {
            var dialog = new NewOrEditVariableDialog(variableToEdit);

            if (dialog.ShowDialog() == true)
            {
                var assetsToSave = new List<AutomationAsset>();

                var newVariable = new AutomationVariable(variableAssetName, dialog.value, dialog.encrypted);
                assetsToSave.Add(newVariable);

                AutomationAssetManager.SaveLocally(iseClient.currWorkspace, assetsToSave, getEncryptionCertificateThumbprint());
                refreshAssets();
            }
        }

        private void ButttonNewAsset_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ChooseNewAssetTypeDialog();
                
            if (dialog.ShowDialog() == true)
            {
                if (dialog.newAssetType == AutomationISE.Model.Constants.assetVariable)
                {
                    createOrUpdateVariableAsset(dialog.newAssetName, null);
                }
                else if (dialog.newAssetType == AutomationISE.Model.Constants.assetCredential)
                {
                    createOrUpdateCredentialAsset(dialog.newAssetName, null);
                }
                else if (dialog.newAssetType == AutomationISE.Model.Constants.assetConnection)
                {
                    
                }
                else if (dialog.newAssetType == AutomationISE.Model.Constants.assetCertificate)
                {
                    
                }
            }
        }

        private void ButtonEditAsset_Click(object sender, RoutedEventArgs e)
        {
            var asset = getSelectedAssets().ElementAt(0);
            
            if(asset is AutomationCredential) {
                createOrUpdateCredentialAsset(asset.Name, (AutomationCredential)asset);
            }
            else if(asset is AutomationVariable) {
                createOrUpdateVariableAsset(asset.Name, (AutomationVariable)asset);
            }
        }

        private void certificateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AutomationSelfSignedCertificate.SetCertificateInConfigFile(certificateTextBox.Text);
                UpdateStatusBox(configurationStatusTextBox, "Updated certificate thumbprint to use for encryption / decryption of assets");
            }
            catch (Exception ex)
            {
                MessageBox.Show("The thumbprint could not be updated " + ex.Message, "Error");
            }
        }

        private void certificateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }

    public class RunbookDownloadJob
    {
        public AutomationRunbook Runbook { get; set; }
        public RunbookDownloadJob(AutomationRunbook rb)
        {
            this.Runbook = rb;
        }
    }
}
