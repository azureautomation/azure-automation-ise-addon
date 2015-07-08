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
                throw new Exception("An Azure AD Authentication Result is needed to query Azure for subscriptions.");
            if (subscriptionClient == null)  //lazy instantiation
            {
                if (cloudCredentials == null)
                    subscriptionCredentials = new Microsoft.WindowsAzure.TokenCloudCredentials(azureADAuthResult.AccessToken);
                subscriptionClient = new Microsoft.WindowsAzure.Subscriptions.SubscriptionClient(subscriptionCredentials);
            }
            var cancelToken = new CancellationToken();
            Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse subscriptionsResult = await subscriptionClient.Subscriptions.ListAsync(cancelToken);
            return subscriptionsResult.Subscriptions;
        }

        public async Task<IList<AutomationAccount>> GetAutomationAccounts()
        {
            if(currSubscription == null)
                throw new Exception("Cannot get Automation Accounts until an Azure subscription has been set.");
       //     if (automationManagementClient == null) //lazy instantiation
       //     {
         //       if (subscriptionCreds == null)
          //      {
                    var cloudtoken = AuthenticateHelper.RefreshTokenByAuthority(currSubscription.ActiveDirectoryTenantId);
                    subscriptionCreds = new TokenCloudCredentials(currSubscription.SubscriptionId, cloudtoken.AccessToken);
          //      }
                automationManagementClient = new AutomationManagementClient(subscriptionCreds);
       //     }
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
                throw new Exception("Cannot get Automation Accounts until an Azure subscription has been set.");
     //       if(resourceManagementClient == null) //lazy instantiation
      //      {
     //           if (subscriptionCreds == null)
     //           {
                    var cloudtoken = AuthenticateHelper.RefreshTokenByAuthority(currSubscription.ActiveDirectoryTenantId);
                    subscriptionCreds = new TokenCloudCredentials(currSubscription.SubscriptionId, cloudtoken.AccessToken);
      //         }
                resourceManagementClient = new ResourceManagementClient(subscriptionCreds);
     //       }
            ResourceGroupListResult resourceGroupResult = await resourceManagementClient.ResourceGroups.ListAsync(null);
            return resourceGroupResult.ResourceGroups;
        }

        private async Task<SortedSet<AutomationAsset>> GetAssetsInfo()
        {
            return (SortedSet<AutomationAsset>)await AutomationAssetManager.GetAll(getCurrentAccountWorkspace(), automationManagementClient, accountResourceGroups[currAccount].Name, currAccount.Name);
        }

        public async Task<SortedSet<AutomationAsset>> GetAssetsOfType(String type)
        {
            var assets = await GetAssetsInfo();

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

        public async Task DownloadAllAssets()
        {
           await AutomationAssetManager.DownloadAllFromCloud(getCurrentAccountWorkspace(), automationManagementClient, accountResourceGroups[currAccount].Name, currAccount.Name);
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
