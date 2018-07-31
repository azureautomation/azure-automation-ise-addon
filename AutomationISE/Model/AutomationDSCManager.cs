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
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Automation;
using Microsoft.Azure.Management.Automation.Models;
using System.Globalization;
using AutomationISE.Properties;

namespace AutomationISE.Model
{
    public static class AutomationDSCManager
    {
        private static int TIMEOUT_MS = 30000;

        public static async Task UploadConfigurationAsDraft(AutomationDSC configuration, AutomationManagementClient automationManagementClient, string resourceGroupName, AutomationAccount account)
        {
            string fileContent = null;
            try
            {
                if (File.Exists(Path.GetFullPath(configuration.localFileInfo.FullName)))
                {
                    fileContent = System.IO.File.ReadAllText(configuration.localFileInfo.FullName);
                }
            }
            catch (Exception)
            {
                // exception in accessing the file path
                throw new FileNotFoundException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resources.LocalConfigurationFileNotFound));
            }

            DscConfigurationCreateOrUpdateProperties draftProperties;
            draftProperties = new DscConfigurationCreateOrUpdateProperties();
            // Get current properties if is not a new configuration and set these on the draft also so they are preserved.
            DscConfigurationGetResponse response = null;           
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            if (configuration.SyncStatus != AutomationAuthoringItem.Constants.SyncStatus.LocalOnly)
            {
                response = await automationManagementClient.Configurations.GetAsync(resourceGroupName, account.Name, configuration.Name, cts.Token);
            }

            // Create properties
            DscConfigurationCreateOrUpdateParameters draftParams = new DscConfigurationCreateOrUpdateParameters(draftProperties);
            
            draftParams.Name = configuration.Name;
            draftParams.Location = account.Location;
            draftParams.Properties.Description = configuration.Description;

