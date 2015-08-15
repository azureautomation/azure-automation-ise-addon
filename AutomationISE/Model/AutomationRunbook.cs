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
    public class AutomationRunbook : AutomationAuthoringItem
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
        public IDictionary<string, RunbookParameter> Parameters { get; set; }

        //Runbook already exists in the cloud, but not on disk.
        public AutomationRunbook(Runbook cloudRunbook, RunbookDraft cloudRunbookDraft) :
            base(cloudRunbook.Name, null, cloudRunbook.Properties.LastModifiedTime.LocalDateTime)
        {
            this.AuthoringState = cloudRunbook.Properties.State;
            this.localFileInfo = null;
            this.Description = cloudRunbook.Properties.Description;
            this.Parameters = cloudRunbook.Properties.Parameters;
            if (cloudRunbookDraft != null)
            {
                this.LastModifiedCloud = cloudRunbookDraft.LastModifiedTime.LocalDateTime;
                UpdateSyncStatus();
            }
        }

        //Runbook exists on disk, but not in the cloud.
        public AutomationRunbook(FileInfo localFile)
            : base(System.IO.Path.GetFileNameWithoutExtension(localFile.Name), localFile.LastWriteTime, null)
        {
            this.AuthoringState = AutomationRunbook.AuthoringStates.New;
            this.localFileInfo = localFile;
            this.Parameters = null;
        }

        //Runbook exists both on disk and in the cloud. But are they in sync?
        public AutomationRunbook(FileInfo localFile, Runbook cloudRunbook, RunbookDraft cloudRunbookDraft)
            : base(cloudRunbook.Name, localFile.LastWriteTime, cloudRunbook.Properties.LastModifiedTime.LocalDateTime)
        {
            this.AuthoringState = cloudRunbook.Properties.State;
            this.localFileInfo = localFile;
            this.Description = cloudRunbook.Properties.Description;
            this.Parameters = cloudRunbook.Properties.Parameters;
            if (cloudRunbookDraft != null)
            {
                this.LastModifiedCloud = cloudRunbookDraft.LastModifiedTime.LocalDateTime;
                UpdateSyncStatus();
            }
        }
        public static class AuthoringStates
        {
            public const String New = "New";
            public const String InEdit = "In Edit";
            public const String Published = "Published";
        }
    }
}
