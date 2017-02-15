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
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Automation;
using Microsoft.Azure.Management.Automation.Models;
using System.Management.Automation;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure;
using Microsoft.Azure.Management.Storage.Models;
using System.Security.Cryptography;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;

namespace AutomationISE.Model
{
    public static class AutomationModuleManager
    {
        private static int TIMEOUT_MS = 30000;

        public static async Task<Boolean> CreateStorageAccount(String authority, AutomationManagementClient automationManagementClient, string resourceGroupName, AutomationAccount account, string storageResourceGroup, string storageSubID, string storageAccount, string storageRGLocation)
        {
            try
            {
                // Get the token for the tenant on this subscription.
                var cloudtoken = AuthenticateHelper.RefreshTokenByAuthority(authority);
                var subscriptionCreds = new TokenCloudCredentials(storageSubID, cloudtoken.AccessToken);
                var resourceManagementClient = new ResourceManagementClient(subscriptionCreds);

                // Check if the resource group exists, otherwise create it.
                var rgExists = resourceManagementClient.ResourceGroups.CheckExistence(storageResourceGroup);
                if (!(rgExists.Exists))
                {
                    var resourceGroup = new ResourceGroup { Location = storageRGLocation };
                    await resourceManagementClient.ResourceGroups.CreateOrUpdateAsync(storageResourceGroup, resourceGroup);
                }

                // Create storage client and set subscription to work against
                var token = new Microsoft.Rest.TokenCredentials(cloudtoken.AccessToken);
                var storageManagementClient = new Microsoft.Azure.Management.Storage.StorageManagementClient(token);
                storageManagementClient.SubscriptionId = storageSubID;

                // Use Standard local replication as the sku since it is not critical to keep these modules replicated
                var storageParams = new StorageAccountCreateParameters()
                {
                    Location = storageRGLocation,
                    Kind = Kind.Storage,
                    Sku = new Microsoft.Azure.Management.Storage.Models.Sku(SkuName.StandardLRS)
                };

                // Create storage account
                CancellationToken cancelToken = new CancellationToken();
                await storageManagementClient.StorageAccounts.CreateAsync(storageResourceGroup, storageAccount, storageParams, cancelToken);
            }
            catch (Exception Ex)
            {
                throw Ex;
            }
            return true;
        }

        public static async Task UploadModule(AuthenticationResult auth, AutomationModule module, AutomationManagementClient automationManagementClient, string resourceGroupName, AutomationAccount account, string storageResourceGroup, string storageSubID, string storageAccount)
        {

            // Update the module from powershell gallery if it exists, otherwise upload to storage
            if (!(await UploadFromGalleryIfExists(module.Name, module.localVersion, automationManagementClient, resourceGroupName, account)))
            {
                // Create storage client and set subscription to work against
                var token = new Microsoft.Rest.TokenCredentials(auth.AccessToken);
                var storageManagementClient = new Microsoft.Azure.Management.Storage.StorageManagementClient(token);
                storageManagementClient.SubscriptionId = storageSubID;

                // Get storage key and set up connection to storage account
                var storageKeys = storageManagementClient.StorageAccounts.ListKeys(storageResourceGroup, storageAccount);
                var storageKey = storageKeys.Keys.FirstOrDefault().Value;
                var storageConnection = "DefaultEndpointsProtocol=https;AccountName=" + storageAccount + ";AccountKey=" + storageKey;
                CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(storageConnection);

                // Zip and upload module to storage account
                var zipModule = ZipModuleFolder(module.localModulePath, module.Name);
                var blobClient = cloudStorageAccount.CreateCloudBlobClient();
                await UploadModuleToStorageAccount(blobClient, Constants.moduleContainer, zipModule, module.Name.ToLower());

                // Get sas token and upload to automation account
                var SaSURI = await GetSASToken(blobClient, Constants.moduleContainer, module.Name.ToLower());
                await UploadModuleToAutomationAccount(module.Name, automationManagementClient, resourceGroupName, account, SaSURI);
            }
        }

