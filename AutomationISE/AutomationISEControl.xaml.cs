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
            InitializeComponent();
            var ADUserName = Properties.Settings.Default["ADUserName"].ToString();
            azureADTextBox.Text = ADUserName;

            var localWorkspace = Properties.Settings.Default["localWorkspace"].ToString();
            workspaceTextBox.Text = localWorkspace;

            assetsComboBox.Items.Add(Constants.assetVariable);
        }

        public ObjectModelRoot HostObject
        {
            get;
            set;
        }

        private async void loginButton_Click(object sender, RoutedEventArgs e)
        {
            try {
                String ADUsername = azureADTextBox.Text;
                SecureString ADPassword = passwordBox.SecurePassword;
                Properties.Settings.Default["ADUserName"] = ADUsername;
                Properties.Settings.Default["localWorkspace"] = workspaceTextBox.Text;
                Properties.Settings.Default.Save();

                AuthenticationResult ADToken = await AuthenticateHelper.GetAuthorizationHeader(ADUsername, ADPassword);
                subscriptionClient = new AutomationAzure.AutomationSubscription(ADToken, workspaceTextBox.Text);


                SubscriptionListResult subscriptions = await subscriptionClient.ListSubscriptions();
                subscriptionComboBox.ItemsSource = subscriptions.Subscriptions;
                subscriptionComboBox.DisplayMemberPath = "DisplayName";
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
            Subscription subscription = (Subscription) subscriptionComboBox.SelectedValue;
            List<AutomationAccount> automationAccounts = await subscriptionClient.ListAutomationAccounts(subscription);
            accountsComboBox.ItemsSource = automationAccounts;
            accountsComboBox.DisplayMemberPath = "AutomationAccountName";

        }

        private async void accountsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AutomationAccount automationAccount = (AutomationAccount) accountsComboBox.SelectedValue;
            List<AutomationRunbook> runbooksList = await automationAccount.ListRunbooks();
            RunbookslistView.ItemsSource = runbooksList;

        }

        private async void assetsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {


        }

        private async void assetsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedAsset = assetsComboBox.SelectedValue;
            if (selectedAsset.ToString() == Constants.assetVariable)
            {
        //        List<StaticAssets.VariableJson> assetVariableList = new List<StaticAssets.VariableJson>();
          //      StaticAssets.VariableJson variables = new StaticAssets.VariableJson();

                AutomationAccount automationAccount = (AutomationAccount)accountsComboBox.SelectedValue;
                List<AutomationVariable> variablesList = await automationAccount.ListVariables();
                assetsListView.ItemsSource = variablesList;
            }
        }

         void workspaceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }

}