            // If this is not a new configuration, set the existing properties of the configuration
            if (response != null)
            {
                draftParams.Tags = response.Configuration.Tags;
                draftParams.Location = response.Configuration.Location;
                draftParams.Properties.LogVerbose = response.Configuration.Properties.LogVerbose;
                draftParams.Properties.Description = response.Configuration.Properties.Description;
            }
            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            /* Update the configuration content from .ps1 file */
            DscConfigurationCreateOrUpdateParameters draftUpdateParams = new DscConfigurationCreateOrUpdateParameters()
            {
                Name = configuration.Name,
                Location = draftParams.Location,
                Tags = draftParams.Tags,
                Properties = new DscConfigurationCreateOrUpdateProperties()
                {
                    Description = draftParams.Properties.Description,
                    LogVerbose = draftParams.Properties.LogVerbose,
                    Source = new Microsoft.Azure.Management.Automation.Models.ContentSource()
                    {
                        ContentType = ContentSourceType.EmbeddedContent,
                                Value = fileContent
                    }
                }
            };

            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            await automationManagementClient.Configurations.CreateOrUpdateAsync(resourceGroupName, account.Name, draftUpdateParams, cts.Token);
            /* Ensure the correct sync status is detected */
            DscConfiguration draft = await GetConfigurationDraft(configuration.Name, automationManagementClient, resourceGroupName, account.Name);
            configuration.localFileInfo.LastWriteTime = draft.Properties.LastModifiedTime.LocalDateTime;
            configuration.LastModifiedLocal = draft.Properties.LastModifiedTime.LocalDateTime;
            configuration.LastModifiedCloud = draft.Properties.LastModifiedTime.LocalDateTime;
        }

        public static async Task DownloadConfiguration(AutomationDSC configuration, AutomationManagementClient automationManagementClient, string workspace, string resourceGroupName, AutomationAccount account)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            DscConfigurationGetResponse response = await automationManagementClient.Configurations.GetAsync(resourceGroupName, account.Name, configuration.Name, cts.Token);
            DscConfigurationGetResponse draftResponse = null;
            DscConfigurationGetContentResponse configurationContentResponse = null;
            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            if (response.Configuration.Properties.State == "Published")
            {
                configurationContentResponse = await automationManagementClient.Configurations.GetContentAsync(resourceGroupName, account.Name, configuration.Name, cts.Token);
            }
            else
            {
                // Draft not supported yet
            }
            String configFilePath = System.IO.Path.Combine(workspace, configuration.Name + ".ps1");
            try
            {
                File.WriteAllText(configFilePath, configurationContentResponse.Content.ToString(), Encoding.UTF8);
            }
            catch (Exception Ex)
            {
                // Atempting to write the file while it is being read. Wait a second and retry.
                if (Ex.HResult == -2147024864)
                {
                    Thread.Sleep(1000);
                    File.WriteAllText(configFilePath, configurationContentResponse.Content.ToString(), Encoding.UTF8);
                }
            }
            configuration.localFileInfo = new FileInfo(configFilePath);

            if (response.Configuration.Properties.State == "Published")
            {
                await UploadConfigurationAsDraft(configuration, automationManagementClient, resourceGroupName, account);
                cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                draftResponse = await automationManagementClient.Configurations.GetAsync(resourceGroupName, account.Name, configuration.Name, cts.Token);
            }
            /* Ensures the correct sync status is detected */
            if (draftResponse != null)
            {
                configuration.localFileInfo.LastWriteTime = draftResponse.Configuration.Properties.LastModifiedTime.LocalDateTime;
                configuration.LastModifiedLocal = draftResponse.Configuration.Properties.LastModifiedTime.LocalDateTime;
                configuration.LastModifiedCloud = draftResponse.Configuration.Properties.LastModifiedTime.LocalDateTime;
            }
        }

        public static async Task<ISet<AutomationDSC>> GetAllConfigurationMetadata(AutomationManagementClient automationManagementClient, string workspace, string resourceGroupName, string accountName, Dictionary<string,string> localScriptsParsed)
        {
            ISet<AutomationDSC> result = new SortedSet<AutomationDSC>();
            IList<DscConfiguration> cloudConfigurations = await DownloadConfigurationMetadata(automationManagementClient, resourceGroupName, accountName);


            Dictionary<string, string> filePathForConfiguration = new Dictionary<string, string>();
            if (localScriptsParsed != null)
            {
                foreach (string path in localScriptsParsed.Keys)
                {
                    if (localScriptsParsed[path] == "configuration")
                    {
                        /*
                        Possible that the ps1 file name is not the same as the configuration name
                        but this is not supported in the service any more. Have some parsing here if required later...

                        int configurationNameLocation = ASTScript.EndBlock.Extent.Text.ToLower().Substring("configuration".Length + 1).IndexOf("{");
                        if (configurationNameLocation > 0)
                        {
                            string configurationName = ASTScript.EndBlock.Extent.Text.Substring("Configuration".Length + 1, configurationNameLocation);
                            if (!(filePathForConfiguration.ContainsKey(configurationName.Trim())))
                            {
                                filePathForConfiguration.Add(configurationName.Trim(), path);
                            }
                        }
                        */
                        filePathForConfiguration.Add(System.IO.Path.GetFileNameWithoutExtension(path), path);
                    }
                }
            }
            /* Start by checking the downloaded configurations */
            foreach (DscConfiguration cloudConfiguration in cloudConfigurations)
            {

                DscConfigurationGetResponse draftResponse;

                try
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    cts.CancelAfter(TIMEOUT_MS);
                    draftResponse = await automationManagementClient.Configurations.GetAsync(resourceGroupName, accountName, cloudConfiguration.Name, cts.Token);
                }
                catch
                {
                    draftResponse = null;
                    continue;
                }
                if (filePathForConfiguration.ContainsKey(cloudConfiguration.Name))
                {
                    result.Add(new AutomationDSC(new FileInfo(filePathForConfiguration[cloudConfiguration.Name]), cloudConfiguration, draftResponse.Configuration));
                }
                else
                {
                    result.Add(new AutomationDSC(cloudConfiguration, draftResponse.Configuration));
                }
            }
            /* Now find configurations on disk that aren't yet accounted for */
            foreach (string localConfigurationName in filePathForConfiguration.Keys)
            {
                if (result.FirstOrDefault(x => x.Name == localConfigurationName) == null)
                {
                    result.Add(new AutomationDSC(new FileInfo(filePathForConfiguration[localConfigurationName])));
                }
            }
        return result;
        }
        
        public static async Task<DscConfiguration> GetConfigurationDraft(string configurationName, AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            DscConfigurationGetResponse response = await automationManagementClient.Configurations.GetAsync(resourceGroupName, accountName, configurationName, cts.Token);
            return response.Configuration;
        }

        private static async Task<IList<DscConfiguration>> DownloadConfigurationMetadata(AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            IList<DscConfiguration> configurations = new List<DscConfiguration>();
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            DscConfigurationListResponse cloudConfiguraitons = await automationManagementClient.Configurations.ListAsync(resourceGroupName, accountName, cts.Token);           
            foreach (var configuration in cloudConfiguraitons.Configurations)
            {
                configurations.Add(configuration);
            }

            while (cloudConfiguraitons.NextLink != null)
            {
                cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                cloudConfiguraitons = await automationManagementClient.Configurations.ListNextAsync(cloudConfiguraitons.NextLink, cts.Token);
                foreach (var configuration in cloudConfiguraitons.Configurations)
                {
                    configurations.Add(configuration);
                }
            }
            return configurations;
        }

        public static void CreateLocalConfiguration(string configurationName, string workspace)
        {
            String configurationFilePath = System.IO.Path.Combine(workspace, configurationName + ".ps1");
            if (File.Exists(configurationFilePath))
                throw new Exception("A script with that name already exists");

            // Create the file with a UTF8 Byte Order Mark
            using (FileStream stream = new FileStream(configurationFilePath, FileMode.Create))
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    writer.Write(Encoding.UTF8.GetPreamble());
                }
            }

            try
            {
                File.WriteAllText(configurationFilePath, "Configuration " + configurationName + "\r\n{\r\n}", Encoding.UTF8);
            }
            catch (Exception Ex)
            {
                // Atempting to write the file while it is being read. Wait a second and retry.
                if (Ex.HResult == -2147024864)
                {
                    Thread.Sleep(1000);
                    File.WriteAllText(configurationFilePath, "Configuration " + configurationName + "\r\n{\r\n}", Encoding.UTF8);
                }
            }

        }

        public static void CreateLocalConfigurationData(string configurationName, string workspace)
        {
            String configurationFilePath = System.IO.Path.Combine(workspace, configurationName + ".ps1");
            if (File.Exists(configurationFilePath))
                throw new Exception("A configuration with that name already exists");

            // Create the file with a UTF8 Byte Order Mark
            using (FileStream stream = new FileStream(configurationFilePath, FileMode.Create))
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    writer.Write(Encoding.UTF8.GetPreamble());
                }
            }
            try
            {
                File.WriteAllText(configurationFilePath, "@{\r\n\tAllNodes = @(\r\n\t\t@{\r\n\t\t\tNodeName = '*'\r\r\n\t\t}\r\n\t)\r\n}", Encoding.UTF8);
            }
            catch (Exception Ex)
            {
                // Atempting to write the file while it is being read. Wait a second and retry.
                if (Ex.HResult == -2147024864)
                {
                    Thread.Sleep(1000);
                    File.WriteAllText(configurationFilePath, "@{\r\n\tAllNodes = @(\r\n\t\t@{\r\n\t\t\tNodeName = '*'\r\r\n\t\t}\r\n\t)\r\n}", Encoding.UTF8);
                }
            }
        }

        public static void DeleteLocalConfiguration(AutomationDSC configuration)
        {
            File.Delete(configuration.localFileInfo.FullName);
        }

        public static async Task DeleteCloudConfiguration(AutomationDSC configuration, AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            await automationManagementClient.Configurations.DeleteAsync(resourceGroupName, accountName, configuration.Name, cts.Token);
        }
    }
}