        private static async Task<Boolean> UploadFromGalleryIfExists(String moduleName, String version, AutomationManagementClient automationManagementClient, string resourceGroupName, AutomationAccount account)
        {
            // Get dependent modules so these can be imported first
            var dependencies =  PowerShellGallery.GetGalleryModuleDependencies(moduleName,version);

            // If there are depdendent modules, recursively call this function until there are no dependencies.
            // This could get into an infinite loop if modules are uploaded that have a dependency on each other which
            // should never happen. Might want to catch this error in the future.
            if (dependencies.Count > 0)
            {
                foreach (var dependentModule in dependencies)
                {
                    await UploadFromGalleryIfExists(dependentModule.moduleName, dependentModule.moduleVersion,automationManagementClient,resourceGroupName,account);
                }
            }

            // Get the gallery location of the module and import to automation account
            var galleryURI = PowerShellGallery.GetGalleryModuleUri(moduleName, version);
            if (galleryURI != null)
            {
                // If the module already exists in the automation account with the same version, skip
                var existingModule = await CheckModuleExists(moduleName, automationManagementClient, resourceGroupName, account.Name, version);
                if (!existingModule)
                {
                    await UploadModuleToAutomationAccount(moduleName, automationManagementClient, resourceGroupName, account, galleryURI);
                }
                return true;
            } 
            return false;
        }

        private static async Task UploadModuleToStorageAccount(CloudBlobClient blobClient, String containerName, String modulePath, String blobName)
        {
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            if (!(await container.ExistsAsync()))
            {
                throw new Exception("Container " + containerName + " does not exist");
            }

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);

            // Read in local module
            var fileStream = File.ReadAllBytes(@modulePath);

            // Check if blob already exists and if it is different then local file, upload.
            if (await blockBlob.ExistsAsync())
            {
                var MD5 = new MD5CryptoServiceProvider();
                var localHash = System.Convert.ToBase64String(MD5.ComputeHash(fileStream));
                var storageHash = blockBlob.Properties.ContentMD5;
                if (localHash != storageHash)
                {
                    await blockBlob.UploadFromByteArrayAsync(fileStream, 0, fileStream.Length);
                }
            }
            else
            {
                // module does not exist in storage, upload
                await blockBlob.UploadFromByteArrayAsync(fileStream, 0, fileStream.Length);
            }
        }

        private static String ZipModuleFolder(String folderPath, String moduleName)
        {
            var zipPath = Path.Combine(Path.GetTempPath(), moduleName.ToLower()) + ".zip";
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            System.IO.Compression.ZipFile.CreateFromDirectory(folderPath, zipPath);
            return zipPath;
        }

        private static async Task<String> GetSASToken(CloudBlobClient blobClient,String containerName, String blobName)
        {
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            if (!(await container.ExistsAsync()))
            {
                throw new Exception("Container " + containerName + " does not exist");
            }

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);
            if (!(await blockBlob.ExistsAsync()))
            {
                throw new Exception("Blob " + blobName + " does not exist");
            }

