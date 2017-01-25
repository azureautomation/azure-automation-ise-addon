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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Automation;
using Microsoft.Azure.Management.Automation.Models;
using System.Web.Script.Serialization;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Windows;

namespace AutomationISE.Model
{
    public class AutomationAssetManager
    {
        private static int TIMEOUT_MS = 30000;

        public static async Task DownloadAllFromCloud(String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName, String encryptionCertThumbprint, ICollection<ConnectionType> connectionTypes)
        {
            var assets = await AutomationAssetManager.GetAll(null, automationApi, resourceGroupName, automationAccountName, encryptionCertThumbprint, connectionTypes);
            AutomationAssetManager.SaveLocally(localWorkspacePath, assets, encryptionCertThumbprint, connectionTypes);
        }

        public static async void DownloadFromCloud(ICollection<AutomationAsset> assetsToDownload, String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName, String encryptionCertThumbprint, ICollection<ConnectionType> connectionTypes)
        {
            try
            {
                var cloudAssets = await AutomationAssetManager.GetAll(null, automationApi, resourceGroupName, automationAccountName, encryptionCertThumbprint, connectionTypes);
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

                AutomationAssetManager.SaveLocally(localWorkspacePath, assetsToSaveLocally, encryptionCertThumbprint, connectionTypes);
            }
            catch (Exception exception)
            {
                System.Windows.Forms.MessageBox.Show(exception.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        public static async Task UploadToCloud(ICollection<AutomationAsset> assetsToUpload, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName)
        {
            var jss = new JavaScriptSerializer();

            foreach (var assetToUpload in assetsToUpload)
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                if (assetToUpload is AutomationVariable)
                {
                    var asset = (AutomationVariable)assetToUpload;

                    var properties = new VariableCreateOrUpdateProperties();
                    properties.IsEncrypted = asset.Encrypted;

                    var stringBuilder = new StringBuilder();
                    jss.Serialize(asset.getValue(), stringBuilder);
                    properties.Value = stringBuilder.ToString();

                    await automationApi.Variables.CreateOrUpdateAsync(resourceGroupName, automationAccountName, new VariableCreateOrUpdateParameters(asset.Name, properties), cts.Token);
                }
                else if(assetToUpload is AutomationCredential)
                {
                    var asset = (AutomationCredential)assetToUpload;

                    var properties = new CredentialCreateOrUpdateProperties();
                    properties.UserName = asset.getUsername();
                    properties.Password = asset.getPassword();

                    await automationApi.PsCredentials.CreateOrUpdateAsync(resourceGroupName, automationAccountName, new CredentialCreateOrUpdateParameters(asset.Name, properties), cts.Token);
                }
                else if (assetToUpload is AutomationConnection)
                {
                    var asset = (AutomationConnection)assetToUpload;

                    var properties = new ConnectionCreateOrUpdateProperties();
                    var connectionFieldsAsJson = new Dictionary<string, string>();

                    foreach(KeyValuePair<string, object> field in asset.getFields())
                    {
                        connectionFieldsAsJson.Add(field.Key, field.Value.ToString());
                    }

                    properties.FieldDefinitionValues = connectionFieldsAsJson;
                    properties.ConnectionType = new ConnectionTypeAssociationProperty();
                    properties.ConnectionType.Name = asset.ConnectionType;

                    await automationApi.Connections.CreateOrUpdateAsync(resourceGroupName, automationAccountName, new ConnectionCreateOrUpdateParameters(asset.Name, properties), cts.Token);
                }
                else if (assetToUpload is AutomationCertificate)
                {
                    var asset = (AutomationCertificate)assetToUpload;

                    var cert = (asset.getPassword() == null)
                                ? new X509Certificate2(asset.getCertPath(), String.Empty,
                                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet |
                                    X509KeyStorageFlags.MachineKeySet)
                                : new X509Certificate2(asset.getCertPath(), asset.getPassword(),
                                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet |
                                    X509KeyStorageFlags.MachineKeySet);

                    var properties = new CertificateCreateOrUpdateProperties()
                    {
                        Base64Value = Convert.ToBase64String(cert.Export(X509ContentType.Pkcs12)),
                        Thumbprint = cert.Thumbprint,
                        IsExportable = asset.getExportable()
                    };

                    await automationApi.Certificates.CreateOrUpdateAsync(resourceGroupName, automationAccountName, new CertificateCreateOrUpdateParameters(asset.Name, properties), cts.Token);
                }
            }
        }

        public static void SaveLocally(String localWorkspacePath, ICollection<AutomationAsset> assets, String encryptionCertThumbprint, ICollection<ConnectionType> connectionTypes)
        {
            LocalAssetsStore.Add(localWorkspacePath, assets, encryptionCertThumbprint, connectionTypes);
        }

        public static void Delete(ICollection<AutomationAsset> assetsToDelete, String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName, bool deleteLocally, bool deleteFromCloud, String encryptionCertThumbprint, ICollection<ConnectionType> connectionTypes)
        {
            if (deleteLocally)
            {
                LocalAssetsStore.Delete(localWorkspacePath, assetsToDelete, encryptionCertThumbprint, connectionTypes);
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
                    else if (assetToDelete is AutomationCertificate)
                    {
                        automationApi.Certificates.Delete(resourceGroupName, automationAccountName, assetToDelete.Name);
                    }
                    else if (assetToDelete is AutomationConnection)
                    {
                        automationApi.Connections.Delete(resourceGroupName, automationAccountName, assetToDelete.Name);
                    }
                }
            }
        }

        public static async Task<ISet<AutomationAsset>> GetAll(String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName, string encryptionCertThumbprint, ICollection<ConnectionType> connectionTypes)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            VariableListResponse cloudVariables = await automationApi.Variables.ListAsync(resourceGroupName, automationAccountName, cts.Token);
            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            CredentialListResponse cloudCredentials = await automationApi.PsCredentials.ListAsync(resourceGroupName, automationAccountName, cts.Token);
            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            ConnectionListResponse cloudConnections = await automationApi.Connections.ListAsync(resourceGroupName, automationAccountName, cts.Token);

            CertificateListResponse cloudCertificates = await automationApi.Certificates.ListAsync(resourceGroupName, automationAccountName, cts.Token);

            // need to get connections one at a time to get each connection's values. Values currently come back as empty in list call
            var connectionAssetsWithValues = new HashSet<Connection>();
            foreach (var connection in cloudConnections.Connection)
            {
                cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                var connectionResponse = await automationApi.Connections.GetAsync(resourceGroupName, automationAccountName, connection.Name, cts.Token);
                connectionAssetsWithValues.Add(connectionResponse.Connection);
            }

            LocalAssets localAssets = LocalAssetsStore.Get(localWorkspacePath, encryptionCertThumbprint, connectionTypes);

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
            foreach (var cloudAsset in connectionAssetsWithValues)
            {
                ConnectionTypeGetResponse connectionType = await automationApi.ConnectionTypes.GetAsync(resourceGroupName, automationAccountName, cloudAsset.Properties.ConnectionType.Name); 
                var localAsset = localAssets.Connections.Find(asset => asset.Name == cloudAsset.Name);

                var automationAsset = (localAsset != null) ?
                        new AutomationConnection(localAsset, cloudAsset) :
                        new AutomationConnection(cloudAsset, connectionType.ConnectionType);

                automationAssets.Add(automationAsset);
            }

            // Add remaining locally created connections
            foreach (var localAsset in localAssets.Connections)
            {
                var automationAsset = new AutomationConnection(localAsset);
                automationAssets.Add(automationAsset);
            }

            // Compare cloud certificates to local
            foreach (var cloudAsset in cloudCertificates.Certificates)
            {
                var localAsset = localAssets.Certificate.Find(asset => asset.Name == cloudAsset.Name);

                var automationAsset = (localAsset != null) ?
                        new AutomationCertificate(localAsset, cloudAsset) :
                        new AutomationCertificate(cloudAsset);

                automationAssets.Add(automationAsset);
            }

            // Add remaining locally created certificates
            foreach (var localAsset in localAssets.Certificate)
            {
                var automationAsset = new AutomationCertificate(localAsset);
                automationAssets.Add(automationAsset);
            }

            return automationAssets;
        }

