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
using AutomationAzure;
using System.Security;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.Subscriptions.Models;
using System.Threading;
using System.Windows.Threading;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for AutomationISEControl.xaml
    /// </summary>
    public partial class AutomationISEControl : UserControl, IAddOnToolHostObject
    {
        AutomationSubscription subscriptionClient;
        public string workspace;
        public AutomationISEControl()
        {
            try
            {
                InitializeComponent();

                var localWorkspace = Properties.Settings.Default["localWorkspace"].ToString();
                if (localWorkspace == "")
                {
                    var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") + "\\";
                    localWorkspace = System.IO.Path.Combine(systemDrive, "AutomationWorkspace");
                }
                workspaceTextBox.Text = localWorkspace;

                assetsComboBox.Items.Add(Constants.assetVariable);
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

                Properties.Settings.Default["localWorkspace"] = workspaceTextBox.Text;
                Properties.Settings.Default.Save();

                UpdateStatusBox(configurationStatusTextBox, "Launching login window to sign in");
                AuthenticationResult ADToken = AuthenticateHelper.GetInteractiveLogin();

                subscriptionClient = new AutomationAzure.AutomationSubscription(ADToken, workspaceTextBox.Text);

                UpdateStatusBox(configurationStatusTextBox, Properties.Resources.RetrieveSubscriptions);
                SubscriptionListResult subscriptions = await subscriptionClient.ListSubscriptions();
                if (subscriptions.Subscriptions.Count > 0)
                {
                    UpdateStatusBox(configurationStatusTextBox, Properties.Resources.FoundSubscriptions);
                    subscriptionComboBox.ItemsSource = subscriptions.Subscriptions;
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
                Subscription subscription = (Subscription)subscriptionComboBox.SelectedValue;
                if (subscription != null)
                {
                    UpdateStatusBox(configurationStatusTextBox, Properties.Resources.RetrieveAutomationAccounts);
                    List<AutomationAccount> automationAccounts = await subscriptionClient.ListAutomationAccounts(subscription);
                    accountsComboBox.ItemsSource = automationAccounts;
                    accountsComboBox.DisplayMemberPath = "AutomationAccountName";
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
                AutomationAccount automationAccount = (AutomationAccount)accountsComboBox.SelectedValue;
                if (automationAccount != null)
                {
                    List<AutomationRunbook> runbooksList = await automationAccount.ListRunbooks();
                    RunbookslistView.ItemsSource = runbooksList;
                }
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }

        }

        private async void assetsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {


        }

        private async void assetsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedAsset = assetsComboBox.SelectedValue;
                if (selectedAsset.ToString() == Constants.assetVariable)
                {
                    AutomationAccount automationAccount = (AutomationAccount)accountsComboBox.SelectedValue;
                    List<AutomationVariable> variablesList = await automationAccount.ListVariables();
                    assetsListView.ItemsSource = variablesList;
                }
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

         void workspaceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default["localWorkspace"] = workspaceTextBox.Text;
            Properties.Settings.Default.Save();
        }

        private void workspaceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.SelectedPath = workspaceTextBox.Text;
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                workspaceTextBox.Text = dialog.SelectedPath;

                UpdateStatusBox(configurationStatusTextBox, "Saving workspace location: " + workspaceTextBox.Text);
                Properties.Settings.Default["localWorkspace"] = dialog.SelectedPath;
                Properties.Settings.Default.Save();
            }
            catch (Exception exception)
            {
                var detailsDialog = System.Windows.Forms.MessageBox.Show(exception.Message);
            }
        }

        private void configurationStatusTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void UpdateStatusBox(System.Windows.Controls.TextBox statusTextBox, String Message)
        {
            var dispatchMessage = Dispatcher.BeginInvoke(DispatcherPriority.Send, (SendOrPostCallback)delegate
            {
                statusTextBox.AppendText(Message + "\r\n");
            }, null);

            System.Windows.Forms.Application.DoEvents();
        }
    }

}
