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

namespace AutomationAzure
{
    /// <summary>
    /// The automation runbook.
    /// </summary>
    public class AutomationRunbook : AutomationAuthoringItem
    {
        // cloud only
        public AutomationRunbook(Runbook cloudRunbook) :
            base(cloudRunbook.Name, null, cloudRunbook.Properties.LastModifiedTime.DateTime)
        {
            this.Status = cloudRunbook.Properties.State;
            this.Parameters = cloudRunbook.Properties.Parameters;
        }

        // local only - new
        public AutomationRunbook(string name) :
            base(name, DateTime.Now, null)
        {
            this.Status = AutomationRunbook.Constants.Status.New;
            this.Parameters = null;
        }

        // local only - from file
        public AutomationRunbook(FileInfo localFile)
            : base(localFile.Name, localFile.LastWriteTime, null)
        {
            this.Status = AutomationRunbook.Constants.Status.New;
            this.Parameters = null;
        }

        // both cloud and local
        public AutomationRunbook(FileInfo localFile, Runbook cloudRunbook)
            : base(cloudRunbook.Name, localFile.LastWriteTime, cloudRunbook.Properties.LastModifiedTime.DateTime)
        {
            this.Status = cloudRunbook.Properties.State;
            this.Parameters = cloudRunbook.Properties.Parameters;
        }

        /// <summary>
        /// The parameters of the runbook
        /// </summary>
        public IDictionary<string,RunbookParameter> Parameters { get; set; }

        /// <summary>
        /// The cloud status for the runbook
        /// </summary>
        public string Status { get; set; }

        public class Constants
        {
            public class Status
            {
                public const String New = "New";
                public const String InEdit = "Edit";
                public const String Published = "Published";
            }
        }
    }
}
