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
using System.Drawing;
using System.Net;
using System.Management.Automation;
using Microsoft.Azure.Management.Storage.Models;
using System.Management.Automation.Runspaces;

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
        private ObservableCollection<AutomationDSC> DSCListViewModel;
        private ObservableCollection<AutomationAsset> assetListViewModel;
        private ObservableCollection<AutomationModule> moduleListViewModel;
        private ISet<ConnectionType> connectionTypes;
        private ISet<AutomationAsset> assets;
        private ListSortDirection runbookCurrSortDir;
        private string runbookCurrSortProperty;
        private ListSortDirection configurationCurrSortDir;
        private string configurationCurrSortProperty;
        private ListSortDirection moduleCurrSortDir;
        private string moduleCurrSortProperty;
        private ListSortDirection assetCurrSortDir;
        private string assetCurrSortProperty;
        private int numBackgroundTasks = 0;
        private Object backgroundWorkLock;
        private Object refreshScriptsLock;
        private Object refreshModulesLock;
        private Storyboard progressSpinnerStoryboard;
        private Storyboard progressSpinnerStoryboardReverse;
        private Storyboard miniProgressSpinnerStoryboard;
        private Storyboard miniProgressSpinnerStoryboardReverse;
        private bool promptShortened;
        private string certificateThumbprint;
        private FileSystemWatcher fileWatcher;

        private string VSStudio;
        private string VSCode;
        public ObjectModelRoot HostObject { get; set; }
        string lastUpdated = "";
        private string addOnVersion = null;
        Dictionary<string, string> localScriptsParsed = new Dictionary<string, string>();
        Dictionary<string, PSObject> localModulesParsed = new Dictionary<string, PSObject>();
        private bool collectUsage = true;


        public AutomationISEControl()
        {
            try
            {
                InitializeComponent();
                iseClient = new AutomationISEClient();
                /* Spinner animation stuff */
                backgroundWorkLock = new Object();
                refreshScriptsLock = new Object();
                refreshModulesLock = new Object();
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

                /* Set up file system watcher */
                fileWatcher = new System.IO.FileSystemWatcher();

                /* Update UI */
                workspaceTextBox.Text = iseClient.baseWorkspace;
                userNameTextBox.Text = Properties.Settings.Default["ADUserName"].ToString();
                subscriptionComboBox.IsEnabled = false;
                accountsComboBox.IsEnabled = false;

                assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetConnection);
                assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetCredential);
                assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetVariable);
                assetsComboBox.Items.Add(AutomationISE.Model.Constants.assetCertificate);


                setCreationButtonStatesTo(false);
                setAllAssetButtonStatesTo(false);
                assetsComboBox.IsEnabled = false;
                setAllRunbookButtonStatesTo(false);
                setAllConfigurationButtonStatesTo(false);
                ButtonRefreshModule.IsEnabled = false;

                // Generate self-signed certificate for encrypting local assets in the current user store Cert:\CurrentUser\My\
                var certObj = new AutomationSelfSignedCertificate();
                certificateThumbprint = certObj.CreateSelfSignedCertificate(iseClient.baseWorkspace);
                certificateTextBox.Text = certificateThumbprint;
                UpdateStatusBox(configurationStatusTextBox, "Thumbprint of certificate used to encrypt local assets: " + certificateThumbprint);

                // Load feedback and help page preemptively
                addOnVersion = PowerShellGallery.GetLocalVersion();
                surveyBrowserControl.Navigate(new Uri(Constants.feedbackURI));
                helpBrowserControl.Navigate(new Uri(Constants.helpURI + "?version=" + addOnVersion));

                // Check if this is the latest version from PowerShell Gallery
                if (PowerShellGallery.CheckGalleryVersion())
                {
                    versionLabel.Foreground = System.Windows.Media.Brushes.Red;
                    versionLabel.Content = "New AzureAutomationAuthoringToolkit available";
                }
                else
                {
                    versionLabel.Visibility = Visibility.Collapsed;
                    versionButton.Visibility = Visibility.Collapsed;
                }
                runAscheckBox.IsChecked = Properties.Settings.Default.RunAs;

                IDEComboBox.Visibility = Visibility.Collapsed;
                IDEEditorLabel.Visibility = Visibility.Collapsed;

                AzureEnvironmentComboBox.Items.Add("Public Azure");
                AzureEnvironmentComboBox.Items.Add("US Government Azure");

                String AzureEnvironment = Properties.Settings.Default.loginAuthority;
                if (AzureEnvironment == Constants.publicLoginAuthority)
                {
                    AzureEnvironmentComboBox.SelectedItem = "Public Azure";
                }
                if (AzureEnvironment == Constants.USGovernmentLoginAuthority)
                {
                    AzureEnvironmentComboBox.SelectedItem = "US Government Azure";
                }

            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

        /// <summary>
        /// Checks when a property changes on selected tab
        /// If the property change is getting focus, then update the runbook list to match this
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CurrentPowerShellTab_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (HostObject != null)
            {
                if (e.PropertyName == "LastEditorWithFocus")
                {
                    if (runbookListViewModel != null && DSCListViewModel != null && HostObject.CurrentPowerShellTab.Files.Count > 0)
                    {
                        foreach (AutomationRunbook runbook in runbookListViewModel)
                        {
                            if (runbook.Name.Equals(Path.GetFileNameWithoutExtension(HostObject.CurrentPowerShellTab.Files.SelectedFile.DisplayName)))
                            {
                                RunbooksListView.SelectedItem = runbook;
                                RunbooksListView.ScrollIntoView(RunbooksListView.SelectedItem);
                                break;
                            }
                        }
                        foreach (AutomationDSC configuration in DSCListViewModel)
                        {
                            if (configuration.localFileInfo != null)
                            {
                                if (Path.GetFileNameWithoutExtension(configuration.localFileInfo.ToString()).Equals(Path.GetFileNameWithoutExtension(HostObject.CurrentPowerShellTab.Files.SelectedFile.DisplayName)))
                                {
                                    DSCListView.SelectedItem = configuration;
                                    DSCListView.ScrollIntoView(DSCListView.SelectedItem);
                                    break;
                                }
                            }
                        }
                    }
                }
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
            ButtonCreateConfiguration.IsEnabled = enabled;
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

        public void setAllConfigurationButtonStatesTo(bool enabled)
        {
            ButtonDeleteConfiguration.IsEnabled = enabled;
            ButtonDownloadConfiguration.IsEnabled = enabled;
            ButtonOpenConfiguration.IsEnabled = enabled;
            ButtonUploadConfiguration.IsEnabled = enabled;
            ButtonCompileConfiguration.IsEnabled = enabled;
        }
        public void setAllModuleButtonStatesTo(bool enabled)
        {
            ButtonDeleteModule.IsEnabled = enabled;
            ButtonUploadModule.IsEnabled = enabled;
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

        public async Task<ISet<ConnectionType>> getConnectionTypes()
        {
            return await AutomationAssetManager.GetConnectionTypes(iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
        }

        public SortedSet<AutomationAsset> getAssetsOfType(String type)
        {
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
            try
            {
                if (connectionTypes != null) connectionTypes.Clear();
                await AutomationAssetManager.DownloadAllFromCloud(iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint(), connectionTypes);
            }
            catch (Exception exception)
            {
                System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        public void downloadAssets(ICollection<AutomationAsset> assetsToDownload)
        {
            try
            {
                AutomationAssetManager.DownloadFromCloud(assetsToDownload, iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint(), connectionTypes);
            }
            catch (Exception exception)
            {
                System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);

            }
        }

        public async Task uploadAssets(ICollection<AutomationAsset> assetsToUpload)
        {
            try
            {
                await AutomationAssetManager.UploadToCloud(assetsToUpload, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);

                // Since the cloud assets uploaded will have a last modified time of now, causing them to look newer than their local counterparts,
                // download the assets after upload to force last modified time between local and cloud to be the same, showing them as in sync (which they are)
                downloadAssets(assetsToUpload);
            }
            catch (Exception exception)
            {
                System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        public void deleteAssets(ICollection<AutomationAsset> assetsToDelete)
        {
            try
            {
                bool deleteLocally = true;
                bool deleteFromCloud = true;

                // when asset is only local or only in cloud, we know where they want to delete it from. But when asset is both local and cloud,
                // they may not have meant to delete it from cloud, so ask them 
                foreach (var assetToDelete in assetsToDelete)
                {
                    if (assetToDelete.LastModifiedCloud != null && assetToDelete.LastModifiedLocal != null)
                    {
                        var messageBoxResult = System.Windows.Forms.MessageBox.Show(
                            "At least some of the selected assets have both local and cloud versions. Do you want to also delete the cloud versions of these assets?",
                            "Delete Confirmation",
                            System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Warning
                        );

                        if (messageBoxResult == System.Windows.Forms.DialogResult.No)
                        {
                            deleteFromCloud = false;
                        }
                        else if (messageBoxResult == System.Windows.Forms.DialogResult.Cancel)
                        {
                            deleteFromCloud = false;
                            deleteLocally = false;
                        }

                        break;
                    }
                }

                AutomationAssetManager.Delete(assetsToDelete, iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, deleteLocally, deleteFromCloud, getEncryptionCertificateThumbprint(), connectionTypes);
            }
            catch (Exception exception)
            {
                System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        public void refreshAuthToken(object source, ElapsedEventArgs e)
        {
            try
            {
                refreshAuthTokenTimer.Stop();
                iseClient.RefreshAutomationClientwithNewToken();
                refreshAuthTokenTimer.Start();
                if (refreshAccountDataTimer.Enabled == false) refreshAccountDataTimer.Start();
            }
            catch (Exception exception)
            {
                refreshAuthTokenTimer.Stop();
                loginButton.Content = "Sign In";
                System.Windows.Forms.MessageBox.Show("Your session expired and could not be refreshed. Please sign in again./r/nDetails: " + exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        public void refreshAccountData(object source, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    Task t = refreshRunbooks();
                    t = refreshAssets();
                    t = refreshConfigurations();
                }
                catch (Exception exception)
                {
                    refreshAccountDataTimer.Stop();
                    int tokenExpiredResult = -2146233088;
                    if (exception.HResult == tokenExpiredResult)
                    {
                        iseClient.RefreshAutomationClientwithNewToken();
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);

                    }
                }
            });
        }

        private async Task<Boolean> CheckRunAs()
        {
            // Indicates whether the RunAs can be used successfully.
            var runAsSuccess = false;
            try
            {
                // Check if local runas certificate is available in the automation account
                AutomationConnection runAsConnection = (AutomationConnection)assets.FirstOrDefault(x => x.Name == "AzureRunAsConnection" && x.GetType().Name == "AutomationConnection");
                if (runAsConnection == null)
                {
                    System.Windows.Forms.MessageBox.Show("RunAs account is not configured. Please create from the portal", "Warning", System.Windows.Forms.MessageBoxButtons.OK,System.Windows.Forms.MessageBoxIcon.Warning);
                    return false;
                }
                // Check if a certificate for RunAs is already present. It will be in the form (RunAs + account name + hostname)
                AutomationCertificate certificateAsset = (AutomationCertificate)assets.FirstOrDefault(x => x.Name == ("RunAs" + ((AutomationAccount)accountsComboBox.SelectedValue).Name + Dns.GetHostName()) && x.GetType().Name == "AutomationCertificate");
                // If certificate is present in cloud, update RunAsConnection connection thumbprint if needed
                if (certificateAsset != null && certificateAsset.SyncStatus != "Local Only")
                {
                    var assetsToSave = new List<AutomationAsset>();
                    var thumbprint = certificateAsset.getThumbprint();
                    var connectionFields = runAsConnection.getFields();
                    // If local RunAs connection does not contain the local certificate thumprint, then update it
                    if (connectionFields["CertificateThumbprint"].ToString() != thumbprint)
                    {
                        connectionFields["CertificateThumbprint"] = thumbprint;
                        assetsToSave.Add(runAsConnection);

                    }

                    // Update RunAsCertificate to point to this local certificate
                    AutomationCertificate runAsCertificate = (AutomationCertificate)assets.FirstOrDefault(x => x.Name == "AzureRunAsCertificate" && x.GetType().Name == "AutomationCertificate");
                    runAsCertificate.setThumbprint(thumbprint);
                    assetsToSave.Add(runAsCertificate);
                    AutomationAssetManager.SaveLocally(iseClient.currWorkspace, assetsToSave, getEncryptionCertificateThumbprint(), connectionTypes);
                    refreshAssets();
                    certificateAsset.UpdateSyncStatus();
                    runAsCertificate.UpdateSyncStatus();
                    runAsSuccess = true;
                }
                else
                {
                    // Create local cert and upload to the cloud

                    // Refresh token to work against graph API
                    var token = AuthenticateHelper.RefreshTokenByAuthority(iseClient.currSubscription.Authority, Constants.graphURI);
                    
                    // Create new instance of the runAsClient so we can update the AD application with the new certificate
                    var runAsClient = new RunAs(token);

                    // Create certificate and update AD application with new certificate
                    var connectionFields = runAsConnection.getFields();
                    var newCertificate = await runAsClient.CreateLocalRunAs(connectionFields["ApplicationId"].ToString(), "RunAs" + ((AutomationAccount)accountsComboBox.SelectedValue).Name + Dns.GetHostName());
                    if (newCertificate != null)
                    {
                        // Upload local certificate to automation account so it could work in the service also
                        // if the local RunAs connection is uploaded. 
                        var properties = new CertificateCreateOrUpdateProperties()
                        {
                            Base64Value = Convert.ToBase64String(newCertificate.Export(X509ContentType.Pkcs12)),
                            Thumbprint = newCertificate.Thumbprint,
                            IsExportable = true
                        };

                        var cts = new CancellationTokenSource();
                        cts.CancelAfter(30000);
                        await iseClient.automationManagementClient.Certificates.CreateOrUpdateAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, new CertificateCreateOrUpdateParameters(newCertificate.FriendlyName, properties), cts.Token);

                        // Update RunAs connection with the new certificate
                        connectionFields["CertificateThumbprint"] = newCertificate.Thumbprint;
                        var assetsToSave = new List<AutomationAsset>();
                        assetsToSave.Add(runAsConnection);

                        // Update RunAsCertificate to point to this local certificate
                        AutomationCertificate runAsCertificate = (AutomationCertificate)assets.FirstOrDefault(x => x.Name == "AzureRunAsCertificate" && x.GetType().Name == "AutomationCertificate");
                        runAsCertificate.setThumbprint(newCertificate.Thumbprint);
                        assetsToSave.Add(runAsCertificate);

                        // Save new certificate to local assets store.
                        var newCert = new AutomationCertificate(newCertificate.FriendlyName, newCertificate.Thumbprint, null, null, true, true);
                        assetsToSave.Add(newCert);
                        AutomationAssetManager.SaveLocally(iseClient.currWorkspace, assetsToSave, getEncryptionCertificateThumbprint(), connectionTypes);

                        // Refresh assets and set sync status
                        refreshAssets();
                        newCert.UpdateSyncStatus();
                        runAsSuccess = true;
                    }
                }
            }
            catch (Exception Ex)
            {
                System.Windows.Forms.MessageBox.Show("Error configuring RunAs: " + Ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                runAsSuccess = false;
            }
            return runAsSuccess;
        }

        public async Task refreshAssets(bool useExistingAssetValues = false)
        {
            try
            {
                if (!useExistingAssetValues)
                {
                    assets = await AutomationAssetManager.GetAll(iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint(), connectionTypes);
                }

                var selectedAssets = getSelectedAssets();
                string selectedAssetType = (string)assetsComboBox.SelectedValue;
                if (selectedAssetType == null) return;
                if (selectedAssetType == AutomationISE.Model.Constants.assetVariable)
                {
                    mergeAssetListWith(getAssetsOfType("AutomationVariable"));
                }
                else if (selectedAssetType == AutomationISE.Model.Constants.assetCredential)
                {
                    mergeAssetListWith(getAssetsOfType("AutomationCredential"));
                }
                else if (selectedAssetType == AutomationISE.Model.Constants.assetConnection)
                {
                    mergeAssetListWith(getAssetsOfType("AutomationConnection"));
                }
                else if (selectedAssetType == AutomationISE.Model.Constants.assetCertificate)
                {
                    mergeAssetListWith(getAssetsOfType("AutomationCertificate"));
                }
                setSelectedAssets(selectedAssets);

                connectionTypes = await getConnectionTypes();
            }
            catch (Exception exception)
            {
                int tokenExpiredResult = -2146233088;
                if (exception.HResult == tokenExpiredResult)
                {
                    refreshAccountDataTimer.Stop();
                    loginButton.Content = "Sign In";
                }
                if (exception.HResult == -2146233029)
                {
                    // Waiting for data error from query. Ignore this as it is transient.
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
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
                ScriptAnalyzerTextBlock_ISEAddon.Visibility = Visibility.Collapsed;
                UpdateStatusBox(configurationStatusTextBox, "Launching login window...");
                if (loginButton.Content.ToString() == "Sign In")
                    iseClient.azureADAuthResult = AutomationISE.Model.AuthenticateHelper.GetInteractiveLogin(userNameTextBox.Text);
                else
                    iseClient.azureADAuthResult = AutomationISE.Model.AuthenticateHelper.GetInteractiveLogin(userNameTextBox.Text,"common",true);
                loginButton.Content = "Switch User";
                refreshAccountDataTimer.Stop();

                if (HostObject == null)
                {
                    IDEComboBox.Items.Clear();
                    IDEEditorLabel.Visibility = Visibility.Visible;
                    IDEComboBox.Visibility = Visibility.Visible;
                    String editor = Properties.Settings.Default["Editor"].ToString();
                    VSStudio = GetVisualStudioPath();
                    if (VSStudio != null)
                    {
                        IDEComboBox.Items.Add("Visual Studio");
                    }
                    VSCode = GetVSCodePath();
                    if (VSCode != null)
                    {
                        IDEComboBox.Items.Add("VS Code");
                    }
                    if (!String.IsNullOrEmpty(editor))
                        IDEComboBox.SelectedValue = editor;
                    else
                    {
                        if (IDEComboBox.HasItems)
                            IDEComboBox.SelectedItem = IDEComboBox.Items[0];
                    }

                    togglePromptButton.Visibility = Visibility.Collapsed;
                    ButtonInsertAssets.Content = "Copy to clipboard";
                }

                beginBackgroundWork(Properties.Resources.RetrieveSubscriptions);
                userNameTextBox.Text = iseClient.azureADAuthResult.UserInfo.DisplayableId;
                UpdateStatusBox(configurationStatusTextBox, "Logged in user: " + userNameTextBox.Text);
                Properties.Settings.Default["ADUserName"] = userNameTextBox.Text;
                Properties.Settings.Default.Save();
                subscriptionComboBox.ItemsSource = null;

                IList<AutomationISEClient.SubscriptionObject> subscriptions = await iseClient.GetSubscriptions();

                if (subscriptions.Count > 0)
                {
                    endBackgroundWork(Properties.Resources.FoundSubscriptions);
                    var subscriptionList = subscriptions.OrderBy(x => x.Name);
                    subscriptionComboBox.ItemsSource = subscriptionList;
                    subscriptionComboBox.DisplayMemberPath = "Name";
                    foreach (AutomationISEClient.SubscriptionObject selectedSubscription in subscriptionComboBox.Items)
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

                if (HostObject != null) { HostObject.CurrentPowerShellTab.PropertyChanged += new PropertyChangedEventHandler(CurrentPowerShellTab_PropertyChanged); }
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
                var detailsDialog = System.Windows.Forms.MessageBox.Show(Ex.InnerException.Message);
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
                if (subscriptionComboBox.SelectedItem != null)
                {

                    var selectedSubscription = (AutomationISEClient.SubscriptionObject)subscriptionComboBox.SelectedValue;
                    if (selectedSubscription.Name != null)
                    {
                    Properties.Settings.Default.lastSubscription = selectedSubscription.SubscriptionId;
                    Properties.Settings.Default.Save();
                    }

                    iseClient.currSubscription = (AutomationISEClient.SubscriptionObject)subscriptionComboBox.SelectedValue;
                    if (iseClient.currSubscription.Name != null)
                    {
                        beginBackgroundWork(Properties.Resources.RetrieveAutomationAccounts);
                        IList<AutomationAccount> automationAccounts = await iseClient.GetAutomationAccounts();
                        var accountList = automationAccounts.OrderBy(x => x.Name);
                        accountsComboBox.ItemsSource = accountList;
                        accountsComboBox.DisplayMemberPath = "Name";
                        if (accountsComboBox.HasItems)
                        {
                            endBackgroundWork(Properties.Resources.FoundAutomationAccounts);
                            var lastAccountName = Properties.Settings.Default.lastAutomationAccount;
                            if (lastAccountName != "")
                            {
                                // find if automation account is present in list
                                foreach (AutomationAccount selectedAccount in accountsComboBox.Items)
                                {
                                    if (selectedAccount.Name.ToString() == Properties.Settings.Default.lastAutomationAccount.ToString())
                                    {
                                        accountsComboBox.SelectedItem = selectedAccount;
                                    }
                                }
                                if (accountsComboBox.SelectedItem == null) accountsComboBox.SelectedItem = accountsComboBox.Items[0];
                            }
                            else
                            {
                                accountsComboBox.SelectedItem = accountsComboBox.Items[0];
                            }
                            accountsComboBox.IsEnabled = true;
                        }
                        else
                        {
                            endBackgroundWork(Properties.Resources.NoAutomationAccounts);
                        }
                    }
                }
                subscriptionComboBox.IsEnabled = true;
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
                accountsComboBox.IsEnabled = false;
                ButtonRefreshModule.IsEnabled = false;
                iseClient.currAccount = account;
                refreshAccountDataTimer.Stop();
                if (account != null)
                {
                    /* Update Status */
                    UpdateStatusBox(configurationStatusTextBox, "Selected automation account: " + account.Name);
                    Properties.Settings.Default.lastAutomationAccount = account.Name;
                    Properties.Settings.Default.Save();

                    if (iseClient.AccountWorkspaceExists())
                        accountPathTextBox.Text = iseClient.currWorkspace;
                    // Refresh local modules

                    //Set up module view and start looking for modules
                    if (runbookListViewModel != null) runbookListViewModel.Clear();
                    ModuleListView.ItemsSource = null;
                    if (moduleListViewModel != null) moduleListViewModel.Clear();
                    RefreshModulesTask();
                    moduleListViewModel = new ObservableCollection<AutomationModule>();

                    /* Update Runbooks */
                    beginBackgroundWork("Getting account data");
                    beginBackgroundWork("Getting runbook data for " + account.Name);
                    if (runbookListViewModel != null) runbookListViewModel.Clear();
                    if (assetListViewModel != null) assetListViewModel.Clear();
                    localScriptsParsed = null;
                    await refreshLocalScripts();
                    var localScripts = await getLocalScripts();
                    runbookListViewModel = new ObservableCollection<AutomationRunbook>(await AutomationRunbookManager.GetAllRunbookMetadata(iseClient.automationManagementClient, 
                          iseClient.currWorkspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name,localScripts));
                    endBackgroundWork("Done getting runbook data");

                    /* Update Configurations */
                    beginBackgroundWork("Getting configuration data for " + account.Name);
                    if (DSCListViewModel != null) DSCListViewModel.Clear();
                    if (assetListViewModel != null) assetListViewModel.Clear();
                    DSCListViewModel = new ObservableCollection<AutomationDSC>(await AutomationDSCManager.GetAllConfigurationMetadata(iseClient.automationManagementClient,
                          iseClient.currWorkspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, localScripts));
                    endBackgroundWork("Done getting configuration data");

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
                        System.Windows.Forms.MessageBox.Show(message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);

                    }
                    /* Update UI */
                    RunbooksListView.ItemsSource = runbookListViewModel;
                    CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(RunbooksListView.ItemsSource);
                    view.Filter = FilterRunbook;
                    RunbooksListView.Items.SortDescriptions.Clear();
                    RunbooksListView.Items.SortDescriptions.Add(new SortDescription("LastModifiedLocal", ListSortDirection.Descending));

                    DSCListView.ItemsSource = DSCListViewModel;
                    CollectionView DSCview = (CollectionView)CollectionViewSource.GetDefaultView(DSCListView.ItemsSource);
                    DSCview.Filter = FilterConfiguration;
                    DSCListView.Items.SortDescriptions.Clear();
                    DSCListView.Items.SortDescriptions.Add(new SortDescription("LastModifiedLocal", ListSortDirection.Descending));
         
                    ModuleListView.ItemsSource = moduleListViewModel;
                    RefreshModulesTask();

                    CollectionView Moduleview = (CollectionView)CollectionViewSource.GetDefaultView(ModuleListView.ItemsSource);
                    Moduleview.Filter = FilterModule;
                    ModuleListView.Items.SortDescriptions.Clear();
                    ModuleListView.Items.SortDescriptions.Add(new SortDescription("LastModifiedCloud", ListSortDirection.Descending));

                    assetsListView.ItemsSource = assetListViewModel;
                    // Set credentials assets to be selected
                    assetsComboBox.SelectedItem = assetsComboBox.Items[1];
                    setAllRunbookButtonStatesTo(false);
                    setAllConfigurationButtonStatesTo(false);
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
                    if (HostObject != null)
                    {
                        HostObject.CurrentPowerShellTab.InvokeSynchronous("cd \"" + iseClient.currWorkspace + "\"" + ";function prompt {'PS " + pathHint + "> '}", false, 5000);
                    }
                    promptShortened = true;
                    endBackgroundWork("Finished getting data for " + account.Name);
                    refreshAccountDataTimer.Start();

                    if (Properties.Settings.Default.RunAs)
                    {
                        if (await CheckRunAs())
                        {
                            // Use RunAs connection in account to authenticate with Azure.
                            if (HostObject != null)
                            {
                                HostObject.CurrentPowerShellTab.Invoke("$RunAsConnection = Get-AutomationConnection -Name AzureRunAsConnection;try {$Login=Add-AzureRmAccount -ServicePrincipal -TenantId $RunAsConnection.TenantId -ApplicationId $RunAsConnection.ApplicationId -CertificateThumbprint $RunAsConnection.CertificateThumbprint -ErrorAction Stop}catch{Sleep 10;$Login=Add-AzureRmAccount -ServicePrincipal -TenantId $RunAsConnection.TenantId -ApplicationId $RunAsConnection.ApplicationId -CertificateThumbprint $RunAsConnection.CertificateThumbprint};Set-AzureRmContext -SubscriptionId $RunAsConnection.SubscriptionID");
                            }
                        }
                    }

                    /* Set up file watch on the current workspace */
                    fileWatcher.Path = iseClient.currWorkspace + "\\";
                    fileWatcher.Filter = "*.p*";
                    fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

                    fileWatcher.Changed += new FileSystemEventHandler(FileSystemChanged);
                    fileWatcher.Created += new FileSystemEventHandler(FileSystemChanged);
                    fileWatcher.Deleted += new FileSystemEventHandler(FileSystemChanged);
                    fileWatcher.EnableRaisingEvents = true;
                    accountsComboBox.IsEnabled = true;
                    ButtonRefreshModule.IsEnabled = true;
                }
            }
            catch (Exception exception)
            {
                accountsComboBox.IsEnabled = true;
                endBackgroundWork("Error getting account data");
                UpdateStatusBox(configurationStatusTextBox, exception.StackTrace);
                System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private void FileSystemChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                DateTime lastWriteTimeonFile = File.GetLastWriteTime(e.FullPath);
                // Shorten to seconds to prevent multiple updates
                String lastWriteTime = lastWriteTimeonFile.ToString("G");
                if (lastWriteTime != lastUpdated)
                {
                    lastUpdated = lastWriteTime;
                    Task t = new Task(delegate { refreshLocalScripts(e.FullPath); });
                    t.Start();
                    foreach (AutomationRunbook runbook in runbookListViewModel)
                    {
                        if (runbook.Name.Equals(Path.GetFileNameWithoutExtension(e.Name)))
                        {
                            runbook.LastModifiedLocal = DateTime.Now;
                            runbook.UpdateSyncStatus();
                            break;
                        }
                    }

                    foreach (AutomationDSC configuration in DSCListViewModel)
                    {
                        if (configuration.Name.Equals(Path.GetFileNameWithoutExtension(e.Name)))
                        {
                            configuration.LastModifiedLocal = DateTime.Now;
                            configuration.UpdateSyncStatus();
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Runbook could not be refreshed.\r\nError details: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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
                await refreshAssets(true);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Assets could not be refreshed.\r\nError details: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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
                System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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

        private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabItem selectedTab = (TabItem)((TabControl)sender).SelectedItem;
            if (selectedTab != null)
            {
                switch (selectedTab.Name)
                {
                    case "configurationTab":
                        break;
                    case "runbookTab":
                        break;
                    case "DSCTab":
                        break;
                    case "settingsTab":
                        break;
                    case "ModuleTab":
                        break;
                    case "feedbackTab":
                        break;
                    case "helpTab":
                        helpBrowserControl.Navigate(new Uri(Constants.helpURI + "?version=" + addOnVersion));
                        break;
                    default:
                        Debug.WriteLine("Couldn't find tab handler with name: " + selectedTab.Name);
                        return;
                }
            }
            return;
        }

        private async void ButtonDownloadAsset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                beginBackgroundWork("Downloading selected assets...");
                downloadAssets(getSelectedAssets());
                var selectedAssets = getSelectedAssets();
                foreach (var asset in selectedAssets)
                {
                    asset.LastModifiedCloud = asset.LastModifiedLocal;
                    asset.UpdateSyncStatus();
                }
                endBackgroundWork("Assets downloaded.");
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error downloading assets.");
                System.Windows.Forms.MessageBox.Show("Assets could not be downloaded.\r\nError details: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
           }
        }

        private async void ButtonUploadAsset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                beginBackgroundWork("Uploading selected assets...");
                await uploadAssets(getSelectedAssets());
                var selectedAssets = getSelectedAssets();
                foreach (var asset in selectedAssets)
                {
                    asset.LastModifiedCloud = asset.LastModifiedLocal;
                    asset.UpdateSyncStatus();
                }
                endBackgroundWork("Assets uploaded.");
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error uploading assets.");
                System.Windows.Forms.MessageBox.Show("Assets could not be uploaded.\r\nError details: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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
                System.Windows.Forms.MessageBox.Show("Assets could not be deleted.\r\nError details: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
           }
        }

        private void ButtonInsertAsset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var asset = getSelectedAssets().ElementAt(0);
                if (HostObject != null)
                {
                    HostObject.CurrentPowerShellTab.Files.SelectedFile.Editor.InsertText(asset.getGetCommand());
                }
                else
                {
                    AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;
                    if (selectedRunbook == null) { System.Windows.Forms.MessageBox.Show("Select a runbook so the type (PowerShell or Python) can be determined", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);  return; };
                    if (selectedRunbook.RunbookType == Constants.RunbookType.PowerShellScript)
                    {
                        Clipboard.SetText(asset.getGetCommand(Constants.RunbookType.PowerShellScript));
                    }
                    if (selectedRunbook.RunbookType == Constants.RunbookType.Python2)
                    {
                        Clipboard.SetText(asset.getGetCommand(Constants.RunbookType.Python2));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Asset could not be inserted.\r\nError details: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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

        private bool ConfirmConfigurationDownload(string name)
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
                        runbook.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.InSync;
                    }
                    catch (OperationCanceledException)
                    {
                        endBackgroundWork("Downloading " + runbook.Name + " timed out.");
                    }
                    catch (Exception ex)
                    {
                        endBackgroundWork("Error downloading runbook " + runbook.Name);
                        System.Windows.Forms.MessageBox.Show("The runbook " + runbook.Name + " could not be downloaded.\r\nError details: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                }

                if (count == 1) endBackgroundWork("Downloaded " + name + ".");
                else if (count > 1) endBackgroundWork("Downloaded " + count + " runbooks.");
                else endBackgroundWork();
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error downloading runbooks.");
                System.Windows.Forms.MessageBox.Show(ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                SetButtonStatesForSelectedRunbook();
            }
        }

        private async void ButtonDownloadConfiguration_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonDownloadConfiguration.IsEnabled = false;
                /* 
                 * These outer, empty calls to [begin|end]BackgroundWork() prevent the spinner from stopping
                 * every time a configuration download finishes, which can make the UI look jittery.
                 */
                beginBackgroundWork();
                int count = 0;
                string name = "";
                foreach (Object obj in DSCListView.SelectedItems)
                {
                    AutomationDSC configuration = (AutomationDSC)obj;
                    if (configuration.SyncStatus == AutomationDSC.Constants.SyncStatus.LocalOnly ||
                        configuration.SyncStatus == AutomationDSC.Constants.SyncStatus.InSync)
                        continue;
                    if (configuration.localFileInfo != null && File.Exists(configuration.localFileInfo.FullName) && !ConfirmConfigurationDownload(configuration.Name))
                        continue;
                    try
                    {
                        beginBackgroundWork("Downloading configuration " + configuration.Name + "...");
                        await AutomationDSCManager.DownloadConfiguration(configuration, iseClient.automationManagementClient,
                                    iseClient.currWorkspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount);
                        endBackgroundWork("Downloaded " + configuration.Name + ".");
                        count++;
                        name = configuration.Name;
                        configuration.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.InSync;
                    }
                    catch (OperationCanceledException)
                    {
                        endBackgroundWork("Downloading " + configuration.Name + " timed out.");
                    }
                    catch (Exception ex)
                    {
                        endBackgroundWork("Error downloading configuration " + configuration.Name);
                        System.Windows.Forms.MessageBox.Show("The configuration " + configuration.Name + " could not be downloaded.\r\nError details: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                }

                if (count == 1) endBackgroundWork("Downloaded " + name + ".");
                else if (count > 1) endBackgroundWork("Downloaded " + count + " configurations.");
                else endBackgroundWork();
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error downloading configurations.");
                System.Windows.Forms.MessageBox.Show(ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                SetButtonStatesForSelectedConfiguration();
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

        private void SetButtonStatesForSelectedConfiguration()
        {
            AutomationDSC selectedConfiguration = (AutomationDSC)DSCListView.SelectedItem;
            if (selectedConfiguration == null)
            {
                setAllConfigurationButtonStatesTo(false);
                ButtonCreateConfiguration.IsEnabled = true;
                return;
            }
            ButtonDeleteConfiguration.IsEnabled = true;
            ButtonCreateConfiguration.IsEnabled = true;
            /* Set Download button status */
            if (selectedConfiguration.SyncStatus == AutomationDSC.Constants.SyncStatus.LocalOnly)
                ButtonDownloadConfiguration.IsEnabled = false;
            else
                ButtonDownloadConfiguration.IsEnabled = true;
            /* Set Open and Upload button status */
            if (selectedConfiguration.localFileInfo != null && File.Exists(selectedConfiguration.localFileInfo.FullName))
            {
                ButtonOpenConfiguration.IsEnabled = true;
                ButtonUploadConfiguration.IsEnabled = true;
            }
            else
            {
                ButtonOpenConfiguration.IsEnabled = false;
                ButtonUploadConfiguration.IsEnabled = false;
            }
            /* Set Compile button status */
            if (selectedConfiguration.SyncStatus == AutomationDSC.Constants.SyncStatus.LocalOnly)
            {
                ButtonCompileConfiguration.IsEnabled = false;
            }
            else
            {
                ButtonCompileConfiguration.IsEnabled = true;
            }
        }

        private void SetButtonStatesForSelectedModule()
        {
            AutomationModule selectedModule = (AutomationModule)ModuleListView.SelectedItem;
            if (selectedModule == null)
            {
                setAllModuleButtonStatesTo(false);
                return;
            }
            if (selectedModule.SyncStatus == AutomationModule.Constants.SyncStatus.CloudOnly)
            {
                ButtonDeleteModule.IsEnabled = true;
                ButtonUploadModule.IsEnabled = false;
            }
            else if (selectedModule.SyncStatus == AutomationModule.Constants.SyncStatus.LocalOnly)
            {
                ButtonDeleteModule.IsEnabled = false;
                ButtonUploadModule.IsEnabled = true;
            }
            else
            {
                setAllModuleButtonStatesTo(true);
            }

            if (Properties.Settings.Default.StorageAccount == "na")
            {
                ButtonUploadModule.IsEnabled = false;
            }

        }

        private void RunbooksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetButtonStatesForSelectedRunbook();
            AutomationRunbook runbook = (AutomationRunbook)RunbooksListView.SelectedItem;

            if ((RunbooksListView.SelectedItem != null && runbook != null) && runbook.localFileInfo != null)
            {
                if (HostObject != null)
                {
                    var currentFile = HostObject.CurrentPowerShellTab.Files.Where(x => x.FullPath == runbook.localFileInfo.FullName);
                    if (currentFile.Count() > 0)
                    {
                        HostObject.CurrentPowerShellTab.Files.SetSelectedFile(currentFile.FirstOrDefault());
                    }
                }
            }
        }

        private void DSCListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AutomationDSC dsc = (AutomationDSC)DSCListView.SelectedItem;

            if ((DSCListView.SelectedItem != null) && (dsc != null) && (dsc.localFileInfo != null))
            {
                if (HostObject != null)
                {
                    var currentFile = HostObject.CurrentPowerShellTab.Files.Where(x => x.FullPath == dsc.localFileInfo.FullName);
                    if (currentFile.Count() > 0)
                    {
                        HostObject.CurrentPowerShellTab.Files.SetSelectedFile(currentFile.FirstOrDefault());
                    }
                }
            }
            SetButtonStatesForSelectedConfiguration();
        }

        private void ModuleListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AutomationModule module = (AutomationModule)ModuleListView.SelectedItem;

            if ((ModuleListView.SelectedItem != null) && (module != null))
            {
                SetButtonStatesForSelectedModule();
            }
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
                        System.Windows.Forms.MessageBox.Show("There is no local copy of the selected runbook to open. Please download the runbook.",
                                "No Local Runbook", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                        continue;
                    }
                    if (HostObject != null)
                    {
                        var currentFile = HostObject.CurrentPowerShellTab.Files.Where(x => x.FullPath == selectedRunbook.localFileInfo.FullName);
                        if (currentFile.Count() > 0)
                        {
                            try
                            {
                                HostObject.CurrentPowerShellTab.Files.SetSelectedFile(currentFile.FirstOrDefault());
                            }
                            catch
                            {
                                System.Windows.Forms.MessageBox.Show("Could not select " + selectedRunbook.localFileInfo.Name + " in the ISE.",
                                    "Open Runbook", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                            }
                        }
                        else
                            HostObject.CurrentPowerShellTab.Files.Add(selectedRunbook.localFileInfo.FullName);
                    }
                    if (HostObject == null)
                    {
                        if (IDEComboBox.HasItems)
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo();
                            startInfo.CreateNoWindow = false;
                            startInfo.UseShellExecute = false;
                            if ((string)IDEComboBox.SelectedValue == "Visual Studio")
                            {
                                startInfo.FileName = @VSStudio;
                                startInfo.Arguments = "/Edit " + '"' + selectedRunbook.localFileInfo.FullName + '"';

                            }
                            if ((string)IDEComboBox.SelectedValue == "VS Code")
                            {
                                startInfo.FileName = @VSCode;
                                startInfo.Arguments = '"' + selectedRunbook.localFileInfo.FullName + '"';
                            }
                            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                            using (Process exeProcess = Process.Start(startInfo))
                            {
                                exeProcess.Start();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("The runbook could not be opened.\r\nError details: " + ex.Message, "Error");
            }
        }

        private string GetVisualStudioPath()
        {
            using (Microsoft.Win32.RegistryKey localMachineKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\VisualStudio\Sxs\VS7"))
            {
                if (localMachineKey != null)
                {
                    var keys = localMachineKey.GetValueNames();
                    if (keys != null)
                    {
                        Array.Sort(keys);
                        Array.Reverse(keys);
                        var devEnv = localMachineKey.GetValue(keys[0]);
                        return (devEnv + "Common7\\IDE\\devenv.exe");
                    }
                }
                return null;
            }
        }

        private string GetVSCodePath()
        {
            using (Microsoft.Win32.RegistryKey VSCodeKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\Applications\Code.exe\shell\open\command"))
            {
                if (VSCodeKey != null)
                {
                    var codeValue = VSCodeKey.GetValue("").ToString();
                    return (codeValue.Substring(0, codeValue.IndexOf('%') - 1).Replace("\"", string.Empty).Trim());
                }
                using (Microsoft.Win32.RegistryKey VSCodeCurrentUserKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Classes\Applications\Code.exe\shell\open\command"))
                {
                    if (VSCodeCurrentUserKey != null)
                    {
                        var codeValue = VSCodeCurrentUserKey.GetValue("").ToString();
                        return (codeValue.Substring(0, codeValue.IndexOf('%') - 1).Replace("\"", string.Empty).Trim());
                    }
                }
                return null;
            }
        }

        private void ButtonOpenConfiguration_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (Object obj in DSCListView.SelectedItems)
                {
                    AutomationDSC selectedConfiguration = (AutomationDSC)obj;
                    if (selectedConfiguration.SyncStatus == AutomationDSC.Constants.SyncStatus.CloudOnly)
                    {
                        System.Windows.Forms.MessageBox.Show("There is no local copy of the selected configuration to open. Please download the configuration.",
                                "No Local configuration", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                        continue;
                    }
                    if (HostObject != null)
                    {
                        var currentFile = HostObject.CurrentPowerShellTab.Files.Where(x => x.FullPath == selectedConfiguration.localFileInfo.FullName);
                        if (currentFile.Count() > 0)
                        {
                            try
                            {
                                // If the file is opened but not saved, an exception will be thrown here
                                HostObject.CurrentPowerShellTab.Files.Remove(currentFile.First());
                            }
                            catch
                            {
                                System.Windows.Forms.MessageBox.Show("There are unsaved changes to " + selectedConfiguration.localFileInfo.Name + ", so it cannot be re-opened.",
                                    "Unsaved configuration Changes", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                            }
                        }
                        HostObject.CurrentPowerShellTab.Files.Add(selectedConfiguration.localFileInfo.FullName);
                    }
                    if (HostObject == null)
                    {
                        if (IDEComboBox.HasItems)
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo();
                            startInfo.CreateNoWindow = false;
                            startInfo.UseShellExecute = false;
                            if ((string)IDEComboBox.SelectedValue == "Visual Studio")
                            {
                                startInfo.FileName = @VSStudio;
                                startInfo.Arguments = "/Edit " + '"' + selectedConfiguration.localFileInfo.FullName + '"';

                            }
                            if ((string)IDEComboBox.SelectedValue == "VS Code")
                            {
                                startInfo.FileName = @VSCode;
                                startInfo.Arguments = '"' + selectedConfiguration.localFileInfo.FullName + '"';
                            }
                            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                            using (Process exeProcess = Process.Start(startInfo))
                            {
                                exeProcess.Start();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("The configuration could not be opened.\r\nError details: " + ex.Message, "Error");
            }
        }

        private void ButtonOpenModule_Click(object sender, RoutedEventArgs e)
        {

            if (ModuleListView.SelectedItem != null)
            {
                var module = (AutomationModule)ModuleListView.SelectedItem;
                var message = "Module name: " + module.Name + "\nLocal version: " + module.localVersion + "\nCloud version: " + module.cloudVersion;
                message = message + "\nLocal path: " + module.localModulePath;
                System.Windows.Forms.MessageBox.Show(message, "Information", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                ButtonUploadModule.IsEnabled = true;
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
                        selectedRunbook.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.InSync;
                        selectedRunbook.AuthoringState = AutomationRunbook.AuthoringStates.Published;
                        endBackgroundWork("Published runbook " + selectedRunbook.Name);
                    }
                    catch (Exception ex)
                    {
                        endBackgroundWork("Error publishing runbook " + selectedRunbook.Name);
                        System.Windows.Forms.MessageBox.Show("The runbook could not be published.\r\nDetails: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                }

                if (count == 1) endBackgroundWork("Published " + name);
                else if (count > 1) endBackgroundWork("Published " + count + " runbooks.");
                else endBackgroundWork();
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error publishing runbooks.");
                System.Windows.Forms.MessageBox.Show("Error publishing runbooks.\r\nDetails: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                /* Update UI */
                ButtonPublishRunbook.Content = "Publish Draft";
                SetButtonStatesForSelectedRunbook();
            }
        }

        private void addLocalRunbookToView(String localFile)
        {

            AutomationRunbook runbook = new AutomationRunbook(new System.IO.FileInfo(localFile));
            runbookListViewModel.Add(runbook);
        }

        private void removeLocalRunbookToView(String localFile)
        {

            AutomationRunbook runbook = new AutomationRunbook(new System.IO.FileInfo(localFile));
            runbookListViewModel.Remove(runbook);
        }

        private void addLocalConfigurationToView(String localFile)
        {

            AutomationDSC configuration = new AutomationDSC(new System.IO.FileInfo(localFile));
            DSCListViewModel.Add(configuration);
        }

        private void removeLocalConfigurationToView(String localFile)
        {

            AutomationDSC configuration = new AutomationDSC(new System.IO.FileInfo(localFile));
            DSCListViewModel.Remove(configuration);
        }

        private async Task refreshRunbooks()
        {
            var localScripts = await getLocalScripts();
            ISet<AutomationRunbook> runbooks = await AutomationRunbookManager.GetAllRunbookMetadata(iseClient.automationManagementClient,
                                    iseClient.currWorkspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name,localScripts);
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

        private async Task refreshLocalScripts(string localFile = null)
        {
            System.Management.Automation.Language.Token[] AST;
            System.Management.Automation.Language.ParseError[] ASTError = null;
            Dictionary<string, string> copyScripts = new Dictionary<string, string>();

            try {
                if (Directory.Exists(iseClient.currWorkspace))
                {
                    string[] localScripts = null;
                    if (localFile == null)
                    {
                        localScripts = Directory.GetFiles(iseClient.currWorkspace, "*.*")
                                      .Where(s => s.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
                                                  s.EndsWith(".py", StringComparison.OrdinalIgnoreCase)).ToArray();
                    }
                    else
                    {
                        if ((localFile.EndsWith(".ps1")) || (localFile.EndsWith(".py")))
                        {
                            localScripts = new string[1];
                            localScripts[0] = localFile;
                        }
                    }

                    foreach (string path in localScripts)
                    {
                        if (File.Exists(path))
                        {
                            String PSScriptText = File.ReadAllText(path);

                            var ASTScript = System.Management.Automation.Language.Parser.ParseInput(PSScriptText, out AST, out ASTError);
                            if (ASTScript.EndBlock != null)
                            {
                                if ((ASTScript.EndBlock.Extent.Text.ToLower().StartsWith("configuration")))
                                {
                                    copyScripts.Add(path, "configuration");
                                }
                                else
                                {
                                    copyScripts.Add(path, "script");
                                }
                            }
                            else copyScripts.Add(path, "script");
                            ASTScript = null;
                        }
                    }
                    // Lock access to localScriptsParsed dictionary so it is not overwritten when accessed by other threads.
                    lock (refreshScriptsLock)
                    {
                        if (localFile != null && localScriptsParsed != null)
                        {
                            // If the file has been deleted, remove it from the dictionary
                            if (!(File.Exists(localFile)))
                            {
                                localScriptsParsed.Remove(localFile);
                            }
                            else
                            {
                                // If the file already exists, skip, else add the new file to the dictionary
                                if (localScriptsParsed.ContainsKey(copyScripts.Keys.FirstOrDefault()) == false)
                                {
                                    localScriptsParsed.Add(copyScripts.Keys.FirstOrDefault(), copyScripts.Values.FirstOrDefault());
                                }
                            }
                        }
                        else localScriptsParsed = new Dictionary<string, string>(copyScripts);
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't throw exception for file in use error as this will get picked up on the next sync and is not destructive.
                if (ex.HResult != -2147024864)
                {
                    System.Windows.Forms.MessageBox.Show("Error reading local files.\r\nDetails: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
                }
        }


        private async Task refreshLocalModules(string localFile = null)
        {
            System.Management.Automation.Language.Token[] AST;
            System.Management.Automation.Language.ParseError[] ASTError = null;
            Dictionary<string, PSObject> copyModules = new Dictionary<string, PSObject>();

            try
            {
                if (Directory.Exists(iseClient.currWorkspace))
                {
                    // Get all available modules in the system
                    List<string> localModules = new List<string>();
                    using (Runspace runSpace = RunspaceFactory.CreateRunspace())
                    {

                        runSpace.Open();
                        using (Pipeline pipeline = runSpace.CreatePipeline())
                        {
                            Command getModuleCommand = new Command("Get-Module");
                            getModuleCommand.Parameters.Add("-ListAvailable");
                            pipeline.Commands.Add(getModuleCommand);
                            Collection<PSObject> output = pipeline.Invoke();
                            foreach (PSObject result in output)
                            {
                                if (result != null)
                                {
                                    var moduleName = result.Properties["Name"].Value.ToString();
                                    if (!(copyModules.ContainsKey(moduleName))) copyModules.Add(moduleName, result);
                                }
                            }
                        }
                        runSpace.Close();
                    }
                    
                    // Lock access to localModulesParsed dictionary so it is not overwritten when accessed by other threads.
                    lock (refreshModulesLock)
                    {
                        if (localFile != null && localModulesParsed != null)
                        {
                            // If the file has been deleted, remove it from the dictionary
                            if (!(Directory.Exists(localFile)))
                            {
                                localModulesParsed.Remove(localFile);
                            }
                            else
                            {
                                // If the file already exists, skip, else add the new file to the dictionary
                                if (localModulesParsed.ContainsKey(copyModules.Keys.FirstOrDefault()) == false)
                                {
                                    localModulesParsed.Add(copyModules.Keys.FirstOrDefault(), copyModules.Values.FirstOrDefault());
                                }
                            }
                        }
                        else localModulesParsed = new Dictionary<string, PSObject>(copyModules);
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore the case where the refresh is happening when the user is saving the file ("The process cannot access the file")
                if (ex.HResult.ToString() != "-2147024864")
                    MessageBox.Show("Error reading local files.\r\nDetails: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<Dictionary<string, string>> getLocalScripts()
        {
            Dictionary<string, string> copyOfScripts = null;
            // Lock access to localScriptsParsed dictionary so it is not overwritten when accessed by other threads.
            lock (refreshScriptsLock)
            {
                if (localScriptsParsed != null) copyOfScripts = new Dictionary<string, string>(localScriptsParsed);
            }
            return copyOfScripts;

        }

        private async Task<Dictionary<string, PSObject>> getLocalModules()
        {
            Dictionary<string, PSObject> copyOfModules = null;
            // Lock access to localScriptsParsed dictionary so it is not overwritten when accessed by other threads.
            lock (refreshModulesLock)
            {
                if (localModulesParsed != null) copyOfModules = new Dictionary<string, PSObject>(localModulesParsed);
            }
            return copyOfModules;

        }

        private async Task refreshConfigurations()
        {
            var localScripts = await getLocalScripts();
            ISet<AutomationDSC> configurations = await AutomationDSCManager.GetAllConfigurationMetadata(iseClient.automationManagementClient,
                                    iseClient.currWorkspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, localScripts);
            IDictionary<String, AutomationDSC> configurationWithName = new Dictionary<String, AutomationDSC>(configurations.Count);
            foreach (AutomationDSC configuration in configurations)
            {
                configurationWithName.Add(configuration.Name, configuration);
            }
            ISet<AutomationDSC> configurationsToDelete = new SortedSet<AutomationDSC>();
            foreach (AutomationDSC curr in DSCListViewModel)
            {
                if (!configurationWithName.ContainsKey(curr.Name))
                {
                    configurationsToDelete.Add(curr);
                    continue;
                }
                curr.AuthoringState = configurationWithName[curr.Name].AuthoringState;
                curr.Parameters = configurationWithName[curr.Name].Parameters;
                curr.Description = configurationWithName[curr.Name].Description;
                curr.LastModifiedCloud = configurationWithName[curr.Name].LastModifiedCloud;
                curr.LastModifiedLocal = configurationWithName[curr.Name].LastModifiedLocal;
                curr.UpdateSyncStatus();
                configurationWithName.Remove(curr.Name);
            }
            foreach (AutomationDSC configuration in configurationsToDelete)
            {
                DSCListViewModel.Remove(configuration);
            }
            foreach (String name in configurationWithName.Keys)
            {
                DSCListViewModel.Add(configurationWithName[name]);
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
                            selectedRunbook.LastModifiedCloud = selectedRunbook.LastModifiedLocal;
                            selectedRunbook.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.InSync;
                            if (selectedRunbook.AuthoringState == AutomationRunbook.AuthoringStates.Published)
                            {
                                selectedRunbook.AuthoringState = AutomationRunbook.AuthoringStates.InEdit;
                            }
                            selectedRunbook.LastModifiedCloud = selectedRunbook.LastModifiedLocal;
                            selectedRunbook.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.InSync;
                            endBackgroundWork("Uploaded " + selectedRunbook.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        endBackgroundWork("Error uploading runbook " + selectedRunbook.Name);
                        System.Windows.Forms.MessageBox.Show("The runbook " + selectedRunbook.Name + " could not be uploaded.\r\nError details: " + ex.Message);
                    }
                }

                if (count == 1) endBackgroundWork("Uploaded " + name);
                else if (count > 1) endBackgroundWork("Uploaded " + count + " runbooks.");
                else endBackgroundWork();
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error uploading runbooks.");
                System.Windows.Forms.MessageBox.Show(ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                SetButtonStatesForSelectedRunbook();
            }
        }

        private async void ButtonUploadConfiguration_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonUploadConfiguration.IsEnabled = false;
                beginBackgroundWork();
                int count = 0;
                string name = "";
                foreach (Object obj in DSCListView.SelectedItems)
                {
                    AutomationDSC selectedConfiguration = (AutomationDSC)obj;
                    if (selectedConfiguration.SyncStatus == AutomationDSC.Constants.SyncStatus.CloudOnly)
                        continue;
                    try
                    {
                        // If the file is unsaved in the ISE, show warning to user before uploading
                        if (checkIfDSCFileIsSaved(selectedConfiguration))
                        {
                            beginBackgroundWork("Uploading configuration " + selectedConfiguration.Name + "...");
                            await AutomationDSCManager.UploadConfigurationAsDraft(selectedConfiguration, iseClient.automationManagementClient,
                                        iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount);
                            count++;
                            name = selectedConfiguration.Name;
                            selectedConfiguration.LastModifiedCloud = selectedConfiguration.LastModifiedLocal;
                            selectedConfiguration.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.InSync;
                            endBackgroundWork("Uploaded " + selectedConfiguration.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        endBackgroundWork("Error uploading configuration " + selectedConfiguration.Name);
                        System.Windows.Forms.MessageBox.Show("The configuration " + selectedConfiguration.Name + " could not be uploaded.\r\nError details: " + ex.Message);
                    }
                }

                if (count == 1) endBackgroundWork("Uploaded " + name);
                else if (count > 1) endBackgroundWork("Uploaded " + count + " configurations.");
                else endBackgroundWork();
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error uploading configurations.");
                System.Windows.Forms.MessageBox.Show(ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                SetButtonStatesForSelectedConfiguration();
            }
        }

        private async void ButtonTestRunbook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RunbooksListView.SelectedItems.Count > 1)
                {
                    string message = "Batch creation of test jobs is suppressed for performance reasons.";
                    message += "\r\nPlease create test jobs one at a time.";
                    System.Windows.Forms.MessageBox.Show(message, "Test Job Warning", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                ButtonTestRunbook.IsEnabled = false;
                AutomationRunbook selectedRunbook = (AutomationRunbook)RunbooksListView.SelectedItem;

                if (selectedRunbook.LastModifiedLocal > selectedRunbook.LastModifiedCloud)
                {
                    var dialog = System.Windows.Forms.MessageBox.Show("Local copy is newer than cloud version. Continue testing cloud version?",
                            "Local Runbook is newer", System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Warning);
                    if (dialog == System.Windows.Forms.DialogResult.Cancel) return;
                }

                if (selectedRunbook.AuthoringState == AutomationRunbook.AuthoringStates.Published)
                {
                    beginBackgroundWork();
                    await AutomationRunbookManager.CheckOutRunbook(selectedRunbook, iseClient.automationManagementClient,
                        iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount);
                }
                JobOutputWindow jobWindow = new JobOutputWindow(selectedRunbook, iseClient,Properties.Settings.Default.jobRefreshTimeInMilliseconds);
                jobWindow.Show();
            }
            catch (Exception exception)
            {
                System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                SetButtonStatesForSelectedRunbook();
                endBackgroundWork();
            }
        }

        private async void ButtonCompileConfiguration_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DSCListView.SelectedItems.Count > 1)
                {
                    string message = "Batch compilation of test jobs is suppressed for performance reasons.";
                    message += "\r\nPlease compile configurations one at a time.";
                    System.Windows.Forms.MessageBox.Show(message, "Compile Configuration Warning", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }

                ButtonCompileConfiguration.IsEnabled = false;
                AutomationDSC selectedConfiguration = (AutomationDSC)DSCListView.SelectedItem;
                if (selectedConfiguration.LastModifiedLocal > selectedConfiguration.LastModifiedCloud)
                {
                    var dialog = System.Windows.Forms.MessageBox.Show("Local copy is newer than cloud version. Continue compiling cloud version?",
                            "Local Configuration is newer", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning);
                    if (dialog == System.Windows.Forms.DialogResult.No) return;
                }

                DSCCompilationJobOutputWindow jobWindow = new DSCCompilationJobOutputWindow(selectedConfiguration.Name, iseClient, Properties.Settings.Default.jobRefreshTimeInMilliseconds);
                jobWindow.Show();
            }
            catch (Exception exception)
            {
                System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                SetButtonStatesForSelectedConfiguration();
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
            if (HostObject != null)
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
            }
            return true;
        }

        /// <summary>
        /// Checks if a file is unsaved in the ISE and shows a dialog to ask user to confirm if they want to continue
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns>false if the user clicks cancel or else returns true to continue with upload of unsaved file</returns>
        private Boolean checkIfDSCFileIsSaved(AutomationDSC configuration)
        {
            if (HostObject != null)
            {
                var currentFile = HostObject.CurrentPowerShellTab.Files.Where(x => x.FullPath == configuration.localFileInfo.FullName);
                if (currentFile.Count() != 0)
                {
                    if (currentFile.First().IsSaved == false)
                    {
                        String message = "The file " + configuration.localFileInfo.Name + " has unsaved changes.";
                        message += "\r\nPlease save your changes before uploading.";
                        System.Windows.Forms.DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(message, "Upload Warning",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                        return false;
                    }
                }
            }
            return true;
        }

        private async Task createOrUpdateCredentialAsset(string credentialAssetName, AutomationCredential credToEdit, bool newAsset = false)
        {
            if (newAsset)
            {
                var asset = await AutomationAssetManager.GetAsset(credentialAssetName, Constants.AssetType.Credential, iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint(), connectionTypes);
                if (asset != null) throw new Exception("Credential with that name already exists");
            }

            var dialog = new NewOrEditCredentialDialog(credToEdit);

            if (dialog.ShowDialog() == true)
            {
                var assetsToSave = new List<AutomationAsset>();

                var newCred = new AutomationCredential(credentialAssetName, dialog.username, dialog.password);
                assetsToSave.Add(newCred);

                try
                {
                    AutomationAssetManager.SaveLocally(iseClient.currWorkspace, assetsToSave, getEncryptionCertificateThumbprint(), connectionTypes);
                    await refreshAssets();
                }
                catch (Exception exception)
                {
                    System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
        }

        private async Task createOrUpdateVariableAsset(string variableAssetName, AutomationVariable variableToEdit, bool newAsset = false)
        {
            if (newAsset)
            {
                // Check if variable already exists before creating one.
                var asset = await AutomationAssetManager.GetAsset(variableAssetName, Constants.AssetType.Variable, iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint(), connectionTypes);
                if (asset != null) throw new Exception("Variable with that name already exists");
            }

            var dialog = new NewOrEditVariableDialog(variableToEdit);

            if (dialog.ShowDialog() == true)
            {
                var assetsToSave = new List<AutomationAsset>();

                var newVariable = new AutomationVariable(variableAssetName, dialog.value, dialog.encrypted);
                assetsToSave.Add(newVariable);

                try
                {
                    AutomationAssetManager.SaveLocally(iseClient.currWorkspace, assetsToSave, getEncryptionCertificateThumbprint(), connectionTypes);
                    await refreshAssets();
                }
                catch (Exception exception)
                {
                    System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
        }

        private async Task createOrUpdateConnectionAsset(string connectionAssetName, AutomationConnection connectionToEdit, bool newAsset = false)
        {
            if (newAsset)
            {
                // Check if connection already exists before creating one.
                var asset = await AutomationAssetManager.GetAsset(connectionAssetName, Constants.AssetType.Connection, iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint(), connectionTypes);
                if (asset != null) throw new Exception("Connection with that name already exists");
            }

            var dialog = new NewOrEditConnectionDialog(connectionToEdit, connectionTypes);

            if (dialog.ShowDialog() == true)
            {
                var assetsToSave = new List<AutomationAsset>();

                var newConnection = new AutomationConnection(connectionAssetName, dialog.connectionFields, dialog.connectionType);
                assetsToSave.Add(newConnection);

                try
                {
                    AutomationAssetManager.SaveLocally(iseClient.currWorkspace, assetsToSave, getEncryptionCertificateThumbprint(), connectionTypes);
                    await refreshAssets();
                }
                catch (Exception exception)
                {
                    System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
        }


        private async Task createOrUpdateCertificateAsset(string certificateAssetName, AutomationCertificate certToEdit, bool newAsset = false)
        {
            if (newAsset)
            {
                var asset = await AutomationAssetManager.GetAsset(certificateAssetName, Constants.AssetType.Certificate, iseClient.currWorkspace, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, getEncryptionCertificateThumbprint(), connectionTypes);
                if (asset != null) throw new Exception("Certificate with that name already exists");
            }

            var dialog = new NewOrEditCertificateDialog(certToEdit);

            if (dialog.ShowDialog() == true)
            {
                var assetsToSave = new List<AutomationAsset>();

                var newCert = new AutomationCertificate(certificateAssetName, dialog.thumbprint, dialog.certPath, dialog.password, dialog.exportable, dialog.encrypted);
                assetsToSave.Add(newCert);

                try
                {
                    AutomationAssetManager.SaveLocally(iseClient.currWorkspace, assetsToSave, getEncryptionCertificateThumbprint(), connectionTypes);
                    await refreshAssets();
                }
                catch (Exception exception)
                {
                    System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
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
                        assetsComboBox.SelectedItem = assetsComboBox.Items[2];
                    }
                    else if (dialog.newAssetType == AutomationISE.Model.Constants.assetCredential)
                    {
                        await createOrUpdateCredentialAsset(dialog.newAssetName, null,true);
                        assetsComboBox.SelectedItem = assetsComboBox.Items[1];
                    }
                    else if (dialog.newAssetType == AutomationISE.Model.Constants.assetConnection)
                    {
                        await createOrUpdateConnectionAsset(dialog.newAssetName, null,true);
                        assetsComboBox.SelectedItem = assetsComboBox.Items[0];
                    }
                    else if (dialog.newAssetType == AutomationISE.Model.Constants.assetCertificate)
                    {
                        await createOrUpdateCertificateAsset(dialog.newAssetName, null, true);
                        assetsComboBox.SelectedItem = assetsComboBox.Items[3];
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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
                else if (asset is AutomationCertificate)
                {
                    await createOrUpdateCertificateAsset(asset.Name, (AutomationCertificate)asset);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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
                System.Windows.Forms.MessageBox.Show("The thumbprint could not be updated:\r\n" + ex.Message + ".", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private async void ButtonSourceControlRunbook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                JobCreateResponse sourceControlJob = await AutomationSourceControl.startSourceControlJob(iseClient.automationManagementClient,
                            iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);

                JobOutputWindow jobWindow = new JobOutputWindow(sourceControlJob.Job.Properties.Runbook.Name, sourceControlJob, iseClient, Properties.Settings.Default.jobRefreshTimeInMilliseconds);
                jobWindow.Show();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("The source control job could not be started. " + ex.Message, "Error");
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
                System.Windows.Forms.MessageBox.Show(exception.Message, "Couldn't open path", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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

        private void DSCListColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = (GridViewColumnHeader)sender;
            string sortProperty = column.Tag.ToString();
            DSCListView.Items.SortDescriptions.Clear();
            if (sortProperty != configurationCurrSortProperty || configurationCurrSortDir == ListSortDirection.Descending)
                configurationCurrSortDir = ListSortDirection.Ascending;
            else
                configurationCurrSortDir = ListSortDirection.Descending;
            configurationCurrSortProperty = sortProperty;
            SortDescription newDescription = new SortDescription(configurationCurrSortProperty, configurationCurrSortDir);
            DSCListView.Items.SortDescriptions.Add(newDescription);
            if (configurationCurrSortProperty != "Name")
                DSCListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        }

        private void ModuleListColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = (GridViewColumnHeader)sender;
            string sortProperty = column.Tag.ToString();
            ModuleListView.Items.SortDescriptions.Clear();
            if (sortProperty != moduleCurrSortProperty || moduleCurrSortDir == ListSortDirection.Descending)
                moduleCurrSortDir = ListSortDirection.Ascending;
            else
                moduleCurrSortDir = ListSortDirection.Descending;
            moduleCurrSortProperty = sortProperty;
            SortDescription newDescription = new SortDescription(moduleCurrSortProperty, moduleCurrSortDir);
            ModuleListView.Items.SortDescriptions.Add(newDescription);
            if (moduleCurrSortProperty != "Name")
                ModuleListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
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
                System.Windows.Forms.MessageBox.Show(exception.Message, "Could not launch portal", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private void togglePromptButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (HostObject != null)
                {
                    if (promptShortened)
                    {
                        HostObject.CurrentPowerShellTab.Invoke("cd \"" + iseClient.currWorkspace + "\"" + ";function prompt {'PS ' + $(Get-Location) + '> '}");
                        promptShortened = false;
                    }
                    else
                    {
                        //TODO: factor this into the iseClient
                        string pathHint = Path.GetPathRoot(iseClient.currWorkspace) + "..." + Path.DirectorySeparatorChar + Path.GetFileName(iseClient.currWorkspace);
                        HostObject.CurrentPowerShellTab.Invoke("cd \"" + iseClient.currWorkspace + "\"" + ";function prompt {'PS " + pathHint + "> '}");
                        promptShortened = true;
                    }
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
                CreateRunbookDialog createOptionsWindow;
                if (HostObject != null)
                {
                     createOptionsWindow = new CreateRunbookDialog(false);
                }
                else
                {
                    createOptionsWindow = new CreateRunbookDialog(true);
                }
                bool? result = createOptionsWindow.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    fileWatcher.EnableRaisingEvents = false;
                    AutomationRunbookManager.CreateLocalRunbook(createOptionsWindow.runbookName, iseClient.currWorkspace, createOptionsWindow.runbookType);
                    String runbookFileName = null;
                    if (createOptionsWindow.runbookType == Constants.RunbookType.Python2)
                    {
                        runbookFileName = createOptionsWindow.runbookName + ".py";
                    }
                    else
                    {
                        runbookFileName = createOptionsWindow.runbookName + ".ps1";
                    }
                    if (HostObject != null)
                    {
                        HostObject.CurrentPowerShellTab.Files.Add(System.IO.Path.Combine(iseClient.currWorkspace, runbookFileName));
                    }
                    if (HostObject == null)
                    {
                        if (IDEComboBox.HasItems)
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo();
                            startInfo.CreateNoWindow = false;
                            startInfo.UseShellExecute = false;
                            if ((string)IDEComboBox.SelectedValue == "Visual Studio")
                            {
                                startInfo.FileName = @VSStudio;
                                startInfo.Arguments = "/Edit " + '"' + System.IO.Path.Combine(iseClient.currWorkspace, runbookFileName) + '"';

                            }
                            if ((string)IDEComboBox.SelectedValue == "VS Code")
                            {
                                startInfo.FileName = @VSCode;
                                startInfo.Arguments = '"' + System.IO.Path.Combine(iseClient.currWorkspace, runbookFileName) + '"';
                            }
                            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                            using (Process exeProcess = Process.Start(startInfo))
                            {
                                exeProcess.Start();
                            }
                        }
                    }
                    fileWatcher.EnableRaisingEvents = true;
                    await refreshLocalScripts(System.IO.Path.Combine(iseClient.currWorkspace, runbookFileName));
                    addLocalRunbookToView(System.IO.Path.Combine(iseClient.currWorkspace, runbookFileName));
                    /* Select new runbook from list*/
                    foreach (AutomationRunbook runbook in runbookListViewModel)
                    {
                        if (runbook.Name.Equals(createOptionsWindow.runbookName))
                        {
                            RunbooksListView.SelectedItem = runbook;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Could not create the new runbook.\r\nError details: " + ex.Message);
            } 
            finally
            {
                fileWatcher.EnableRaisingEvents = true;
                endBackgroundWork();
                SetButtonStatesForSelectedRunbook();
            }
        }

        private async void ButtonCreateConfiguration_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonCreateConfiguration.IsEnabled = false;
                beginBackgroundWork("Creating new configuration...");
                CreateConfigurationDialog createOptionsWindow = new CreateConfigurationDialog();
                bool? result = createOptionsWindow.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    fileWatcher.EnableRaisingEvents = false;
                    if (createOptionsWindow.configurationName.Contains(Constants.nodeConfigurationIdentifier))
                    {
                        AutomationDSCManager.CreateLocalConfigurationData(createOptionsWindow.configurationName, iseClient.currWorkspace);
                        if (HostObject != null)
                        {
                            HostObject.CurrentPowerShellTab.Files.Add(System.IO.Path.Combine(iseClient.currWorkspace, createOptionsWindow.configurationName + ".ps1"));
                        }
                        addLocalRunbookToView(System.IO.Path.Combine(iseClient.currWorkspace, createOptionsWindow.configurationName + ".ps1"));
                        this.runbookTab.Focus();
                    }
                    else
                    {
                        AutomationDSCManager.CreateLocalConfiguration(createOptionsWindow.configurationName, iseClient.currWorkspace);
                        if (HostObject != null)
                        {
                            HostObject.CurrentPowerShellTab.Files.Add(System.IO.Path.Combine(iseClient.currWorkspace, createOptionsWindow.configurationName + ".ps1"));
                        }
                        addLocalConfigurationToView(System.IO.Path.Combine(iseClient.currWorkspace, createOptionsWindow.configurationName + ".ps1"));
                    }
                    fileWatcher.EnableRaisingEvents = true;
                    await refreshLocalScripts(System.IO.Path.Combine(iseClient.currWorkspace, createOptionsWindow.configurationName + ".ps1"));
                    /* Select new configuration from list*/
                    foreach (AutomationDSC configuraiton in DSCListViewModel)
                    {
                        if (configuraiton.Name.Equals(createOptionsWindow.configurationName))
                        {
                            DSCListView.SelectedItem = configuraiton;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Could not create the new configuraiton.\r\nError details: " + ex.Message);
            }
            finally
            {
                fileWatcher.EnableRaisingEvents = true;
                endBackgroundWork();
                SetButtonStatesForSelectedConfiguration();
            }
        }

        private async void ButtonDeleteRunbook_Click(object sender, RoutedEventArgs e)
        {
            var runbookList = new List<AutomationRunbook>();
            foreach (Object obj in RunbooksListView.SelectedItems)
            {
                runbookList.Add((AutomationRunbook)obj);
            }
            try
            {
                beginBackgroundWork("Deleting selected runbooks...");
                foreach (AutomationRunbook runbook in runbookList)
                {
                    if (runbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.CloudOnly)
                    {
                        String message = "Are you sure you wish to delete the cloud copy of " + runbook.Name + "?  ";
                        message += "There is no local copy.";
                        System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show(message, "Confirm Runbook Deletion", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning);
                        if (result == System.Windows.Forms.DialogResult.Yes)
                        {
                            await AutomationRunbookManager.DeleteCloudRunbook(runbook, iseClient.automationManagementClient,
                                iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
                        }
                    }
                    else if (runbook.SyncStatus == AutomationRunbook.Constants.SyncStatus.LocalOnly)
                    {
                        String message = "Are you sure you wish to delete the local copy of " + runbook.Name + "?  ";
                        message += "There is no cloud copy.";
                        System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show(message, "Confirm Runbook Deletion", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning);
                        if (result == System.Windows.Forms.DialogResult.Yes)
                        {
                            if (runbook.localFileInfo != null && File.Exists(runbook.localFileInfo.FullName))
                            {
                                AutomationRunbookManager.DeleteLocalRunbook(runbook);
                                removeLocalRunbookToView(runbook.localFileInfo.FullName);
                                await refreshLocalScripts(runbook.localFileInfo.FullName);
                            }
                        }
                    }
                    else
                    {
                        DeleteRunbookDialog deleteOptionsWindow = new DeleteRunbookDialog(runbook.Name);
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
                                {
                                    AutomationRunbookManager.DeleteLocalRunbook(runbook);
                                    removeLocalRunbookToView(runbook.localFileInfo.FullName);
                                    await refreshLocalScripts(runbook.localFileInfo.FullName);
                                }
                            }
                        }
                    }
                }
                await refreshRunbooks();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Could not delete the selected runbook(s).\r\nError details: " + ex.Message);
            }
            finally
            {
                endBackgroundWork();
            }
        }

        private async void ButtonDeleteConfiguration_Click(object sender, RoutedEventArgs e)
        {
            var DSCList = new List<AutomationDSC>();
            foreach (Object obj in DSCListView.SelectedItems)
            {
                DSCList.Add((AutomationDSC)obj);
            }

            try
            {
                beginBackgroundWork("Deleting selected configurations...");
                foreach (AutomationDSC configuration in DSCList)
                {
                    if (configuration.SyncStatus == AutomationDSC.Constants.SyncStatus.CloudOnly)
                    {
                        String message = "Are you sure you wish to delete the cloud copy of " + configuration.Name + "?  ";
                        message += "There is no local copy.";
                        System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show(message, "Confirm configuration Deletion", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning);
                        if (result == System.Windows.Forms.DialogResult.Yes)
                        {
                            await AutomationDSCManager.DeleteCloudConfiguration(configuration, iseClient.automationManagementClient,
                                iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
                        }
                    }
                    else if (configuration.SyncStatus == AutomationDSC.Constants.SyncStatus.LocalOnly)
                    {
                        String message = "Are you sure you wish to delete the local copy of " + configuration.Name + "?  ";
                        message += "There is no cloud copy.";
                        System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show(message, "Confirm Configuration Deletion", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning);
                        if (result == System.Windows.Forms.DialogResult.Yes)
                        {
                            if (configuration.localFileInfo != null && File.Exists(configuration.localFileInfo.FullName))
                            {
                                AutomationDSCManager.DeleteLocalConfiguration(configuration);
                                removeLocalConfigurationToView(configuration.localFileInfo.FullName);
                                await refreshLocalScripts(configuration.localFileInfo.FullName);
                            }
                        }
                    }
                    else
                    {
                        DeleteConfigurationDialog deleteOptionsWindow = new DeleteConfigurationDialog(configuration.Name);
                        bool? result = deleteOptionsWindow.ShowDialog();
                        if (result.HasValue && result.Value)
                        {
                            if (deleteOptionsWindow.deleteLocalOnly)
                            {
                                if (configuration.localFileInfo != null && File.Exists(configuration.localFileInfo.FullName))
                                    AutomationDSCManager.DeleteLocalConfiguration(configuration);
                            }
                            else
                            {
                                await AutomationDSCManager.DeleteCloudConfiguration(configuration, iseClient.automationManagementClient,
                                    iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
                                if (configuration.localFileInfo != null && File.Exists(configuration.localFileInfo.FullName))
                                {
                                    AutomationDSCManager.DeleteLocalConfiguration(configuration);
                                    removeLocalConfigurationToView(configuration.localFileInfo.FullName);
                                    await refreshLocalScripts(configuration.localFileInfo.FullName);
                                }
                            }
                        }
                    }
                }
                await refreshConfigurations();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Could not delete the selected configurations(s).\r\nError details: " + ex.Message);
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

        private void DSCListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                AutomationDSC configuration = ((FrameworkElement)e.OriginalSource).DataContext as AutomationDSC;
                if (configuration != null)
                {
                    ButtonOpenConfiguration_Click(null, null);
                }
            }
            catch { }
        }

        private void ModuleListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                AutomationModule module = ((FrameworkElement)e.OriginalSource).DataContext as AutomationModule;
                if (module != null)
                {
                    ButtonOpenModule_Click(null, null);
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

        private void ConfigurationFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {

                CollectionViewSource.GetDefaultView(DSCListView.ItemsSource).Refresh();
            }
            catch { }
        }

        private void ModuleFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (ModuleListView.ItemsSource != null)
                    CollectionViewSource.GetDefaultView(ModuleListView.ItemsSource).Refresh();
            }
            catch { }
        }

        private bool doBasicRunbookFiltering(object item)
        {
            bool authoringStateMatch = ((item as AutomationRunbook).AuthoringState.IndexOf(RunbookFilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            bool syncStatusMatch = ((item as AutomationRunbook).SyncStatus.IndexOf(RunbookFilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            bool nameMatch = ((item as AutomationRunbook).Name.IndexOf(RunbookFilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            return (authoringStateMatch || syncStatusMatch || nameMatch);
        }

        private bool doBasicConfigurationFiltering(object item)
        {
            bool authoringStateMatch = ((item as AutomationDSC).AuthoringState.IndexOf(ConfigurationFilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            bool syncStatusMatch = ((item as AutomationDSC).SyncStatus.IndexOf(ConfigurationFilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            bool nameMatch = ((item as AutomationDSC).Name.IndexOf(ConfigurationFilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            return (authoringStateMatch || syncStatusMatch || nameMatch);
        }

        private bool doBasicModuleFiltering(object item)
        {
            bool syncStatusMatch = ((item as AutomationModule).SyncStatus.IndexOf(ModuleFilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            bool nameMatch = ((item as AutomationModule).Name.IndexOf(ModuleFilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            return (syncStatusMatch || nameMatch);
        }

        private bool doAdvancedRunbookFiltering(object item)
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


        private bool doAdvancedConfigurationFiltering(object item)
        {
            string[] queries = ConfigurationFilterTextBox.Text.Split(null);
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
                authoringStateMatch = ((item as AutomationDSC).AuthoringState.IndexOf(statusQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!String.IsNullOrEmpty(syncStatusQuery) && syncStatusQuery.Length > 1)
                syncStatusMatch = ((item as AutomationDSC).SyncStatus.IndexOf(syncStatusQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!String.IsNullOrEmpty(nameQuery) && nameQuery.Length > 1)
                nameMatch = ((item as AutomationDSC).Name.IndexOf(nameQuery, StringComparison.OrdinalIgnoreCase) >= 0);

            return (authoringStateMatch && syncStatusMatch && nameMatch);
        }

        private bool doAdvancedModuleFiltering(object item)
        {
            string[] queries = ModuleFilterTextBox.Text.Split(null);
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

            bool syncStatusMatch = String.IsNullOrEmpty(syncStatusQuery) ? true : false;
            bool nameMatch = String.IsNullOrEmpty(nameQuery) ? true : false;
            if (!String.IsNullOrEmpty(syncStatusQuery) && syncStatusQuery.Length > 1)
                syncStatusMatch = ((item as AutomationModule).SyncStatus.IndexOf(syncStatusQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!String.IsNullOrEmpty(nameQuery) && nameQuery.Length > 1)
                nameMatch = ((item as AutomationModule).Name.IndexOf(nameQuery, StringComparison.OrdinalIgnoreCase) >= 0);

            return (syncStatusMatch && nameMatch);
        }

        private bool FilterRunbook(object item)
        {
            if (String.IsNullOrEmpty(RunbookFilterTextBox.Text) || RunbookFilterTextBox.Text.Length < 2 || RunbookFilterTextBox.Text == "Search")
                return true;
            if (RunbookFilterTextBox.Text.IndexOf(':') >= 0)
                return doAdvancedRunbookFiltering(item);
            else
                return doBasicRunbookFiltering(item);
        }

        private bool FilterConfiguration(object item)
        {
            if (String.IsNullOrEmpty(ConfigurationFilterTextBox.Text) || ConfigurationFilterTextBox.Text.Length < 2 || ConfigurationFilterTextBox.Text == "Search")
                return true;
            if (ConfigurationFilterTextBox.Text.IndexOf(':') >= 0)
                return doAdvancedConfigurationFiltering(item);
            else
                return doBasicConfigurationFiltering(item);
        }

        private bool FilterModule(object item)
        {
            if (String.IsNullOrEmpty(ModuleFilterTextBox.Text) || ModuleFilterTextBox.Text.Length < 2 || ModuleFilterTextBox.Text == "Search")
                return true;
            if (ModuleFilterTextBox.Text.IndexOf(':') >= 0)
                return doAdvancedModuleFiltering(item);
            else
            {
                return doBasicModuleFiltering(item);
            }
        }

        private void RunbookFilterFocus(object sender, RoutedEventArgs e)
        {
            if (RunbookFilterTextBox.Text == "Search")
            {
                RunbookFilterTextBox.FontWeight = FontWeights.Normal;
                RunbookFilterTextBox.FontStyle = FontStyles.Normal;
                RunbookFilterTextBox.Text = "";
            }

        }

        private void ConfigurationFilterFocus(object sender, RoutedEventArgs e)
        {
            if (ConfigurationFilterTextBox.Text == "Search")
            {
                ConfigurationFilterTextBox.FontWeight = FontWeights.Normal;
                ConfigurationFilterTextBox.FontStyle = FontStyles.Normal;
                ConfigurationFilterTextBox.Text = "";
            }

        }

        private void ModuleFilterFocus(object sender, RoutedEventArgs e)
        {
            if (ModuleFilterTextBox.Text == "Search")
            {
                ModuleFilterTextBox.FontWeight = FontWeights.Normal;
                ModuleFilterTextBox.FontStyle = FontStyles.Normal;
                ModuleFilterTextBox.Text = "";
            }
        }


        private async void ButtonDeleteModule_Click(object sender, RoutedEventArgs e)
        {
            var moduleList = new List<AutomationModule>();
            foreach (Object obj in ModuleListView.SelectedItems)
            {
                moduleList.Add((AutomationModule)obj);
            }
            try
            {
                beginBackgroundWork("Deleting selected modules...");
                foreach (AutomationModule module in moduleList)
                {
                    if (module.SyncStatus == AutomationDSC.Constants.SyncStatus.CloudOnly)
                    {
                        String message = "Are you sure you wish to delete the cloud copy of " + module.Name + "?  ";
                        message += "There is no local copy.";
                        MessageBoxResult result = MessageBox.Show(message, "Confirm module deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            await AutomationModuleManager.DeleteCloudModule(module, iseClient.automationManagementClient,
                                iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
                            ButtonRefreshModule_Click(true, null);
                        }
                    }
                    else if (module.SyncStatus != AutomationModule.Constants.SyncStatus.LocalOnly)
                    {
                        String message = "Are you sure you wish to delete the cloud copy of " + module.Name + "?  ";
                        message += "Local copy will not be deleted.";
                        MessageBoxResult result = MessageBox.Show(message, "Confirm module Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            await AutomationModuleManager.DeleteCloudModule(module, iseClient.automationManagementClient,
                                iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
                            ButtonRefreshModule_Click(true, null);
                       }
                    }
                }
           }
            catch (Exception ex)
            {
                MessageBox.Show("Could not delete the selected module(s).\r\nError details: " + ex.Message);
            }
            finally
            {
                endBackgroundWork();
            }
        }

        private async void ButtonUploadModule_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                beginBackgroundWork();
                int count = 0;
                string name = "";
                var moduleList = new List<AutomationModule>();
                foreach (Object obj in ModuleListView.SelectedItems)
                {
                    moduleList.Add((AutomationModule)obj);
                }
                foreach (Object obj in moduleList)
                {
                    AutomationModule module = (AutomationModule)obj;
                    if (module.SyncStatus == AutomationModule.Constants.SyncStatus.InSync)
                        continue;
                    try
                    {
                        var azureARMAuthResult = AuthenticateHelper.RefreshTokenByAuthority(Properties.Settings.Default.StorageAuthority, Properties.Settings.Default.loginAuthority);
                        beginBackgroundWork("Uploading module " + module.Name + "...");

                        // Get storage account information stored.
                        var storageAccount = Properties.Settings.Default.StorageAccount;
                        var storageResourceGroup = Properties.Settings.Default.StorageResourceGroup;
                        var storageSubID = Properties.Settings.Default.StorageSubID;
                        module.SyncStatus = "Importing...";
                        await AutomationModuleManager.UploadModule(azureARMAuthResult, module, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount, storageResourceGroup, storageSubID, storageAccount);
                        endBackgroundWork("Uploaded " + module.Name + ".");
                        count++;
                        name = module.Name;
                     }
                    catch (OperationCanceledException)
                    {
                        endBackgroundWork("Uploading " + module.Name + " timed out.");
                    }
                    catch (Exception ex)
                    {
                        endBackgroundWork("Error uploading module " + module.Name);
                        System.Windows.Forms.MessageBox.Show("The module " + module.Name + " could not be uploaded.\r\nError details: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                }

                if (count == 1) endBackgroundWork("Uploaded " + name + ".");
                else if (count > 1) endBackgroundWork("Uploaded " + count + " modules.");
                else endBackgroundWork();
                ButtonRefreshModule_Click(true, null);
            }
            catch (Exception ex)
            {
                endBackgroundWork("Error uploading modules.");
                System.Windows.Forms.MessageBox.Show(ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                SetButtonStatesForSelectedModule();
            }
        }

        private async void ButtonRefreshModule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                beginBackgroundWork();
                ButtonRefreshModule.IsEnabled = false;
                beginBackgroundWork("Refreshing modules");
                RefreshModulesTask();
                var localModules = await getLocalModules();
                // If we don't have any local modules yet, wait 20 seconds to make sure we get them.
                if (localModules.Count < 1)
                {
                    await Task.Delay(20000);
                    localModules = await getLocalModules();
                }
                ModuleListView.ItemsSource = await AutomationModuleManager.GetAllModuleMetadata(iseClient.automationManagementClient,
                                          iseClient.currWorkspace, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name, localModules);
                CollectionView Moduleview = (CollectionView)CollectionViewSource.GetDefaultView(ModuleListView.ItemsSource);
                Moduleview.Filter = FilterModule;
                ModuleListView.Items.SortDescriptions.Clear();
                ModuleListView.Items.SortDescriptions.Add(new SortDescription("LastModifiedCloud", ListSortDirection.Descending));
                ButtonRefreshModule.IsEnabled = true;
                endBackgroundWork("Modules refreshed");
                endBackgroundWork();
            }
            catch (Exception Ex)
            {
                endBackgroundWork("Error refreshing modules");
                endBackgroundWork();
                ButtonRefreshModule.IsEnabled = true;
            }
        }

        private async void RefreshModulesTask()
        {

            Task t = new Task(delegate { RefreshModules(); });
            t.Start();
        }

        private async Task RefreshModules()
        {
            try
            {
                await refreshLocalModules();
            }
            catch (Exception Ex)
            {
                var a = Ex;
            }
        }

        private void RunbookFilterLostFocus(object sender, RoutedEventArgs e)
        {
            if (RunbookFilterTextBox.Text == "")
            {
                RunbookFilterTextBox.FontWeight = FontWeights.Light;
                RunbookFilterTextBox.FontStyle = FontStyles.Italic;
                RunbookFilterTextBox.Text = "Search";
            }

        }

        private void ConfigurationFilterLostFocus(object sender, RoutedEventArgs e)
        {
            if (ConfigurationFilterTextBox.Text == "")
            {
                ConfigurationFilterTextBox.FontWeight = FontWeights.Light;
                ConfigurationFilterTextBox.FontStyle = FontStyles.Italic;
                ConfigurationFilterTextBox.Text = "Search";
            }

        }

        private void ModuleFilterLostFocus(object sender, RoutedEventArgs e)
        {
            if (ModuleFilterTextBox.Text == "")
            {
                ModuleFilterTextBox.FontWeight = FontWeights.Light;
                ModuleFilterTextBox.FontStyle = FontStyles.Italic;
                ModuleFilterTextBox.Text = "Search";
            }

        }

        private void updateButton_Click(object sender, RoutedEventArgs e)
        {
            if (HostObject != null)
            {
                if (MessageBox.Show("Do you want to install the latest PowerShell module from the PowerShell Gallery?  (You will need to restart the ISE after install)",
              "Install AzureAutomationAuthoringToolkit Module", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    HostObject.CurrentPowerShellTab.Invoke("install-module AzureAutomationAuthoringToolkit -Scope CurrentUser -verbose -force");
                }
            }
        }

        private void Hyperlink_ScriptAnalyzer(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);            
        }

        private void runAscheckBox_Checked(object sender, RoutedEventArgs e)
        {
                Properties.Settings.Default.RunAs = true;
                Properties.Settings.Default.Save();
        }

        private void runAscheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
                Properties.Settings.Default.RunAs = false;
                Properties.Settings.Default.Save();
        }

        private async void ButtonStorageModule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonStorageModule.IsEnabled = false;
                var storageDialog = new StorageAccountForModulesDialog();
                storageDialog.subscriptionComboBox.ItemsSource = subscriptionComboBox.Items;
                storageDialog.subscriptionComboBox.DisplayMemberPath = "Name";
                AutomationISEClient.SubscriptionObject existingSubscription = new AutomationISEClient.SubscriptionObject();

                // Set existing subscription if one has been saved
                foreach (AutomationISEClient.SubscriptionObject item in subscriptionComboBox.Items)
                {
                    if (item.SubscriptionId == Properties.Settings.Default.StorageSubID)
                    {
                        existingSubscription = item;
                        break;
                    }
                }

                // Set existing storage account if one has been saved
                if (!(String.IsNullOrEmpty(existingSubscription.SubscriptionId)))
                {
                    storageDialog.subscriptionComboBox.SelectedItem = existingSubscription;
                    storageDialog.resourceGroupcomboBox.SelectedItem = Properties.Settings.Default.StorageResourceGroup;
                    foreach (StorageAccount item in storageDialog.storageAccountcomboBox.Items)
                    {
                        if (item.Name == Properties.Settings.Default.StorageAccount)
                        {
                            storageDialog.storageAccountcomboBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                storageDialog.Owner = (System.Windows.Window) this.Parent;
                var result = storageDialog.ShowDialog();
                if (result.Value && storageDialog.storageAccountName != null && storageDialog.storageResourceGroupName != null && storageDialog.storageSubID != null)
                {
                    if (storageDialog.createNewStorageAccount)
                    {
                        beginBackgroundWork("Creating storage account " + storageDialog.storageAccountName + "...");
                        await AutomationModuleManager.CreateStorageAccount(storageDialog.authority, iseClient.automationManagementClient, iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount, storageDialog.storageResourceGroupName, storageDialog.storageSubID, storageDialog.storageAccountName,storageDialog.region);
                        endBackgroundWork();
                    }
                    // Save storage account information in user.config 
                    Properties.Settings.Default.StorageAccount = storageDialog.storageAccountName;
                    Properties.Settings.Default.StorageResourceGroup = storageDialog.storageResourceGroupName;
                    Properties.Settings.Default.StorageSubID = storageDialog.storageSubID;
                    Properties.Settings.Default.StorageAuthority = storageDialog.authority;
                    Properties.Settings.Default.Save();
                    ButtonUploadModule.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                endBackgroundWork();
                System.Windows.Forms.MessageBox.Show(ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                ButtonStorageModule.IsEnabled = true;
            }
        }

        private void IDEComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IDEComboBox.HasItems)
            {
                Properties.Settings.Default.Editor = IDEComboBox.SelectedItem.ToString();
                Properties.Settings.Default.Save();
            }
        }

        private void AzureEnvironmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (String.Compare(AzureEnvironmentComboBox.SelectedItem.ToString(), "Public Azure") == 0)
            {
                Properties.Settings.Default.loginAuthority = Constants.publicLoginAuthority;
                Properties.Settings.Default.appIdURI = Constants.publicAppIdURI;
            }

            if (String.Compare(AzureEnvironmentComboBox.SelectedItem.ToString(), "US Government Azure") == 0)
            {
                Properties.Settings.Default.loginAuthority = Constants.USGovernmentLoginAuthority;
                Properties.Settings.Default.appIdURI = Constants.USGovernmentAppIdURI;
            }
            Properties.Settings.Default.Save();
        }
    }
}
