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
    /// The automation variable.
    /// </summary>
    public class AutomationConnection : AutomationAsset
    {
        // cloud only
        public AutomationConnection(Connection cloudConnection)
            : base(cloudConnection.Name, null, cloudConnection.Properties.LastModifiedTime.LocalDateTime)
        {
            this.ValueFields = new Dictionary<string, Object>();
            foreach (var key in cloudConnection.Properties.FieldDefinitionValues.Keys)
            {
                string jsonValue = "null";
                cloudConnection.Properties.FieldDefinitionValues.TryGetValue(key, out jsonValue);

                JavaScriptSerializer jss = new JavaScriptSerializer();
                var value = jss.DeserializeObject(jsonValue);
                this.ValueFields.Add(key, value);
            }
        }
        
        // local only - new
        public AutomationConnection(String name, IDictionary<string, Object> valueFields)
            : base(name, DateTime.Now, null)
        {
            this.ValueFields = valueFields;
        }

        // local only - from json
        public AutomationConnection(ConnectionJson localJson)
            : base(localJson, null)
        {
            this.ValueFields = localJson.ValueFields;
        }

        // both cloud and local
        public AutomationConnection(ConnectionJson localJson, Connection cloudCredential)
            : base(localJson, cloudCredential.Properties.LastModifiedTime.LocalDateTime)
        {
            this.ValueFields = localJson.ValueFields;
        }

        public IDictionary<string, Object> getFields()
        {
            return this.ValueFields;
        }

        public void setFields(IDictionary<string, Object> fields)
        {
            this.ValueFields = fields;
        }
    }

    public class ConnectionJson : AssetJson {
        public ConnectionJson() { }

        public ConnectionJson(AutomationConnection connection)
            : base(connection)
        {
            this.ValueFields = connection.getFields();
        }

        public IDictionary<string,Object> ValueFields { get; set; }
    }
}