            // Give access for 24 hours. That would be a large module!
            SharedAccessBlobPolicy modulePolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24),
                Permissions = SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Read
            };

            // Set and return the SaS URI
            var sasBlobtoken = blockBlob.GetSharedAccessSignature(modulePolicy);
            return (blockBlob.Uri + sasBlobtoken);
        }
        private static async Task UploadModuleToAutomationAccount(String moduleName, AutomationManagementClient automationManagementClient, string resourceGroupName, AutomationAccount account,String URIString)
        {

            ModuleCreateOrUpdateParameters draftUpdateParams = new ModuleCreateOrUpdateParameters()
            {
                Name = moduleName,
                Location = account.Location,
                Properties = new ModuleCreateOrUpdateProperties()
                {
                    ContentLink = new Microsoft.Azure.Management.Automation.Models.ContentLink()
                    {
                        Uri = new Uri(URIString)
                    }
                }
            };

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            await automationManagementClient.Modules.CreateOrUpdateAsync(resourceGroupName, account.Name, draftUpdateParams, cts.Token);

            Module module = await GetModule(moduleName, automationManagementClient, resourceGroupName, account.Name);
            // Wait for upload to complete
            while (module.Properties.ProvisioningState != ModuleProvisioningState.Failed && module.Properties.ProvisioningState != ModuleProvisioningState.Succeeded)
            {
                await Task.Delay(10000);
                module = await GetModule(moduleName, automationManagementClient, resourceGroupName, account.Name);
            }

            // Check if there is an error.
            if (!(String.IsNullOrEmpty(module.Properties.Error.Message)))
            { 
                throw new Exception(module.Properties.Error.Message.ToString());
            }
        }

        public static async Task<ISet<AutomationModule>> GetAllModuleMetadata(AutomationManagementClient automationManagementClient, string workspace, string resourceGroupName, string accountName, Dictionary<string,PSObject> localModulesParsed)
        {
            ISet<AutomationModule> result = new SortedSet<AutomationModule>();
            IList<Module> cloudModules = await DownloadModuleMetadata(automationManagementClient, resourceGroupName, accountName);

            /* Start by checking the downloaded modules */
            foreach (Module cloudModule in cloudModules)
            {

                ModuleGetResponse draftResponse;

                try
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    cts.CancelAfter(TIMEOUT_MS);
                    draftResponse = await automationManagementClient.Modules.GetAsync(resourceGroupName, accountName, cloudModule.Name, cts.Token);
                }
                catch
                {
                    draftResponse = null;
                    continue;
                }
                if (localModulesParsed != null && localModulesParsed.ContainsKey(cloudModule.Name))
                {
                    var moduleFileInfo = new FileInfo(localModulesParsed[cloudModule.Name].Properties["Path"].Value.ToString());
                    result.Add(new AutomationModule(localModulesParsed[cloudModule.Name], cloudModule, draftResponse.Module,moduleFileInfo.LastWriteTime));
                    moduleFileInfo = null;
                }
                else
                {
                    result.Add(new AutomationModule(cloudModule, draftResponse.Module));
                }
            }
            /* Now find module on disk that aren't yet accounted for */
            if (localModulesParsed != null)
            {
                foreach (string localModuleName in localModulesParsed.Keys)
                {
                    if (result.FirstOrDefault(x => x.Name == localModuleName) == null)
                    {
                        var moduleFileInfo = new FileInfo(localModulesParsed[localModuleName].Properties["Path"].Value.ToString());
                        result.Add(new AutomationModule(localModulesParsed[localModuleName], moduleFileInfo.LastWriteTime));
                        moduleFileInfo = null;
                    }
                }
            }
            return result;
        }

        public static async Task<Module> GetModule(string moduleName, AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            ModuleGetResponse response = await automationManagementClient.Modules.GetAsync(resourceGroupName, accountName, moduleName, cts.Token);
            return response.Module;
        }
        public static async Task<Boolean> CheckModuleExists(string moduleName, AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName, string version)
        {
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                ModuleGetResponse response = await automationManagementClient.Modules.GetAsync(resourceGroupName, accountName, moduleName, cts.Token);

                // If the cloud module is greater or equal to local module then return false so that it gets imported.
                if ((response.Module.Properties.Version != null  && response.Module.Properties.Version.CompareTo(version) < 0) || !(String.IsNullOrEmpty(response.Module.Properties.Error.Message)))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Module not found.
                if (ex.HResult == -2146233088)
                {
                    return false;
                }
                else throw ex;
            }
            // Module exists and is the same or newer version
            return true;
        }


        private static async Task<IList<Module>> DownloadModuleMetadata(AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            IList<Module> modules = new List<Module>();
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            ModuleListResponse cloudModules = await automationManagementClient.Modules.ListAsync(resourceGroupName, accountName, cts.Token);           
            foreach (var module in cloudModules.Modules)
            {
                // Skip automation internal modules
                if (!(Constants.excludeModules.Contains(module.Name)))
                {
                    modules.Add(module);
                }
            }

            while (cloudModules.NextLink != null)
            {
                cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                cloudModules = await automationManagementClient.Modules.ListNextAsync(cloudModules.NextLink, cts.Token);
                foreach (var module in cloudModules.Modules)
                {
                    // Skip automation internal modules
                    if (!(Constants.excludeModules.Contains(module.Name)))
                    {
                        modules.Add(module);
                    }
                }
            }
            return modules;
        }

    
        public static void DeleteLocalModule(AutomationModule module)
        {
            Directory.Delete(module.localModulePath,true);
        }

        public static async Task DeleteCloudModule(AutomationModule module, AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            await automationManagementClient.Modules.DeleteAsync(resourceGroupName, accountName, module.Name, cts.Token);
        }
    }
}
