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
using System.Web.Script.Serialization;
using System.Text;

namespace AutomationISE.Model
{
    public class AutomationAssetManager 
    {
        public static async Task DownloadAllFromCloud(String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName)
        {
            var assets = await AutomationAssetManager.GetAll(null, automationApi, resourceGroupName, automationAccountName);
            AutomationAssetManager.SaveLocally(localWorkspacePath, assets);
        }

        public static async void DownloadFromCloud(ICollection<AutomationAsset> assetsToDownload, String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName)
        {
            var cloudAssets = await AutomationAssetManager.GetAll(null, automationApi, resourceGroupName, automationAccountName);
            var assetsToSaveLocally = new SortedSet<AutomationAsset>();

            foreach (var cloudAsset in cloudAssets)
            {
                foreach (var assetToDownload in assetsToDownload)
                {
                    if (cloudAsset.Equals(assetToDownload))
                    {
                        assetsToSaveLocally.Add(cloudAsset);
                        break;
                    }
                }
            }

            AutomationAssetManager.SaveLocally(localWorkspacePath, assetsToSaveLocally);
        }

        public static async Task UploadToCloud(ICollection<AutomationAsset> assetsToUpload, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName)
        {
            var jss = new JavaScriptSerializer();
            
            foreach (var assetToUpload in assetsToUpload)
            {
                if (assetToUpload is AutomationVariable)
                {
                    var asset = (AutomationVariable)assetToUpload;

                    var properties = new VariableCreateOrUpdateProperties();
                    properties.IsEncrypted = asset.Encrypted;

                    var stringBuilder = new StringBuilder();
                    jss.Serialize(asset.getValue(), stringBuilder);
                    properties.Value = stringBuilder.ToString();
                    
                    await automationApi.Variables.CreateOrUpdateAsync(resourceGroupName, automationAccountName, new VariableCreateOrUpdateParameters(asset.Name, properties));
                }
                else if(assetToUpload is AutomationCredential)
                {
                    var asset = (AutomationCredential)assetToUpload;

                    var properties = new CredentialCreateOrUpdateProperties();
                    properties.UserName = asset.getUsername();
                    properties.Password = asset.getPassword();

                    await automationApi.PsCredentials.CreateOrUpdateAsync(resourceGroupName, automationAccountName, new CredentialCreateOrUpdateParameters(asset.Name, properties));
                }
                else if (assetToUpload is AutomationConnection)
                {
                    // TODO: implement this and certificates
                }
            }
        }

        public static void SaveLocally(String localWorkspacePath, ICollection<AutomationAsset> assets)
        {
            LocalAssetsStore.Add(localWorkspacePath, assets);
        }

        public static void Delete(ICollection<AutomationAsset> assetsToDelete, String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName, bool deleteLocally, bool deleteFromCloud)
        {
            if (deleteLocally)
            {
                LocalAssetsStore.Delete(localWorkspacePath, assetsToDelete);
            }

            if (deleteFromCloud)
            {
                foreach (var assetToDelete in assetsToDelete)
                {
                    if (assetToDelete.LastModifiedCloud == null)
                    {
                        // asset is local only, no need to delete it from cloud
                        continue;
                    }

                    if (assetToDelete is AutomationVariable)
                    {
                        automationApi.Variables.Delete(resourceGroupName, automationAccountName, assetToDelete.Name);
                    }
                    else if (assetToDelete is AutomationCredential)
                    {
                        automationApi.PsCredentials.Delete(resourceGroupName, automationAccountName, assetToDelete.Name);
                    }
                }
            }
        }

        public static async Task<ISet<AutomationAsset>> GetAll(String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName)
        {
            VariableListResponse cloudVariables = await automationApi.Variables.ListAsync(resourceGroupName, automationAccountName);
            CredentialListResponse cloudCredentials = await automationApi.PsCredentials.ListAsync(resourceGroupName, automationAccountName);

            // TODO: need to get one at a time to get values. values currently comes back as empty
            //ConnectionListResponse cloudConnections = await automationApi.Connections.ListAsync(resourceGroupName, automationAccountName);

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
            /*foreach (var cloudAsset in cloudConnections.Connection)
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
            }*/

            return automationAssets;
        }
    }
}
