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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using AutomationManagement = Microsoft.Azure.Management.Automation;

    /// <summary>
    /// The automation variable.
    /// </summary>
    public class AutomationVariable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationVariable"/> class.
        /// </summary>
        public AutomationVariable(AutomationManagementClient automationClient, String resourceGroup, String automationResource, Variable variable, bool encrypted = false)
        {
            Requires.Argument("variable", variable).NotNull();

            this.Name = variable.Name;
            this.Status = Constants.Status.CloudOnly;
            this.LastModified = variable.Properties.LastModifiedTime.DateTime;
            this.Encrypted = variable.Properties.IsEncrypted;

            this.automationManagementClient = automationClient;
            this.RessourceGroupName = resourceGroup;
            this.AutomationAccountName = automationResource;
        }

        public AutomationVariable(AutomationManagementClient automationClient, String resourceGroup, String automationResource, String workspace)
        {
            Requires.Argument("automationClient", automationClient).NotNull();
            Requires.Argument("resourceGroup", resourceGroup).NotNull();
            Requires.Argument("automationResource", automationResource).NotNull();

            this.automationManagementClient = automationClient;
            this.RessourceGroupName = resourceGroup;
            this.AutomationAccountName = automationResource;
            this.Workspace = workspace;
        }

        public AutomationVariable(StaticAssets.VariableJson variable, bool encrypted = false)
        {
            Requires.Argument("variable", variable).NotNull();

            this.Name = variable.Name;
            this.Status = Constants.Status.LocalOnly;
            this.LastModified = variable.LastModified;
            this.Encrypted = encrypted;

        }

        public async Task<List<AutomationVariable>> ListVariables()
        {
            List<AutomationVariable> automationVariableList = new List<AutomationVariable>();
            var variables = await automationManagementClient.Variables.ListAsync(RessourceGroupName, AutomationAccountName);
            var staticAsset = new StaticAssets("StaticAssets.json", "SecureStaticAssets.json",Workspace);
            List<AutomationVariable> staticAssets = staticAsset.GetVariableAssets();

            // Find all variables
            foreach (var variableAsset in variables.Variables)
            {
                var staticVar = staticAssets.FirstOrDefault(x => x.Name == variableAsset.Name);
                if (staticVar != null)
                {
                    var automationVariable = new AutomationVariable(automationManagementClient, RessourceGroupName, AutomationAccountName, variableAsset);
                    if (staticVar.LastModified > automationVariable.LastModified)
                    {
                        staticVar.Status = Constants.Status.InSync;
                        automationVariableList.Add(staticVar);
                    }
                    else
                    {
                        automationVariable.Status = Constants.Status.InSync;
                        automationVariableList.Add(automationVariable);
                    }
                    staticAssets.Remove(staticVar);
                }
                else
                {
                    var automationVariable = new AutomationVariable(automationManagementClient, RessourceGroupName, AutomationAccountName, variableAsset);
                    automationVariableList.Add(automationVariable);
                }
            }

            // Add remaining locally created assets
            foreach (var variableAsset in staticAssets)
            {
                automationVariableList.Add(variableAsset);
            }

            return automationVariableList;
        }

        public string Workspace { get; set; }

        /// <summary>
        ///  Gets or sets the automation management client
        /// </summary>
        public AutomationManagementClient automationManagementClient { get; set; }

        /// <summary>
        /// Gets or sets the automation account name.
        /// </summary>
        public string AutomationAccountName { get; set; }

        /// <summary>
        /// Gets or sets the Resource group name for this automation account.
        /// </summary>
        public string RessourceGroupName { get; set; }
        /// <summary>
        /// The name of the runbook
        /// </summary>
        public string Name { get; set; }

        public bool Encrypted { get; set; }

        /// <summary>
        /// The cloud status for the variable
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// The local status for the variable
        /// </summary>
        public string LocalStatus { get; set; }

        /// <summary>
        /// The last modified date of the variable
        /// </summary>
        public DateTime LastModified { get; set; }
    }
}
