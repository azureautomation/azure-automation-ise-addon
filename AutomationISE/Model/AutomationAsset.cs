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
using Microsoft.Azure.Management.Automation;
using Microsoft.Azure.Management.Automation.Models;

namespace AutomationAzure
{
    /// <summary>
    /// The automation asset
    /// </summary>
    public abstract class AutomationAsset : AutomationAuthoringItem, IComparable<AutomationAsset>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationAsset"/> class.
        /// </summary>
        public AutomationAsset(string name, DateTime? lastModifiedLocal, DateTime? lastModifiedCloud, IDictionary<String, Object> valueFields) :
            base(name, lastModifiedLocal, lastModifiedCloud)
        {
            this.ValueFields = valueFields;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationAsset"/> class.
        /// </summary>
        public AutomationAsset(string name, DateTime? lastModifiedLocal, DateTime? lastModifiedCloud) :
            base(name, lastModifiedLocal, lastModifiedCloud)
        {
            this.ValueFields = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationAsset"/> class.
        /// </summary>
        public AutomationAsset(AssetJson localJson, DateTime? lastModifiedCloud) :
            base(localJson.Name, localJson.LastModified, lastModifiedCloud)
        {
            this.ValueFields = null;
        }

        public static async Task<ISet<AutomationAsset>> GetAll(String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName)
        {
            VariableListResponse cloudVariables = await automationApi.Variables.ListAsync(resourceGroupName, automationAccountName);
            CredentialListResponse cloudCredentials = await automationApi.PsCredentials.ListAsync(resourceGroupName, automationAccountName);

            LocalAssets.LocalAssetsContainerJson localAssets = LocalAssets.Get(localWorkspacePath);

            SortedSet<AutomationAsset> automationAssets = new SortedSet<AutomationAsset>();
            createAssetsOfType(automationAssets, localAssets.Variables, cloudVariables.Variables, "Variable");

            return automationAssets;
            
        }

        // TODO: how to make generic?
        private static void createAssetsOfType(SortedSet<AutomationAsset> automationAssets, List<VariableJson> localAssets, IList<Variable> cloudAssets, string type)
        {
            
            // Find all variables
            foreach (var cloudAsset in cloudAssets)
            {
                var localAsset = localAssets.Find(asset => asset.Name == cloudAsset.Name);

                var automationAsset = (localAsset != null) ?
                        new AutomationVariable(localAsset, cloudAsset) :
                        new AutomationVariable(cloudAsset);

                automationAssets.Add(automationAsset);
            }

            // Add remaining locally created variables
            foreach (var localAsset in localAssets)
            {
                var automationAsset = new AutomationVariable(localAsset);
                automationAssets.Add(automationAsset);
            }
        }

        public int CompareTo(AutomationAsset other)
        {
            if (this.GetType().Equals(other.GetType()))
            {
                if (this.Name != null)
                {
                    return this.Name.CompareTo(other.Name);
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                return this.GetType().FullName.CompareTo(other.GetType().FullName);
            }
        }

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
