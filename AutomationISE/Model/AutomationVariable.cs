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
            : base(cloudVariable.Name, null, cloudVariable.Properties.LastModifiedTime.LocalDateTime)
        {
            this.Encrypted = cloudVariable.Properties.IsEncrypted;

            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Value", cloudVariable.Properties.Value);
            this.ValueFields = valueFields;
        }
        
        // local only - new
        public AutomationVariable(String name, Object value, bool encrypted)
            : base(name, DateTime.Now, null)
        {
            this.Encrypted = encrypted; 
 
            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Value", value);
            this.ValueFields = valueFields;
        }

        // local only - from json
        public AutomationVariable(VariableJson localJson)
            : base(localJson, null)
        {
            this.Encrypted = localJson.Encrypted;
            
            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Value", localJson.Value);
            this.ValueFields = valueFields;
        }

        // both cloud and local
        public AutomationVariable(VariableJson localJson, Variable cloudVariable)
            : base(localJson, cloudVariable.Properties.LastModifiedTime.LocalDateTime)
        {
            this.Encrypted = cloudVariable.Properties.IsEncrypted;

            IDictionary<String, Object> valueFields = new Dictionary<string, Object>();
            valueFields.Add("Value", localJson.Value);
            this.ValueFields = valueFields;
        }

        /// <summary>
        /// Whether the automation variable is encrypted or not.
        /// </summary>
        public bool Encrypted { get; set; }
    }

    public class VariableJson : AssetJson {
        public VariableJson() {}
        
        public VariableJson(AutomationVariable variable)
            : base(variable)
        {
            this.Encrypted = variable.Encrypted;
            
            Object tempValue;
            variable.ValueFields.TryGetValue("Value", out tempValue);
            this.Value = tempValue;
        }

        public override void Update(AutomationAsset asset)
        {
            var variable = (AutomationVariable)asset;
            this.Encrypted = variable.Encrypted;

            Object tempValue;
            variable.ValueFields.TryGetValue("Value", out tempValue);

            if(this.Encrypted != variable.Encrypted || this.Value != tempValue)
            {
                this.Value = tempValue;
                this.Encrypted = variable.Encrypted;
                this.LastModified = DateTime.Now;
            }
        }
        
        public Object Value { get; set; }
        public bool Encrypted { get; set; }
    }
}
