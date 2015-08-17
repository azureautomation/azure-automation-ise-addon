using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomationISE.Model
{
    using AzureAutomation;
    using AutomationISE.Model;
    using Microsoft.Azure;
    using Microsoft.Azure.Management.Resources;
    using Microsoft.Azure.Management.Resources.Models;
    using Microsoft.Azure.Management.Automation;
    using Microsoft.Azure.Management.Automation.Models;
    using Microsoft.Azure.Subscriptions;
    using Microsoft.Azure.Subscriptions.Models;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.IO;
    using System.Threading;
    using System.Net.Http.Headers;

    public class AutomationISEClient
    {
        /* Azure Credential Data */
        public AuthenticationResult azureADAuthResult { get; set; }
        private TokenCloudCredentials cloudCredentials;
        private Microsoft.WindowsAzure.TokenCloudCredentials subscriptionCredentials;
        private SubscriptionCloudCredentials subscriptionCreds;

        /* Azure Clients */
        public AutomationManagementClient automationManagementClient { get; set; }
        private ResourceManagementClient resourceManagementClient;
        private Microsoft.WindowsAzure.Subscriptions.SubscriptionClient subscriptionClient;

        /* User Session Data */
        public Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse.Subscription currSubscription { get; set; }
        public String baseWorkspace { get; set; }
        public String currWorkspace { get; set; }
        private AutomationAccount _currAccount;
        public AutomationAccount currAccount {
            get { return _currAccount; } 
            set
            {
                _currAccount = value;
                if (_currAccount != null)
                {
                    currWorkspace = getCurrentAccountWorkspace();
                }
            }
        }

	    public Dictionary<AutomationAccount, ResourceGroupExtended> accountResourceGroups { get; set; }

        public AutomationISEClient()
        {
            /* Placeholder. All fields null, will only be instantiated when called upon */
        }

        public async Task<IList<Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse.Subscription>> GetSubscriptions()
        {
            if (azureADAuthResult == null)
                throw new Exception(Properties.Resources.AzureADAuthResult);
            if (cloudCredentials == null)
                subscriptionCredentials = new Microsoft.WindowsAzure.TokenCloudCredentials(azureADAuthResult.AccessToken);
            subscriptionClient = new Microsoft.WindowsAzure.Subscriptions.SubscriptionClient(subscriptionCredentials);
           
            var cancelToken = new CancellationToken();
            Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse subscriptionsResult = await subscriptionClient.Subscriptions.ListAsync(cancelToken);
            return subscriptionsResult.Subscriptions;
        }

        public async Task<IList<AutomationAccount>> GetAutomationAccounts()
        {
            if(currSubscription == null)
                throw new Exception(Properties.Resources.SubscriptionNotSet);

            // Get the token for the tenant on this subscription.
            var cloudtoken = AuthenticateHelper.RefreshTokenByAuthority(currSubscription.ActiveDirectoryTenantId);
            subscriptionCreds = new TokenCloudCredentials(currSubscription.SubscriptionId, cloudtoken.AccessToken);

            automationManagementClient = new AutomationManagementClient(subscriptionCreds);
            
            // Add user agent string to indicate this is coming from the ISE automation client.
            ProductInfoHeaderValue ISEClientAgent = new ProductInfoHeaderValue(Constants.ISEUserAgent, Constants.ISEVersion);
            automationManagementClient.UserAgent.Add(ISEClientAgent);

            //TODO: does this belong here?
            if (accountResourceGroups == null)
                accountResourceGroups = new Dictionary<AutomationAccount, ResourceGroupExtended>();
            else
                accountResourceGroups.Clear();
            IList<AutomationAccount> result = new List<AutomationAccount>();
            IList<ResourceGroupExtended> resourceGroups = await this.GetResourceGroups();
            foreach (ResourceGroupExtended resourceGroup in resourceGroups)
            {
                AutomationAccountListResponse accountListResponse = await automationManagementClient.AutomationAccounts.ListAsync(resourceGroup.Name);
                foreach (AutomationAccount account in accountListResponse.AutomationAccounts)
                {
                    result.Add(account);
                    accountResourceGroups.Add(account, resourceGroup);
                }
            }
            return result;
        }

        public async Task<IList<ResourceGroupExtended>> GetResourceGroups()
        {
            if (currSubscription == null)
                throw new Exception(Properties.Resources.SubscriptionNotSet);

            // Get the token for the tenant on this subscription.
            var cloudtoken = AuthenticateHelper.RefreshTokenByAuthority(currSubscription.ActiveDirectoryTenantId);
            subscriptionCreds = new TokenCloudCredentials(currSubscription.SubscriptionId, cloudtoken.AccessToken);

            resourceManagementClient = new ResourceManagementClient(subscriptionCreds);

            ResourceGroupListResult resourceGroupResult = await resourceManagementClient.ResourceGroups.ListAsync(null);
            return resourceGroupResult.ResourceGroups;
        }

        public bool AccountWorkspaceExists()
        {
            return Directory.Exists(currWorkspace);
        }

        private string getCurrentAccountWorkspace()
        {
            //Account must be unique within the ResourceGroup: no need to include region
            string[] pathFolders = new string[] { this.baseWorkspace, currSubscription.SubscriptionName + " - " + currSubscription.SubscriptionId, 
                accountResourceGroups[currAccount].Name, currAccount.Name };
            return System.IO.Path.Combine(pathFolders);
        }
    }
}
