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

namespace AutomationAzure
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
            this.ValueFields = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationAsset"/> class.
        /// </summary>
        public AutomationAsset(AssetJson localJson, DateTime? lastModifiedCloud) :
            base(localJson.Name, localJson.LastModified, lastModifiedCloud)
        {
            this.ValueFields = null;
        }

        /// <summary>
        /// The value of the asset
        /// </summary>
        public IDictionary<String, Object> ValueFields { get; set; }

    }

    public abstract class AssetJson
    {
        public string Name { get; set; }
        public DateTime LastModified { get; set; }
    }
}
