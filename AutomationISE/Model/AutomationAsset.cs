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

namespace AutomationAzure
{
    /// <summary>
    /// The automation asset
    /// </summary>
    public abstract class AutomationAsset : AutomationAuthoringItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationAsset"/> class.
        /// </summary>
        public AutomationAsset(string name, IDictionary<String, Object> valueFields) :
            base(name)
        {
            this.Name = name;
            this.ValueFields = valueFields;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationAsset"/> class.
        /// </summary>
        public AutomationAsset(string name) :
            base(name)
        {
            this.Name = name;
        }

        /*public async Task<List<AutomationAsset>> ListAssets(String type)
        {
            List<AutomationAsset> automationAssetList = new List<AutomationAsset>();
            var assets = await automationManagementClient.Assets.ListAsync(RessourceGroupName, AutomationAccountName);

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
        }*/

        /// <summary>
        /// The value of the asset
        /// </summary>
        public IDictionary<String, Object> ValueFields { get; set; }

    }

    public abstract class AssetJson
    {
        public string Name { get; set; }
        public DateTime LastModified { get; set; }
    }
}
