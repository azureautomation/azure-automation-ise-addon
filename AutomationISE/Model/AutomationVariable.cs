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
    public class AutomationVariable : AutomationAsset
    {
        // cloud only
        public AutomationVariable(Variable cloudVariable)
            : base(cloudVariable.Name)
        {
            this.Encrypted = cloudVariable.Properties.IsEncrypted;
            this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.CloudOnly;
            this.LastModifiedCloud = cloudVariable.Properties.LastModifiedTime.DateTime;

            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Value", cloudVariable.Properties.Value);
            this.ValueFields = valueFields;

            this.LastModifiedLocal = null;
        }
        
        // local only - new
        public AutomationVariable(String name, Object value, bool encrypted)
            : base(name)
        {
            this.Encrypted = encrypted; 
            this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.LocalOnly;
            this.LastModifiedLocal = DateTime.Now;
            
            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Value", value);
            this.ValueFields = valueFields;

            this.LastModifiedCloud = null;
        }

        // local only - from json
        public AutomationVariable(VariableJson localJson, bool encrypted)
            : base(localJson.Name)
        {
            this.Encrypted = encrypted;
            this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.LocalOnly;
            this.LastModifiedLocal = localJson.LastModified;
            
            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Value", localJson.Value);
            this.ValueFields = valueFields;

            this.LastModifiedCloud = null;
        }

        // both cloud and local
        public AutomationVariable(VariableJson localJson, Variable cloudVariable)
            : base(localJson.Name)
        {
            this.Encrypted = cloudVariable.Properties.IsEncrypted;
            this.LastModifiedLocal = localJson.LastModified;
            this.LastModifiedCloud = cloudVariable.Properties.LastModifiedTime.DateTime;

            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Value", localJson.Value);
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

        /// <summary>
        /// Whether the automation variable is encrypted or not.
        /// </summary>
        public bool Encrypted { get; set; }
    }

    public class VariableJson : AssetJson {
        public Object Value { get; set; }
    }
}
