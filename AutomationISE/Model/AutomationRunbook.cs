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

using Microsoft.Azure.Management.Automation;

namespace AutomationAzure
{
    using AzureAutomation;
    using Microsoft.Azure.Management.Automation.Models;
    using Microsoft.Azure.Management.Resources.Models;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using AutomationManagement = Microsoft.Azure.Management.Automation;

    /// <summary>
    /// The automation runbook.
    /// </summary>
    public class AutomationRunbook
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationRunbook"/> class.
        /// </summary>
        /// <param name="runbook">
        /// The cloud service.
        /// </param>
        public AutomationRunbook(AutomationManagementClient automationClient, String resourceGroup, String automationResource, Runbook runbook)
        {
            Requires.Argument("runbook", runbook).NotNull();

            this.Name = runbook.Name;
            this.Status = runbook.Properties.State;
            this.LocalStatus = Constants.Status.CloudOnly;
            this.LastModified = runbook.Properties.LastModifiedTime.DateTime;

            this.automationManagementClient = automationClient;
            this.RessourceGroupName = resourceGroup;
            this.AutomationAccountName = automationResource;
            this.Parameters = runbook.Properties.Parameters;
        }

        public AutomationRunbook(AutomationManagementClient automationClient, String resourceGroup, String automationResource, FileInfo runbook)
        {
            Requires.Argument("runbook", runbook).NotNull();

            this.Name = runbook.Name;
            this.Status = Constants.notExist;
            this.LocalStatus = Constants.Status.LocalOnly;
            this.LastModified = runbook.LastWriteTime;
            this.LocalFile = runbook;

            this.automationManagementClient = automationClient;
            this.RessourceGroupName = resourceGroup;
            this.AutomationAccountName = automationResource;
        }

        /// <summary>
        ///  Gets or sets the automation management client
        /// </summary>
        public AutomationManagementClient automationManagementClient { get; set; }

        /// <summary>
        /// Gets or sets the automation account name.
        /// </summary>
        public string AutomationAccountName { get; set; }

        /// <summary>
        /// Gets or sets the Resource group name for this automation account.
        /// </summary>
        public string RessourceGroupName { get; set; }

        /// <summary>
        /// Gets or sets the Resource group name for this automation account.
        /// </summary>
        public FileInfo LocalFile { get; set; }

        /// <summary>
        /// The parameters of the runbook
        /// </summary>
        public IDictionary<string,RunbookParameter> Parameters { get; set; }

        /// <summary>
        /// The name of the runbook
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The cloud status for the runbook
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// The local status for the runbook
        /// </summary>
        public string LocalStatus { get; set; }

        /// <summary>
        /// The last modified date of the runbook
        /// </summary>
        public DateTime LastModified { get; set; }
    }
}
