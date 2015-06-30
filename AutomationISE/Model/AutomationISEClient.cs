using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomationISE.Model
{
    using AzureAutomation;
    //using AutomationAzure;
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
    public class AutomationISEClient
    {
        /* Azure Credential Data */
        public AuthenticationResult azureADAuthResult { get; set; }
        private TokenCloudCredentials cloudCredentials;
        private SubscriptionCloudCredentials subscriptionCreds;
        /* Azure Clients */
        private ResourceManagementClient resourceManagementClient;
        private AutomationManagementClient automationManagementClient ;
        private SubscriptionClient subscriptionClient;
        /* User Session Data */
        public Subscription currSubscription { get; set; }
        public String workspace { get; set; }

        public AutomationISEClient()
        {
            /* Assign all members to null. They will only be instantiated when called upon */
            azureADAuthResult = null;
            cloudCredentials = null;
            subscriptionCreds = null;
            subscriptionClient = null;
            currSubscription = null;
        }

        public async Task<IList<Subscription>> GetSubscriptions()
        {
            if (azureADAuthResult == null)
                throw new Exception("An Azure AD Authentication Result is needed to query Azure for subscriptions.");
            if (subscriptionClient == null)  //lazy instantiation
            {
                if (cloudCredentials == null)
                    cloudCredentials = new TokenCloudCredentials(azureADAuthResult.AccessToken);
                subscriptionClient = new SubscriptionClient(cloudCredentials);
            }
            SubscriptionListResult subscriptionsResult = await subscriptionClient.Subscriptions.ListAsync();
            return subscriptionsResult.Subscriptions;
        }
        public async Task<IList<AutomationAccount>> GetAutomationAccounts()
        {
            //TODO: use right kind of Automation Account class (our custom one, if it is the best solution)
            if(currSubscription == null)
                throw new Exception("Cannot get Automation Accounts until an Azure subscription has been set.");
            if (automationManagementClient == null) //lazy instantiation
            {
                if (subscriptionCreds == null)
                    subscriptionCreds = new TokenCloudCredentials(currSubscription.SubscriptionId, azureADAuthResult.AccessToken);
                automationManagementClient = new AutomationManagementClient(subscriptionCreds);
            }
            IList<AutomationAccount> result = new List<AutomationAccount>();
            IList<ResourceGroupExtended> resourceGroups = await this.GetResourceGroups();
            foreach (ResourceGroupExtended resourceGroup in resourceGroups)
            {
                AutomationAccountListResponse accountListResponse = await automationManagementClient.AutomationAccounts.ListAsync(resourceGroup.Name);
                foreach (AutomationAccount account in accountListResponse.AutomationAccounts)
                {
                    result.Add(account);
                }
            }
            return result;
        }
        public async Task<IList<ResourceGroupExtended>> GetResourceGroups()
        {
            if (currSubscription == null)
                throw new Exception("Cannot get Automation Accounts until an Azure subscription has been set.");
            if(resourceManagementClient == null) //lazy instantiation
            {
                if (subscriptionCreds == null )
                    subscriptionCreds = new TokenCloudCredentials(currSubscription.SubscriptionId, azureADAuthResult.AccessToken);
                resourceManagementClient = new ResourceManagementClient(subscriptionCreds);
            }
            ResourceGroupListResult resourceGroupResult = await resourceManagementClient.ResourceGroups.ListAsync(null);
            return resourceGroupResult.ResourceGroups;
        }
    }
}