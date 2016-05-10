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

namespace AutomationISE.Model
{
    /* Note that AutomationAuthoringItem implements INotifyPropertyChanged */
    public class AutomationDSC: AutomationAuthoringItem
    {
        private string _authoringState;
        public string AuthoringState
        {
            get { return _authoringState; }
            set
            {
                _authoringState = value;
                NotifyPropertyChanged("AuthoringState");
            }
        }

        public string Description { get; set; }

        private FileInfo _localFileInfo;
        public FileInfo localFileInfo
        {
            get { return _localFileInfo; }
            set { _localFileInfo = value; }
        }
        public IDictionary<string, DscConfigurationParameter> Parameters { get; set; }

        //Configuration already exists in the cloud, but not on disk.
        public AutomationDSC(DscConfiguration cloudConfiguration, DscConfiguration cloudConfigurationDraft) :
            base(cloudConfiguration.Name, null, cloudConfiguration.Properties.LastModifiedTime.LocalDateTime)
        {
            this.AuthoringState = cloudConfiguration.Properties.State;
            this.localFileInfo = null;
            this.Description = cloudConfiguration.Properties.Description;
            this.Parameters = cloudConfiguration.Properties.Parameters;
            if (cloudConfigurationDraft != null)
            {
                this.LastModifiedCloud = cloudConfigurationDraft.Properties.LastModifiedTime.LocalDateTime;
                UpdateSyncStatus();
            }
        }

        //Configuration exists on disk, but not in the cloud.
        public AutomationDSC(FileInfo localFile)
            : base(System.IO.Path.GetFileNameWithoutExtension(localFile.Name), localFile.LastWriteTime, null)
        {
            this.AuthoringState = DscConfigurationState.New;
            this.localFileInfo = localFile;
            this.Parameters = null;
        }

        //Configuration exists both on disk and in the cloud. But are they in sync?
        public AutomationDSC(FileInfo localFile, DscConfiguration cloudConfiguration, DscConfiguration cloudConfigurationDraft)
            : base(cloudConfiguration.Name, localFile.LastWriteTime, cloudConfiguration.Properties.LastModifiedTime.LocalDateTime)
        {
            this.AuthoringState = cloudConfiguration.Properties.State;
            this.localFileInfo = localFile;
            this.Description = cloudConfiguration.Properties.Description;
            this.Parameters = cloudConfiguration.Properties.Parameters;
            if (cloudConfigurationDraft != null)
            {
                this.LastModifiedCloud = cloudConfigurationDraft.Properties.LastModifiedTime.LocalDateTime;
                UpdateSyncStatus();
            }

        }
        public static class AuthoringStates
        {
            public const String New = "New";
            public const String InEdit = "Edit";
            public const String Published = "Published";
        }
    }
}
