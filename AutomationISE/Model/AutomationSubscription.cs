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

namespace AutomationAzure
{
    using AzureAutomation;
    using Microsoft.Azure;
    using Microsoft.Azure.Management.Resources;
    using Microsoft.Azure.Subscriptions;
    using Microsoft.Azure.Subscriptions.Models;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// The subscriptions in Azure related to Automation accounts.
    /// </summary>
    public class AutomationSubscription
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
        public AutomationSubscription(AuthenticationResult ADToken, string workspace)
        {
            Requires.Argument("ADToken", ADToken).NotNull();
            Requires.Argument("workspace", ADToken).NotNull();

            TokenCloudCredentials cloudCredential = new TokenCloudCredentials(ADToken.AccessToken);
            SubscriptionClient subscriptionClient = new SubscriptionClient(cloudCredential);
            this.subscriptionClient = subscriptionClient;
            this.ADToken = ADToken;
            this.Workspace = workspace;

        }

        public async Task<SubscriptionListResult> ListSubscriptions()
        {
            return await subscriptionClient.Subscriptions.ListAsync();
        }

        public async Task<List<AutomationAccount>> ListAutomationAccounts(Microsoft.Azure.Subscriptions.Models.Subscription subscription)
        {
            ResourceGroup ResourceClient = new ResourceGroup(subscription, ADToken);
            ResourceClient.Workspace = Workspace;

            List<AutomationAccount> automationAccounts = await ResourceClient.ListAutomationAccounts();
            return automationAccounts;
        }

        public string Workspace { get; set; }

        /// <summary>
        /// Gets or sets the subscriptionClient.
        /// </summary>
        public SubscriptionClient subscriptionClient { get; set; } 

        public AuthenticationResult ADToken { get; set; }

    }
}
