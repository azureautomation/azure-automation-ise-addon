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
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Automation;
using Microsoft.Azure.Management.Automation.Models;

namespace AutomationISE.Model
{
    /// <summary>
    /// The automation asset
    /// </summary>
    public abstract class AutomationAsset : AutomationAuthoringItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationAsset"/> class.
        /// </summary>
        public AutomationAsset(string name, DateTime? lastModifiedLocal, DateTime? lastModifiedCloud, IDictionary<String, Object> valueFields) :
            base(name, lastModifiedLocal, lastModifiedCloud)
        {
            this.ValueFields = valueFields;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationAsset"/> class.
        /// </summary>
        public AutomationAsset(string name, DateTime? lastModifiedLocal, DateTime? lastModifiedCloud) :
            base(name, lastModifiedLocal, lastModifiedCloud)
        {
            this.ValueFields = new Dictionary<string, Object>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationAsset"/> class.
        /// </summary>
        public AutomationAsset(AssetJson localJson, DateTime? lastModifiedCloud) :
            base(localJson.Name, DateTime.Parse(localJson.LastModified, null, DateTimeStyles.RoundtripKind), lastModifiedCloud)
        {
            this.ValueFields = new Dictionary<string, Object>();
        }

        /// <summary>
        /// The value of the asset
        /// </summary>
        protected IDictionary<String, Object> ValueFields { get; set; }

    }

    public abstract class AssetJson
    {
        public AssetJson() {}
        
        public AssetJson(AutomationAsset asset)
        {
            this.Name = asset.Name;

            if(asset.LastModifiedCloud == null)
            {
                setLastModified((System.DateTime)asset.LastModifiedLocal);
            }
            else if (asset.LastModifiedLocal == null)
            {
                setLastModified((System.DateTime)asset.LastModifiedCloud);
            }
            else
            {
                var lastModifiedDatetime = (System.DateTime)(asset.LastModifiedLocal > asset.LastModifiedCloud ? asset.LastModifiedLocal : asset.LastModifiedCloud);
                setLastModified(lastModifiedDatetime);
            }
        }

        public void setLastModified(DateTime lastModified)
        {
            this.LastModified = lastModified.ToString("u");
        }
        
        public string Name { get; set; }
        public string LastModified { get; set; }
    }
}
