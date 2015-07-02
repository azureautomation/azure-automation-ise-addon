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

using Microsoft.Azure.Management.Automation;

namespace AutomationISE.Model
{
    using AzureAutomation;
    using Microsoft.Azure;
    using Microsoft.Azure.Management.Automation.Models;
    using Microsoft.Azure.Management.Resources;
    using Microsoft.Azure.Management.Resources.Models;
    using Microsoft.Azure.Subscriptions;
    using Microsoft.Azure.Subscriptions.Models;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// The automation account.
    /// </summary>
    public class ResourceGroup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceGroup"/> class.
        /// </summary>
        /// <param name="subscriptionResource">
        /// The azure subscription
        /// </param>
        /// <param name="ADToken">
        /// The AD token.
        /// </param>
        public ResourceGroup(Microsoft.Azure.Subscriptions.Models.Subscription subscriptionResource, AuthenticationResult ADToken)
        {
            Requires.Argument("subscriptionResource", subscriptionResource).NotNull();
            Requires.Argument("ADToken", ADToken).NotNull();

            SubscriptionCloudCredentials cred = new TokenCloudCredentials(subscriptionResource.SubscriptionId, ADToken.AccessToken);
            this.SubscriptionCredential = cred;

            ResourceManagementClient resourceManagementClient = new ResourceManagementClient(cred);
            this.resourceManagementClient = resourceManagementClient;

            AutomationManagementClient automationManagementClient = new AutomationManagementClient(SubscriptionCredential);
            this.automationManagementClient = automationManagementClient;
        }

        public async Task<ResourceGroupListResult> ListResourceGroups()
        {
            return await resourceManagementClient.ResourceGroups.ListAsync(null);
        }

        /// <summary>
        /// Finds all the automation accounts in the resource groups for this subscription
        /// </summary>
        /// <returns></returns>
        public async Task<List<AutomationAccountOld>> ListAutomationAccounts()
        {
           List<AutomationAccountOld> automationAccountList = new List<AutomationAccountOld>();

            ResourceGroupListResult resourceGroups = await resourceManagementClient.ResourceGroups.ListAsync(null);

            foreach (var group in resourceGroups.ResourceGroups)
            {
                AutomationAccountListResponse accountList = await automationManagementClient.AutomationAccounts.ListAsync(group.Name);

                foreach (var automationAccount in accountList.AutomationAccounts)
                {
                    var Account = new AutomationAccountOld(automationManagementClient, automationAccount, group, Workspace);
                    automationAccountList.Add(Account);
                }
            }

            return automationAccountList;
        }

        public string Workspace { get; set; }

        /// <summary>
        /// Gets or sets the resource management client.
        /// </summary>
        public ResourceManagementClient resourceManagementClient { get; set; } 

        /// <summary>
        /// Gets or sets the automation management client
        /// </summary>
        public AutomationManagementClient automationManagementClient { get; set; }

        /// <summary>
        /// Gets or sets the subscription credentials.
        /// </summary>
        public SubscriptionCloudCredentials SubscriptionCredential { get; set; }

    }
}
