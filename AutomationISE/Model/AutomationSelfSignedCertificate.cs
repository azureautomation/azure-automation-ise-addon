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
using System.Diagnostics;
using System.IO;
using System.Web.Script.Serialization;

namespace AutomationISE.Model
{
    /// <summary>
    /// This class is used to generate a self signed certificate.
    /// </summary>
    class AutomationSelfSignedCertificate
    {
        private Certificate certObj = null;

        /// <summary>
        /// Constructor to intialize the values for the new certificate.
        /// It sets the cert friendly name and expiration date.
        /// Need to document how to remove the certificate thumbprint entry from the config
        /// file if a new certificate needs to be generated.
        /// </summary>
        public AutomationSelfSignedCertificate()
        {
            try
            {
                certObj = new Model.Certificate();
                certObj.FriendlyName = Properties.Settings.Default.certFriendlyName;
                certObj.ExpirationLengthInDays = Constants.ExpirationLengthInDaysForSelfSignedCert;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Creates the self signed certificate and installs into the current users store if
        /// an existing thumbprint does not exist in the configuration file.
        /// Updateds the Add On configuration file with the thumbprint of the generated certificate if one is created.
        /// </summary>
        /// <returns>The thumbprint of the certificate</returns>
        public String CreateSelfSignedCertificate()
        {
            try
            {
                String thumbprint = GetCertificateInConfigFile();
                if (thumbprint == null || thumbprint == "none")
                {
                    certObj.CreateCertificateRequest(Properties.Settings.Default.certName);
                    var selfSignedCert= certObj.InstallCertficate();
                    thumbprint = selfSignedCert.Thumbprint;
                    // Set thumbprint in configuration file.
                    SetCertificateInConfigFile(thumbprint);
                }
                return thumbprint;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Gets the thumbprint in the configuration file for the ISE Add On
        /// </summary>
        /// <returns>The value of the thumbprint value in the configuration file. Will be "none" when intially installed</returns>
        private static String GetCertificateInConfigFile()
        {
            List<PSModuleConfiguration.PSModuleConfigurationItem> config = getConfigFileItems();

            foreach (PSModuleConfiguration.PSModuleConfigurationItem pc in config)
            {
                if (pc.Name.Equals(PSModuleConfiguration.ModuleData.EncryptionCertificateThumbprint_FieldName))
                {
                    return pc.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Updates the configuration file with the thumbprint of the certificate
        /// </summary>
        /// <param name="thumbprint"></param>
        public static void SetCertificateInConfigFile(String thumbprint)
        {
            List<PSModuleConfiguration.PSModuleConfigurationItem> config = getConfigFileItems();
            bool found = false;
            foreach (PSModuleConfiguration.PSModuleConfigurationItem pc in config)
            {
                if (pc.Name.Equals(PSModuleConfiguration.ModuleData.EncryptionCertificateThumbprint_FieldName))
                {
                    found = true;
                    pc.Value = thumbprint;
                }
            }
            if (!found)
            {
                PSModuleConfiguration.PSModuleConfigurationItem pcItem = new PSModuleConfiguration.PSModuleConfigurationItem();
                pcItem.Name = PSModuleConfiguration.ModuleData.EncryptionCertificateThumbprint_FieldName;
                pcItem.Value = thumbprint;
                config.Add(pcItem);
            }

            JavaScriptSerializer jss = new JavaScriptSerializer();
            File.WriteAllText(GetConfigPath(), jss.Serialize(config));

        }

        /// <summary>
        /// Gets the complete list of configuration items in the configuation file
        /// </summary>
        /// <returns>A PSModuleConfiguraiton Item that contains all of the configuration items</returns>
        private static List<PSModuleConfiguration.PSModuleConfigurationItem> getConfigFileItems()
        {
            string configFilePath = GetConfigPath();

            if (!File.Exists(configFilePath))
            {
                Debug.WriteLine("Warning: a config file wasn't found, so a new one will be created");
            }

            JavaScriptSerializer jss = new JavaScriptSerializer();
            return jss.Deserialize<List<PSModuleConfiguration.PSModuleConfigurationItem>>((File.ReadAllText(configFilePath)));
        }

        /// <summary>
        /// Gets the path to the configuration file for the ISE Add On
        /// </summary>
        /// <returns>The full path to the configuration file</returns>
        private static String GetConfigPath()
        {
            string modulePath = PSModuleConfiguration.findModulePath();
            string configFilePath = System.IO.Path.Combine(modulePath, PSModuleConfiguration.ModuleData.ConfigFileName);

            return configFilePath;
        }
    }
}