        /// <summary>
        /// Returns if the asset exists locally or in the cloud
        /// </summary>
        /// <param name="assetName"></param>
        /// <param name="assetType"></param>
        /// <param name="localWorkspacePath"></param>
        /// <param name="automationApi"></param>
        /// <param name="resourceGroupName"></param>
        /// <param name="automationAccountName"></param>
        /// <param name="encryptionCertThumbprint"></param>
        /// <returns>null if the asset does not exist or else returns the asset</returns>
        public static async Task<AutomationAsset> GetAsset(String assetName, String assetType, String localWorkspacePath, AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName, string encryptionCertThumbprint, ICollection<ConnectionType> connectionTypes)
        {
            AutomationAsset automationAsset = null;

            // Get local assets
            LocalAssets localAssets = LocalAssetsStore.Get(localWorkspacePath, encryptionCertThumbprint, connectionTypes);

            // Search for variables
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            if (assetType == Constants.AssetType.Variable)
            {
                // Check local asset store first
                var localVariable = localAssets.Variables.Find(asset => asset.Name == assetName);
                if (localVariable != null)
                {
                    automationAsset = new AutomationVariable(localVariable);
                }
                else
                {
                    try
                    {
                        // Check cloud. Catch exception if it doesn't exist
                        VariableGetResponse cloudVariable = await automationApi.Variables.GetAsync(resourceGroupName, automationAccountName, assetName, cts.Token);
                        automationAsset = new AutomationVariable(cloudVariable.Variable);
                    }
                    catch (Exception e)
                    {
                        // If the exception is not found, don't throw new exception as this is expected
                        if (e.HResult != -2146233088) throw e;
                    }
                }
            }
            // Search for credentials
            else if (assetType == Constants.AssetType.Credential)
            {
                // Check local asset store first
                var localCredential = localAssets.PSCredentials.Find(asset => asset.Name == assetName);
                if (localCredential != null)
                {
                    automationAsset = new AutomationCredential(localCredential);
                }
                else
                {
                    try
                    {
                        // Check cloud. Catch execption if it doesn't exist
                        CredentialGetResponse cloudVariable = await automationApi.PsCredentials.GetAsync(resourceGroupName, automationAccountName, assetName, cts.Token);
                        automationAsset = new AutomationCredential(cloudVariable.Credential);
                    }
                    catch (Exception e)
                    {
                        // If the exception is not found, don't throw new exception as this is expected
                        if (e.HResult != -2146233088) throw e;
                    }
                }
            }
            // Search for connections
            else if (assetType == Constants.AssetType.Connection)
            {
                // Check local asset store first
                var localConnection = localAssets.Connections.Find(asset => asset.Name == assetName);
                if (localConnection != null)
                {
                    automationAsset = new AutomationConnection(localConnection);
                }
                else
                {
                    try
                    {
                        // Check cloud. Catch exception if it doesn't exist
                        ConnectionGetResponse cloudConnection = await automationApi.Connections.GetAsync(resourceGroupName, automationAccountName, assetName, cts.Token);
                        cts = new CancellationTokenSource();
                        cts.CancelAfter(TIMEOUT_MS);
                        ConnectionTypeGetResponse connectionType =  await automationApi.ConnectionTypes.GetAsync(resourceGroupName, automationAccountName, 
                            cloudConnection.Connection.Properties.ConnectionType.Name, cts.Token);
                        automationAsset = new AutomationConnection(cloudConnection.Connection, connectionType.ConnectionType);
                    }
                    catch (Exception e)
                    {
                        // If the exception is not found, don't throw new exception as this is expected
                        if (e.HResult != -2146233088) throw e;
                    }
                }
            }
            // Search for certificates
            else if (assetType == Constants.AssetType.Certificate)
            {
                // Check local asset store first
                var localCertificate = localAssets.Certificate.Find(asset => asset.Name == assetName);
                if (localCertificate != null)
                {
                    automationAsset = new AutomationCertificate(localCertificate);
                }
                else
                {
                    try
                    {
                        // Check cloud. Catch execption if it doesn't exist
                        CertificateGetResponse cloudCertificate = await automationApi.Certificates.GetAsync(resourceGroupName, automationAccountName, assetName, cts.Token);
                        automationAsset = new AutomationCertificate(cloudCertificate.Certificate);
                    }
                    catch (Exception e)
                    {
                        // If the exception is not found, don't throw new exception as this is expected
                        if (e.HResult != -2146233088) throw e;
                    }
                }
            }
            return automationAsset;
        }

        public static async Task<ISet<ConnectionType>> GetConnectionTypes(AutomationManagementClient automationApi, string resourceGroupName, string automationAccountName)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            var connectionTypeListResponse = await automationApi.ConnectionTypes.ListAsync(resourceGroupName, automationAccountName, cts.Token);
            var connectionTypes = new HashSet<ConnectionType>();

            foreach (var connectionType in connectionTypeListResponse.ConnectionTypes)
            {
                connectionTypes.Add(connectionType);
            }

            return connectionTypes;
        }
    }
}
