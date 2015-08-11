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
using System.IO;
using Microsoft.Azure.Management.Automation;
using System.Web.Script.Serialization;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using System.Security.Cryptography;

namespace AutomationISE.Model
{
    public class LocalAssetsStore
    {
        private static AssetJson FindMatchingLocalAsset(LocalAssets localAssets, AutomationAsset asset)
        {
            List<AssetJson> assetJsonList = new List<AssetJson>();
            
            if (asset is AutomationVariable)
            {
                assetJsonList.AddRange(localAssets.Variables);
            }
            else if (asset is AutomationCredential)
            {
                assetJsonList.AddRange(localAssets.PSCredentials);
            }
            else if (asset is AutomationConnection)
            {
                assetJsonList.AddRange(localAssets.Connections);
            }

            foreach (var currentLocalAsset in assetJsonList)
            {
                if (asset.Name == currentLocalAsset.Name)
                {
                    return currentLocalAsset;
                }
            }

            return null;
        }

        // updates the local assets, either removing (if replace = false) or adding/replacing (if replace = true) the specified assets
        private static void Set(String workspacePath, ICollection<AutomationAsset> assetsToAffect, bool replace, String encryptionCertThumbprint)
        {
            LocalAssets localAssets = LocalAssetsStore.Get(workspacePath, encryptionCertThumbprint);

            foreach (var assetToAffect in assetsToAffect)
            {
                AssetJson assetToDelete = LocalAssetsStore.FindMatchingLocalAsset(localAssets, assetToAffect);

                if (assetToAffect is AutomationVariable)
                {
                    if (assetToDelete != null)
                    {
                        localAssets.Variables.Remove((VariableJson)assetToDelete);
                    }

                    if (replace)
                    {
                        localAssets.Variables.Add(new VariableJson((AutomationVariable)assetToAffect));
                    }
                }

                else if (assetToAffect is AutomationCredential)
                {
                    if (assetToDelete != null)
                    {
                        localAssets.PSCredentials.Remove((CredentialJson)assetToDelete);
                    }

                    if (replace)
                    {
                        localAssets.PSCredentials.Add(new CredentialJson((AutomationCredential)assetToAffect));
                    }
                }

                else if (assetToAffect is AutomationConnection)
                {
                    if (assetToDelete != null)
                    {
                        localAssets.Connections.Remove((ConnectionJson)assetToDelete);
                    }

                    if (replace)
                    {
                        localAssets.Connections.Add(new ConnectionJson((AutomationConnection)assetToAffect));
                    }
                }
            }

            DirectoryInfo dir = Directory.CreateDirectory(workspacePath);
            UnsecureLocalAssetsContainerJson.Set(workspacePath, localAssets);
            SecureLocalAssetsContainerJson.Set(workspacePath, localAssets, encryptionCertThumbprint); 
        }

        public static void Add(String workspacePath, ICollection<AutomationAsset> newAssets, String encryptionCertThumbprint)
        {
            LocalAssetsStore.Set(workspacePath, newAssets, true, encryptionCertThumbprint);
        }

        public static void Delete(String workspacePath, ICollection<AutomationAsset> assetsToDelete, String encryptionCertThumbprint)
        {
            LocalAssetsStore.Set(workspacePath, assetsToDelete, false, encryptionCertThumbprint);
        }

        public static LocalAssets Get(String workspacePath, String encryptionCertThumbprint)
        {
            LocalAssets localAssetsContainer = new LocalAssets(); 
            
            UnsecureLocalAssetsContainerJson localAssetsJson = UnsecureLocalAssetsContainerJson.Get(workspacePath);
            SecureLocalAssetsContainerJson secureLocalAssetsJson = SecureLocalAssetsContainerJson.Get(workspacePath, encryptionCertThumbprint);
            
            // add JSON variables to the container
            localAssetsJson.Variable.ForEach(variable => variable.Encrypted = false);
            localAssetsContainer.Variables.AddRange(localAssetsJson.Variable);

            secureLocalAssetsJson.Variable.ForEach(variable => variable.Encrypted = true);
            localAssetsContainer.Variables.AddRange(secureLocalAssetsJson.Variable);

            // add JSON credentials to the container
            localAssetsContainer.PSCredentials.AddRange(secureLocalAssetsJson.PSCredential);

            // add JSON connections to the container
            localAssetsContainer.Connections.AddRange(secureLocalAssetsJson.Connection);

            return localAssetsContainer;
        }

        private abstract class AbstractLocalAssetsContainerJson
        {
            public List<VariableJson> Variable = new List<VariableJson>();
            public static JavaScriptSerializer jss = new JavaScriptSerializer();

            public static void WriteJson(string jsonFilePath, Object assets)
            {
                var assetsSerialized = JsonConvert.SerializeObject(assets, Formatting.Indented);
                File.WriteAllText(jsonFilePath, assetsSerialized);
            }
        }

        private class UnsecureLocalAssetsContainerJson
            : AbstractLocalAssetsContainerJson
        {
            public static UnsecureLocalAssetsContainerJson Get(string workspacePath)
            {
                try
                {
                    string localAssetsFilePath = System.IO.Path.Combine(workspacePath, AutomationISE.Model.Constants.localAssetsFileName); 
                    return jss.Deserialize<UnsecureLocalAssetsContainerJson>(File.ReadAllText(localAssetsFilePath));
                }
                catch
                {
                    return new UnsecureLocalAssetsContainerJson();
                }
            }

            public static void Set(string workspacePath, LocalAssets localAssets)
            {
                var localAssetsUnsecure = new UnsecureLocalAssetsContainerJson();
                foreach (var localVariableAsset in localAssets.Variables)
                {
                    if (!localVariableAsset.Encrypted)
                    {
                        localAssetsUnsecure.Variable.Add(localVariableAsset);
                    }
                }

                WriteJson(System.IO.Path.Combine(workspacePath, AutomationISE.Model.Constants.localAssetsFileName), localAssetsUnsecure);
            }
           
            //public List<CertificateJson> Certificate = new List<CertificateJson>();
        }

