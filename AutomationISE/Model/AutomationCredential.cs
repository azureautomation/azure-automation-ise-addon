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

namespace AutomationISE.Model
{
    /// <summary>
    /// The automation variable.
    /// </summary>
    public class AutomationCredential : AutomationAsset
    {
        // cloud only
        public AutomationCredential(Credential cloudCredential)
            : base(cloudCredential.Name, null, cloudCredential.Properties.LastModifiedTime.LocalDateTime)
        {
            this.setUsername(cloudCredential.Properties.UserName);
            this.setPassword(null);
        }
        
        // local only - new
        public AutomationCredential(String name, string username, string password)
            : base(name, DateTime.Now, null)
        {
            this.setUsername(username);
            this.setPassword(password);
        }

        // local only - from json
        public AutomationCredential(CredentialJson localJson)
            : base(localJson, null)
        {
            this.setUsername(localJson.Username);
            this.setPassword(localJson.Password);
        }

        // both cloud and local
        public AutomationCredential(CredentialJson localJson, Credential cloudCredential)
            : base(localJson, cloudCredential.Properties.LastModifiedTime.LocalDateTime)
        {
            this.setUsername(localJson.Username);
            this.setPassword(localJson.Password);
        }

        public string getUsername()
        {
            Object tempValue;
            this.ValueFields.TryGetValue("Username", out tempValue);
            return (string)tempValue;
        }

        public void setUsername(string username)
        {
            this.ValueFields.Add("Username", username);
        }

        public string getPassword()
        {
            Object tempValue;
            this.ValueFields.TryGetValue("Password", out tempValue);
            return (string)tempValue;
        }

        public void setPassword(string password)
        {
            this.ValueFields.Add("Password", password);
        }
    }

    public class CredentialJson : AssetJson {
        public CredentialJson() { }
        
        public CredentialJson(AutomationCredential credential)
            : base(credential)
        {
            this.Username = credential.getUsername();
            this.Password = credential.getPassword();
        }

        public string Username { get; set; }
        public string Password { get; set; }
    }
}
