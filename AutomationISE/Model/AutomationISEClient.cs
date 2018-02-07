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
        private AuthenticationResult azureARMAuthResult;
        private Microsoft.Azure.TokenCloudCredentials subscriptionCredentials;
        private SubscriptionCloudCredentials subscriptionCreds;

        /* Azure Clients */
        public AutomationManagementClient automationManagementClient { get; set; }
        private ResourceManagementClient resourceManagementClient;
        private Microsoft.Azure.Subscriptions.SubscriptionClient subscriptionClient;

        /* User Session Data */
        public AutomationISEClient.SubscriptionObject currSubscription { get; set; }
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
        private static int TIMEOUT_MS = 60000;

        public AutomationISEClient()
        {
            /* Placeholder. All fields null, will only be instantiated when called upon */
        }

        public async Task<IList<SubscriptionObject>> GetSubscriptions()
        {
            if (azureADAuthResult == null)
                throw new Exception(Properties.Resources.AzureADAuthResult);

            // Common subscription object to host subscriptions from RDFE & ARM
            IList<SubscriptionObject> subscriptionList = new List<SubscriptionObject>();
 
            subscriptionCredentials = new Microsoft.Azure.TokenCloudCredentials(azureADAuthResult.AccessToken);
            subscriptionClient = new Microsoft.Azure.Subscriptions.SubscriptionClient(subscriptionCredentials, new Uri(Properties.Settings.Default.appIdURI));

            var cancelToken = new CancellationToken();

            var tenants = subscriptionClient.Tenants.ListAsync(cancelToken).Result;
            // Get subscriptions for each tenant
            foreach (var tenant in tenants.TenantIds)
            {
                try
                {
                    AuthenticationResult tenantTokenCreds =
                        AuthenticateHelper.RefreshTokenByAuthority(tenant.TenantId,
                            Properties.Settings.Default.appIdURI);
                    subscriptionCredentials = new Microsoft.Azure.TokenCloudCredentials(tenantTokenCreds.AccessToken);
                    var tenantSubscriptionClient =
                        new Microsoft.Azure.Subscriptions.SubscriptionClient(subscriptionCredentials,
                            new Uri(Properties.Settings.Default.appIdURI));
                    var subscriptionListResults = tenantSubscriptionClient.Subscriptions.ListAsync(cancelToken).Result;

                    foreach (var subscription in subscriptionListResults.Subscriptions)
                    {
                        var subList = new SubscriptionObject();
                        subList.Name = subscription.DisplayName;
                        subList.SubscriptionId = subscription.SubscriptionId;
                        subList.Authority = tenant.TenantId;
                        subscriptionList.Add(subList);
                    }
                }
                catch (Exception ex)
                {
                    // ignored
                }
            }

            return subscriptionList;
        }

        /// <summary>
        /// Contains information about the subscription
        /// </summary>
        public struct SubscriptionObject
        {
            public string Name { get; set; }
            public string SubscriptionId { get; set; }
            public string Authority { get; set; }
        }

        public async Task<IList<Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse.Subscription>> GetRDFESubscriptions()
        {
            if (azureADAuthResult == null)
                throw new Exception(Properties.Resources.AzureADAuthResult);
            var subscriptionCredentials = new Microsoft.WindowsAzure.TokenCloudCredentials(azureADAuthResult.AccessToken);
            var subscriptionClient = new Microsoft.WindowsAzure.Subscriptions.SubscriptionClient(subscriptionCredentials, new Uri(Properties.Settings.Default.appIdURI));

            var cancelToken = new CancellationToken();
            Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionListOperationResponse subscriptionResults = await subscriptionClient.Subscriptions.ListAsync(cancelToken);
            return subscriptionResults.Subscriptions;
        }

        /// <summary>
        /// Refreshes the token used to access azure automation.
        /// This is currently called from a timer that runs on the Constants.tokenRefreshInterval
        /// If it is about to expire (2 minutes from the next refresh, it will renew)
        /// </summary>
        public void RefreshAutomationClientwithNewToken()
        {
            // Get the token for the tenant on this subscription and check if it is about to expire.
            // If it is, refresh it if possible.
            if (currSubscription.Name == null) return;
            if (azureARMAuthResult.ExpiresOn.ToLocalTime() < DateTime.Now.AddMinutes(Constants.tokenRefreshInterval + 2))
            {
                azureARMAuthResult = AuthenticateHelper.RefreshTokenByAuthority(currSubscription.Authority, Properties.Settings.Default.appIdURI);
                subscriptionCreds = new TokenCloudCredentials(currSubscription.SubscriptionId, azureARMAuthResult.AccessToken);

                automationManagementClient = new AutomationManagementClient(subscriptionCreds, new Uri(Properties.Settings.Default.appIdURI));

                // Add user agent string to indicate this is coming from the ISE automation client.
                ProductInfoHeaderValue ISEClientAgent = new ProductInfoHeaderValue(Constants.ISEUserAgent, Constants.ISEVersion);
                automationManagementClient.UserAgent.Add(ISEClientAgent);
            }
        }

        public async Task<IList<AutomationAccount>> GetAutomationAccounts()
        {
            if(currSubscription.Name == null)
                throw new Exception(Properties.Resources.SubscriptionNotSet);

            // Get the token for the tenant on this subscription.
            azureARMAuthResult = AuthenticateHelper.RefreshTokenByAuthority(currSubscription.Authority, Properties.Settings.Default.appIdURI);
            subscriptionCreds = new TokenCloudCredentials(currSubscription.SubscriptionId, azureARMAuthResult.AccessToken);

            automationManagementClient = new AutomationManagementClient(subscriptionCreds, new Uri(Properties.Settings.Default.appIdURI));
            
            // Add user agent string to indicate this is coming from the ISE automation client.
            ProductInfoHeaderValue ISEClientAgent = new ProductInfoHeaderValue(Constants.ISEUserAgent, Constants.ISEVersion);
            automationManagementClient.UserAgent.Add(ISEClientAgent);

            //TODO: does this belong here?
            if (accountResourceGroups == null)
                accountResourceGroups = new Dictionary<AutomationAccount, ResourceGroupExtended>();
            else
                accountResourceGroups.Clear();

            IList<AutomationAccount> result = new List<AutomationAccount>();

            // Get ARM automation account resources
            var automationResources = await GetAutomationResources();

            // Retrieve all of the automation accounts found
            foreach (var resource in automationResources.Resources)
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);

                // Find the resource group name from the resource id.
                var startPosition = resource.Id.IndexOf("/resourceGroups/");
                var endPosition = resource.Id.IndexOf("/", startPosition + 16);
                var resourceGroup = resource.Id.Substring(startPosition + 16, endPosition - startPosition - 16);

                AutomationAccountGetResponse account = await automationManagementClient.AutomationAccounts.GetAsync(resourceGroup,resource.Name, cts.Token);
                result.Add(account.AutomationAccount);
                var accountResourceGroup = new ResourceGroupExtended();
                accountResourceGroup.Name  = resourceGroup;
                accountResourceGroups.Add(account.AutomationAccount, accountResourceGroup);
            }
            return result;
        }

        public async Task<ResourceListResult> GetAutomationResources()
        {

            if (currSubscription.Name == null)
                throw new Exception(Properties.Resources.SubscriptionNotSet);

            // Get the token for the tenant on this subscription.
            var cloudtoken = AuthenticateHelper.RefreshTokenByAuthority(azureARMAuthResult.TenantId, Properties.Settings.Default.appIdURI);
            subscriptionCreds = new TokenCloudCredentials(currSubscription.SubscriptionId, cloudtoken.AccessToken);
            resourceManagementClient = new ResourceManagementClient(subscriptionCreds, new Uri(Properties.Settings.Default.appIdURI));

            // Only get automation account resources
            ResourceListParameters automationResourceParams = new ResourceListParameters();
            automationResourceParams.ResourceType = "Microsoft.Automation/automationAccounts";
            ResourceListResult resources = await resourceManagementClient.Resources.ListAsync(automationResourceParams);
            return resources;
        }

        public async Task<IList<ResourceGroupExtended>> GetResourceGroups()
        {
            if (currSubscription.Name == null)
                throw new Exception(Properties.Resources.SubscriptionNotSet);

            // Get the token for the tenant on this subscription.
            var cloudtoken = AuthenticateHelper.RefreshTokenByAuthority(azureARMAuthResult.TenantId, Properties.Settings.Default.appIdURI);
            subscriptionCreds = new TokenCloudCredentials(currSubscription.SubscriptionId, cloudtoken.AccessToken);

            resourceManagementClient = new ResourceManagementClient(subscriptionCreds, new Uri(Properties.Settings.Default.appIdURI));

            ResourceGroupListResult resourceGroupResult = await resourceManagementClient.ResourceGroups.ListAsync(null);

            return resourceGroupResult.ResourceGroups;
        }

        public bool AccountWorkspaceExists()
        {
            return Directory.Exists(currWorkspace);
        }

        private string getCurrentAccountWorkspace()
        {
            //Account must be unique within the ResourceGroup: no need to include region.
            // Remove any invalid characters in the subscription name. Might have to clean others if required later.
            string subscriptionName = new string(currSubscription.Name.Where(x => !(Path.GetInvalidFileNameChars()).Contains(x)).ToArray());

            string[] pathFolders = new string[] { this.baseWorkspace, subscriptionName  + " - " + currSubscription.SubscriptionId, 
                accountResourceGroups[currAccount].Name, currAccount.Name };

            return System.IO.Path.Combine(pathFolders);
        }
    }
}
