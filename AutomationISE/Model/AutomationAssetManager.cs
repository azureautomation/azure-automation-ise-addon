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
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Automation;
using Microsoft.Azure.Management.Automation.Models;

namespace AutomationISE.Model
{
    public class AutomationAssetManager 
    {
        public static async void DownloadAllFromCloud(String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName)
        {
            var assets = await AutomationAssetManager.GetAll(null, automationApi, resourceGroupName, automationAccountName);
            AutomationAssetManager.SaveLocally(localWorkspacePath, assets);
        }

        public static void SaveLocally(String localWorkspacePath, ISet<AutomationAsset> assets)
        {
            LocalAssetsStore.Set(localWorkspacePath, assets);
        }

        public static async Task<ISet<AutomationAsset>> GetAll(String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName)
        {
            VariableListResponse cloudVariables = await automationApi.Variables.ListAsync(resourceGroupName, automationAccountName);
            CredentialListResponse cloudCredentials = await automationApi.PsCredentials.ListAsync(resourceGroupName, automationAccountName);
            ConnectionListResponse cloudConnections = await automationApi.Connections.ListAsync(resourceGroupName, automationAccountName);

            LocalAssets localAssets = LocalAssetsStore.Get(localWorkspacePath);

            var automationAssets = new SortedSet<AutomationAsset>();

            // Compare cloud variables to local
            foreach (var cloudAsset in cloudVariables.Variables)
            {
                var localAsset = localAssets.Variables.Find(asset => asset.Name == cloudAsset.Name);

                var automationAsset = (localAsset != null) ?
                        new AutomationVariable(localAsset, cloudAsset) :
                        new AutomationVariable(cloudAsset);

                automationAssets.Add(automationAsset);
            }

            // Add remaining locally created variables
            foreach (var localAsset in localAssets.Variables)
            {
                var automationAsset = new AutomationVariable(localAsset);
                automationAssets.Add(automationAsset);
            }

            // Compare cloud credentials to local
            foreach (var cloudAsset in cloudCredentials.Credentials)
            {
                var localAsset = localAssets.PSCredentials.Find(asset => asset.Name == cloudAsset.Name);

                var automationAsset = (localAsset != null) ?
                        new AutomationCredential(localAsset, cloudAsset) :
                        new AutomationCredential(cloudAsset);

                automationAssets.Add(automationAsset);
            }

            // Add remaining locally created credentials
            foreach (var localAsset in localAssets.PSCredentials)
            {
                var automationAsset = new AutomationCredential(localAsset);
                automationAssets.Add(automationAsset);
            }

            // Compare cloud connections to local
            foreach (var cloudAsset in cloudConnections.Connection)
            {
                var localAsset = localAssets.Connections.Find(asset => asset.Name == cloudAsset.Name);

                var automationAsset = (localAsset != null) ?
                        new AutomationConnection(localAsset, cloudAsset) :
                        new AutomationConnection(cloudAsset);

                automationAssets.Add(automationAsset);
            }

            // Add remaining locally created connections
            foreach (var localAsset in localAssets.Connections)
            {
                var automationAsset = new AutomationConnection(localAsset);
                automationAssets.Add(automationAsset);
            }

            return automationAssets;
        }
    }
}
