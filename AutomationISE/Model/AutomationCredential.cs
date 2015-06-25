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

namespace AutomationAzure
{
    /// <summary>
    /// The automation variable.
    /// </summary>
    public class AutomationCredential : AutomationAsset
    {
        // cloud only
        public AutomationCredential(Credential cloudCredential)
            : base(cloudCredential.Name)
        {
            this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.CloudOnly;
            this.LastModifiedCloud = cloudCredential.Properties.LastModifiedTime.DateTime;

            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Username", cloudCredential.Properties.UserName);
            valueFields.Add("Password", null);
            this.ValueFields = valueFields;

            this.LastModifiedLocal = null;
        }
        
        // local only - new
        public AutomationCredential(String name, string username, string password)
            : base(name)
        {
            this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.LocalOnly;
            this.LastModifiedLocal = new DateTime(); // TODO: does this default to now?
            
            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Username", username);
            valueFields.Add("Password", password);
            this.ValueFields = valueFields;

            this.LastModifiedCloud = null;
        }

        // local only - from json
        public AutomationCredential(CredentialJson localJson)
            : base(localJson.Name)
        {
            this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.LocalOnly;
            this.LastModifiedLocal = localJson.LastModified;
            
            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Username", localJson.Username);
            valueFields.Add("Password", localJson.Password);
            this.ValueFields = valueFields;

            this.LastModifiedCloud = null;
        }

        // both cloud and local
        public AutomationCredential(CredentialJson localJson, Credential cloudCredential)
            : base(localJson.Name)
        {
            this.LastModifiedLocal = localJson.LastModified;
            this.LastModifiedCloud = cloudCredential.Properties.LastModifiedTime.DateTime;

            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Username", localJson.Username);
            valueFields.Add("Password", localJson.Password);
            this.ValueFields = valueFields;

            if (this.LastModifiedCloud > this.LastModifiedLocal)
            {
                this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.UpdatedInCloud;
            }
            else if(this.LastModifiedCloud < this.LastModifiedLocal)
            {
                this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.UpdatedLocally;
            }
            else
            {
                this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.InSync;
            }
        }
    }

    public class CredentialJson : AssetJson {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
