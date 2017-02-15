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
using System.IO;
using AutomationISE.Model;

using System.Diagnostics;
using System.Management.Automation;

namespace AutomationISE.Model
{
    /* Note that AutomationAuthoringItem implements INotifyPropertyChanged */
    public class AutomationModule : AutomationAuthoringItem
    {
        public string localVersion { get; set; }
        public string cloudVersion { get; set; }
        public string localModulePath{ get; set; }
        public IDictionary<string, DscConfigurationParameter> Parameters { get; set; }

        //Module already exists in the cloud, but not on disk.
        public AutomationModule(Module cloudModule, Module cloudModuleDraft) :
            base(cloudModule.Name, null, cloudModule.Properties.LastModifiedTime.LocalDateTime)
        {
            this.localModulePath = null;
            this.localVersion = null;
            if (cloudModuleDraft != null)
            {
                this.LastModifiedCloud = cloudModuleDraft.Properties.LastModifiedTime.LocalDateTime;
                this.cloudVersion = cloudModuleDraft.Properties.Version;
                UpdateSyncStatus();
            }
        }

        //Module exists on disk, but not in the cloud.
        public AutomationModule(PSObject localModule, DateTime modifiedDate)
            : base(localModule.Properties["Name"].Value.ToString(), modifiedDate, null)
        {
            this.localVersion = localModule.Properties["Version"].Value.ToString();
            this.localModulePath = localModule.Properties["ModuleBase"].Value.ToString();
        }

        //Module exists both on disk and in the cloud. But are they in sync?
        public AutomationModule(PSObject localModule, Module cloudModule, Module cloudModuleDraft, DateTime modifiedDate)
            : base(cloudModule.Name, modifiedDate, cloudModule.Properties.LastModifiedTime.LocalDateTime)
        {
            this.localVersion = localModule.Properties["Version"].Value.ToString();
            this.localModulePath = localModule.Properties["ModuleBase"].Value.ToString();

            // If the versions are the same, set the datetime to be equal so they show up as InSync
            if (cloudModuleDraft != null)
            {
                this.cloudVersion = cloudModuleDraft.Properties.Version;
                if (localModule.Properties["Version"].Value.ToString() == cloudModuleDraft.Properties.Version)
                {
                    this.LastModifiedLocal = cloudModuleDraft.Properties.LastModifiedTime.LocalDateTime;
                }
                UpdateSyncStatus();
            }
        }
    }
}
