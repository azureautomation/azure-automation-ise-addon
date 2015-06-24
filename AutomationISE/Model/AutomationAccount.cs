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
    using Microsoft.Azure.Management.Automation.Models;
    using Microsoft.Azure.Management.Resources.Models;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AutomationManagement = Microsoft.Azure.Management.Automation;

    /// <summary>
    /// The automation account.
    /// </summary>
    public class AutomationAccount
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationAccount"/> class.
        /// </summary>
        /// <param name="automationResource">
        /// The cloud service.
        /// </param>
        /// <param name="resourceGroup">
        /// The resource.
        /// </param>
        public AutomationAccount(AutomationManagementClient automationClient, AutomationManagement.Models.AutomationAccount automationResource, ResourceGroupExtended resourceGroup, string automationAccountWorkspace)
        {
            Requires.Argument("resourceGroup", resourceGroup).NotNull();
            Requires.Argument("automationResource", automationResource).NotNull();

            this.automationManagementClient = automationClient;
            this.AutomationAccountName = automationResource.Name;
            this.Location = automationResource.Location;
            string accountPath = automationClient.Credentials.SubscriptionId + "\\" + resourceGroup.Name + "\\" + automationResource.Location + "\\"+ AutomationAccountName;
            this.automationAccountWorkspace = System.IO.Path.Combine(automationAccountWorkspace, accountPath);

            this.RessourceGroupName = resourceGroup.Name;

            switch (automationResource.Properties.State)
            {
                case AutomationManagement.Models.AutomationAccountState.Ok:
                    this.State = Constants.AutomationAccountState.Ready;
                    break;
                case AutomationManagement.Models.AutomationAccountState.Suspended:
                    this.State = Constants.AutomationAccountState.Suspended;
                    break;
                default:
                    this.State = automationResource.Properties.State;
                    break;
            }

            if (automationResource.Properties.Sku != null) this.Plan = automationResource.Properties.Sku.Name;
        }

        /// <summary>
        ///  Gets or sets the automation management client
        /// </summary>
        public AutomationManagementClient automationManagementClient { get; set; }

        /// <summary>
        ///  Gets a list of runbooks for this automation account
        /// </summary>
        /// <returns></returns>
        public async Task<List<AutomationRunbook>> ListRunbooks()
        {
            List<AutomationRunbook> automationRunbookList = new List<AutomationRunbook>();
            var runbooks = await automationManagementClient.Runbooks.ListAsync(RessourceGroupName, AutomationAccountName);
            foreach (var runbook in runbooks.Runbooks)
            {
                // Only add runbooks that are type script
                if (runbook.Properties.RunbookType == Constants.RunbookType.Script)
                {
                    var automationRunbook = new AutomationRunbook(automationManagementClient, RessourceGroupName, AutomationAccountName, runbook);
                    automationRunbookList.Add(automationRunbook);
                }
            }
            return automationRunbookList;
        }

        public async Task<List<AutomationVariable>> ListVariables()
        {
            var automationVariableClient = new AutomationVariable(automationManagementClient, RessourceGroupName, AutomationAccountName, automationAccountWorkspace);
            return await automationVariableClient.ListVariables();
        }

        public string automationAccountWorkspace { get; set; }

        /// <summary>
        /// Gets or sets the automation account name.
        /// </summary>
        public string AutomationAccountName { get; set; }

        /// <summary>
        /// Gets or sets the Resource group name for this automation account.
        /// </summary>
        public string RessourceGroupName { get; set; }

        /// <summary>
        /// Gets or sets the location.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// Gets or sets the plan.
        /// </summary>
        public string Plan { get; set; }
    }
}
