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
using Microsoft.Azure.Management.Automation.Models;
using System.Web.Script.Serialization;

namespace AutomationISE.Model
{
    /// <summary>
    /// The automation certficate.
    /// </summary>
    public class AutomationCertificate : AutomationAsset
    {
        // cloud only
        public AutomationCertificate(Microsoft.Azure.Management.Automation.Models.Certificate cloudCertificate)
            : base(cloudCertificate.Name, null, cloudCertificate.Properties.LastModifiedTime.LocalDateTime)
        {
            try
            {
                this.setThumbprint(cloudCertificate.Properties.Thumbprint);
                this.setExportable(cloudCertificate.Properties.IsExportable);
            }
            catch
            {
                this.setThumbprint(null);
            }
            this.Encrypted = true;
        }

        // local only - new
        public AutomationCertificate(String name, string Thumbprint, string CertificatePath, string CertificatePassword, bool exportable, bool encrypted)
            : base(name, DateTime.Now, null)
        {
            this.setThumbprint(Thumbprint);
            this.setCertPath(CertificatePath);
            this.setPassword(CertificatePassword);
            this.setExportable(exportable);
            this.Encrypted = encrypted;
        }

        // local only - from json
        public AutomationCertificate(CertificateJson localJson)
            : base(localJson, null)
        {
            this.setThumbprint(localJson.Thumbprint);
            this.setCertPath(localJson.CertPath);
            this.setPassword(localJson.Password);
            this.setExportable(localJson.Exportable);
            this.Encrypted = localJson.Encrypted;
        }

        // both cloud and local
        public AutomationCertificate(CertificateJson localJson, Microsoft.Azure.Management.Automation.Models.Certificate cloudCertificate)
            : base(localJson, cloudCertificate.Properties.LastModifiedTime.LocalDateTime)
        {
            this.setThumbprint(localJson.Thumbprint);
            this.setCertPath(localJson.CertPath);
            this.setPassword(localJson.Password);
            this.setExportable(localJson.Exportable);
            this.Encrypted = localJson.Encrypted;
        }

        public void setThumbprint(string thumbprint)
        {
            this.ValueFields.Remove("Thumbprint");
            this.ValueFields.Add("Thumbprint", thumbprint);
        }

        public string getThumbprint()
        {
            Object tempValue;
            this.ValueFields.TryGetValue("Thumbprint", out tempValue);
            return (string)tempValue;
        }

        public void setCertPath(string certPath)
        {
            this.ValueFields.Remove("CertificatePath");
            this.ValueFields.Add("CertificatePath", certPath);
        }

        public string getCertPath()
        {
            Object tempValue;
            this.ValueFields.TryGetValue("CertificatePath", out tempValue);
            return (string)tempValue;
        }

        public string getPassword()
        {
            Object tempValue;
            this.ValueFields.TryGetValue("Password", out tempValue);
            return (string)tempValue;
        }

        public void setPassword(string password)
        {
            this.ValueFields.Remove("Password");
            this.ValueFields.Add("Password", password);
        }

        public bool getExportable()
        {
            Object tempValue;
            this.ValueFields.TryGetValue("Exportable", out tempValue);
            return (bool)tempValue;
        }

        public void setExportable(bool exportable)
        {
            this.ValueFields.Remove("Exportable");
            this.ValueFields.Add("Exportable", exportable);
        }

        protected override bool isReadyForLocalUse()
        {
            return this.getCertPath() != null;
        }

        public override String getGetCommand(String runbookType = AutomationISE.Model.Constants.RunbookType.PowerShellScript)
        {
            if (runbookType == AutomationISE.Model.Constants.RunbookType.PowerShellScript)
                return ("Get-AutomationCertificate -Name \"" + this.Name + "\"");
            else if (runbookType == AutomationISE.Model.Constants.RunbookType.Python2)
                return ("automationassets.get_automation_certificate(\"" + this.Name + "\")");
            else return "";
        }

        /// <summary>
        /// Whether the automation certificate is encrypted or not.
        /// </summary>
        public bool Encrypted { get; set; }
    }

    public class CertificateJson : AssetJson {
        public CertificateJson() { }

        public CertificateJson(AutomationCertificate certificate)
            : base(certificate)
        {
            this.Thumbprint = certificate.getThumbprint();
            this.CertPath = certificate.getCertPath();
            this.Password = certificate.getPassword();
            this.Exportable = certificate.getExportable();
            this.Encrypted = certificate.Encrypted;
        }

        public IDictionary<string, Object> ValueFields { get; set; }
        public string Thumbprint { get; set;  }
        public string CertPath { get; set; }
        public string Password { get; set; }
        public bool Exportable { get; set; }
        public bool Encrypted { get; set; }
    }
}
