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

using CERTENROLLLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Web.Script.Serialization;

namespace AutomationISE.Model
{
    class AutomationSelfSignedCertificate
    {
        private Certificate certObj = null;

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
                }
                return thumbprint;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static String GetCertificateInConfigFile()
        {
            string modulePath = PSModuleConfiguration.findModulePath();
            string configFilePath = System.IO.Path.Combine(modulePath, PSModuleConfiguration.ModuleData.ConfigFileName);

            if (!File.Exists(configFilePath))
            {
                Debug.WriteLine("Warning: a config file wasn't found in the module, so a new one will be created");
                return null;
            }

            JavaScriptSerializer jss = new JavaScriptSerializer();
            List<PSModuleConfiguration.PSModuleConfigurationItem> config = jss.Deserialize<List<PSModuleConfiguration.PSModuleConfigurationItem>>((File.ReadAllText(configFilePath)));

            foreach (PSModuleConfiguration.PSModuleConfigurationItem pc in config)
            {
                if (pc.Name.Equals(PSModuleConfiguration.ModuleData.EncryptionCertificateThumbprint_FieldName))
                {
                    return pc.Value;
                }
            }
            return null;
        }
    }
}
