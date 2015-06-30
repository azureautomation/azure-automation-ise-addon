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

namespace AutomationAzure
{
    /// <summary>
    /// The automation asset
    /// </summary>
    public abstract class AutomationAuthoringItem : IComparable<AutomationAuthoringItem>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationAuthoringItem"/> class.
        /// </summary>
        public AutomationAuthoringItem(string name, DateTime? lastModifiedLocal, DateTime? lastModifiedCloud)
        {
            this.Name = name;
            this.LastModifiedLocal = lastModifiedLocal;
            this.LastModifiedCloud = lastModifiedCloud;

            if (this.LastModifiedLocal == null)
            {
                this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.CloudOnly;
            }
            else if(this.LastModifiedCloud == null) 
            {
                this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.LocalOnly;
            }
            else
            {
                if (this.LastModifiedCloud > this.LastModifiedLocal)
                {
                    this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.UpdatedInCloud;
                }
                else if (this.LastModifiedCloud < this.LastModifiedLocal)
                {
                    this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.UpdatedLocally;
                }
                else
                {
                    this.SyncStatus = AutomationAuthoringItem.Constants.SyncStatus.InSync;
                }
            }
        }

        public int CompareTo(AutomationAuthoringItem other)
        {
            if (this.GetType().Equals(other.GetType()))
            {
                if (this.Name != null)
                {
                    return this.Name.CompareTo(other.Name);
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                return this.GetType().FullName.CompareTo(other.GetType().FullName);
            }
        }

        /// <summary>
        /// The name of the item
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The sync status for the item
        /// </summary>
        public string SyncStatus { get; set; }

        /// <summary>
        /// The last modified date of the item locally
        /// </summary>
        public Nullable<DateTime> LastModifiedLocal { get; set; }

        /// <summary>
        /// The last modified date of the item in the cloud
        /// </summary>
        public Nullable<DateTime> LastModifiedCloud { get; set; }

        public class Constants
        {
            public class SyncStatus
            {
                public const String LocalOnly = "Local Only";
                public const String InSync = "In Sync";
                public const String CloudOnly = "Cloud Only";
                public const String UpdatedInCloud = "Updated in cloud";
                public const String UpdatedLocally = "Updated locally";
            }
        }
    }
}
