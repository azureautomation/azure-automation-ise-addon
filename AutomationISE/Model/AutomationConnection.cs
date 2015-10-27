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
        public AutomationConnection(Connection cloudConnection, ConnectionType cloudConnectionType)
            : base(cloudConnection.Name, null, cloudConnection.Properties.LastModifiedTime.LocalDateTime)
        {
            this.ConnectionType = cloudConnectionType.Name;
            
            JavaScriptSerializer jss = new JavaScriptSerializer();
            this.ValueFields = new Dictionary<string, Object>();

            foreach(KeyValuePair<string, string> field in cloudConnection.Properties.FieldDefinitionValues)
            {
                if(cloudConnectionType.Properties.FieldDefinitions[field.Key].Type.Equals(AutomationISE.Model.Constants.ConnectionTypeFieldType.String))
                {
                    this.ValueFields.Add(field.Key, field.Value);
                }
                else
                {
                    try
                    {
                        var value = jss.DeserializeObject(field.Value.ToLower());
                        this.ValueFields.Add(field.Key, value);
                    }
                    catch(Exception e)
                    {
                        this.ValueFields.Add(field.Key, field.Value);
                    }
                }
            }
        }
        
        // local only - new
        public AutomationConnection(String name, IDictionary<string, Object> valueFields, string connectionType)
            : base(name, DateTime.Now, null)
        {
            this.ValueFields = valueFields;
            this.ConnectionType = connectionType;
        }

        // local only - from json
        public AutomationConnection(ConnectionJson localJson)
            : base(localJson, null)
        {
            this.ValueFields = localJson.ValueFields;
            this.ConnectionType = localJson.ConnectionType;
        }

        // both cloud and local
        public AutomationConnection(ConnectionJson localJson, Connection cloudCredential)
            : base(localJson, cloudCredential.Properties.LastModifiedTime.LocalDateTime)
        {
            this.ValueFields = localJson.ValueFields;
            this.ConnectionType = localJson.ConnectionType;
        }

        public IDictionary<string, Object> getFields()
        {
            return this.ValueFields;
        }

        public void setFields(IDictionary<string, Object> fields)
        {
            this.ValueFields = fields;
        }

        protected override bool isReadyForLocalUse()
        {
            foreach (KeyValuePair<string, object> field in this.getFields())
            {
                if (field.Value == null)
                {
                    return false;
                }
            }

            return true;
        }

        public override String getGetCommand()
        {
            return ("Get-AutomationConnection -Name \"" + this.Name + "\"");
        }

        /// <summary>
        /// The connection type of the connection
        /// </summary>
        public string ConnectionType { get; set; }
    }

    public class ConnectionJson : AssetJson {
        public ConnectionJson() { }

        public ConnectionJson(AutomationConnection connection)
            : base(connection)
        {
            this.ValueFields = connection.getFields();
            this.ConnectionType = connection.ConnectionType;
        }

        public IDictionary<string, Object> ValueFields { get; set; }
        public string ConnectionType { get; set;  }
    }
}