        private class SecureLocalAssetsContainerJson
            : AbstractLocalAssetsContainerJson
        {
            public static String Encrypt(Object Value, String Thumbprint)
            {
                if (Value == null)
                {
                    return null;
                }
                else
                {
                    X509Store CertStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    try
                    {
                        CertStore.Open(OpenFlags.ReadOnly);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error reading certificate store", ex);
                    }

                    var CertCollection = CertStore.Certificates;
                    var EncryptCert = CertCollection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
                    CertStore.Close();

                    if (EncryptCert.Count == 0)
                    {
                        throw new Exception("Certificate:" + Thumbprint + " does not exist in HKLM\\Root");
                    }

                    RSACryptoServiceProvider rsaEncryptor = (RSACryptoServiceProvider)EncryptCert[0].PublicKey.Key;
                    var valueJson = JsonConvert.SerializeObject(Value);
                    var EncryptedBytes = System.Text.Encoding.Default.GetBytes(valueJson);
                    byte[] EncryptedData = rsaEncryptor.Encrypt(EncryptedBytes, true);
                    return Convert.ToBase64String(EncryptedData);
                }
            }

            public static Object Decrypt(Object EncryptedValue, String Thumbprint)
            {
                if (EncryptedValue == null)
                {
                    return null;
                }
                else if (!(EncryptedValue is string))
                {
                    throw new Exception("Cannot decrypt value. Value to decrypt was not a string.");
                }
                else
                {
                    X509Store CertStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    try
                    {
                        CertStore.Open(OpenFlags.ReadOnly);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error reading certificate store", ex);
                    }

                    var CertCollection = CertStore.Certificates;
                    var EncryptCert = CertCollection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
                    CertStore.Close();

                    if (EncryptCert.Count == 0)
                    {
                        throw new Exception("Certificate:" + Thumbprint + " does not exist in HKLM\\My");
                    }

                    Byte[] EncryptedString = Convert.FromBase64String((string)EncryptedValue);
                    RSACryptoServiceProvider rsaEncryptor = (RSACryptoServiceProvider)EncryptCert[0].PrivateKey;
                    byte[] EncryptedData = rsaEncryptor.Decrypt(EncryptedString, true);
                    var valueJson = System.Text.Encoding.Default.GetString(EncryptedData);
                    return JsonConvert.DeserializeObject(valueJson);
                }
            }
            
            public static SecureLocalAssetsContainerJson Get(string workspacePath, String encryptionCertThumbprint)
            {
                try
                {
                    string secureLocalAssetsFilePath = System.IO.Path.Combine(workspacePath, AutomationISE.Model.Constants.secureLocalAssetsFileName);
                    var localAssetsSecure = jss.Deserialize<SecureLocalAssetsContainerJson>(File.ReadAllText(secureLocalAssetsFilePath));

                    if (encryptionCertThumbprint != null)
                    {
                        foreach (var localVariableAsset in localAssetsSecure.Variable)
                        {
                            localVariableAsset.Value = Decrypt(localVariableAsset.Value, encryptionCertThumbprint);
                        }

                        foreach (var localCredAsset in localAssetsSecure.PSCredential)
                        {
                            var decryptedValue = Decrypt(localCredAsset.Password, encryptionCertThumbprint);

                            if(decryptedValue == null)
                            {
                                localCredAsset.Password = null;
                            }
                            else
                            {
                                localCredAsset.Password = (string)decryptedValue;
                            }
                        }
                    }

                    return localAssetsSecure;
                }
                catch
                {
                    return new SecureLocalAssetsContainerJson();
                }
            }

            public static void Set(string workspacePath, LocalAssets localAssets, String encryptionCertThumbprint)
            {
                var localAssetsSecure = new SecureLocalAssetsContainerJson();
                foreach (var localVariableAsset in localAssets.Variables)
                {
                    if (localVariableAsset.Encrypted)
                    {
                        localAssetsSecure.Variable.Add(localVariableAsset);
                    }
                }

                localAssetsSecure.PSCredential.AddRange(localAssets.PSCredentials);
                localAssetsSecure.Connection.AddRange(localAssets.Connections);

                if (encryptionCertThumbprint != null)
                {
                    foreach (var localVariableAsset in localAssetsSecure.Variable)
                    {
                        localVariableAsset.Value = Encrypt(localVariableAsset.Value, encryptionCertThumbprint);
                    }

                    foreach (var localCredAsset in localAssetsSecure.PSCredential)
                    {
                        localCredAsset.Password = Encrypt(localCredAsset.Password, encryptionCertThumbprint);
                    }
                }

                WriteJson(System.IO.Path.Combine(workspacePath, AutomationISE.Model.Constants.secureLocalAssetsFileName), localAssetsSecure); 
            }

            public List<CredentialJson> PSCredential = new List<CredentialJson>();
            public List<ConnectionJson> Connection = new List<ConnectionJson>();
        }

    }
}
