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
using System.Web.Script.Serialization;
using Microsoft.Azure.Management.Automation.Models;
using Microsoft.Azure.Management.Automation;
using System.Threading.Tasks;
using System.Threading;

namespace AutomationISE.Model
{
    /// <summary>
    /// The source control class for integrating with Azure Automation source control
    /// </summary>
    static class AutomationSourceControl
    {

        public static async Task<bool> isSourceControlEnabled(AutomationManagementClient automationClient, String resourceGroup, String automationAccount)
        {
            // TODO
            // This is a current way to determine if source control is enabled.
            // Will update this once the API becomes available.
            try {
                var response = await automationClient.Variables.GetAsync(resourceGroup, automationAccount, Constants.sourceControlConnectionVariable);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<JobCreateResponse> startSouceControlJob(AutomationManagementClient automationClient, String resourceGroup, String automationAccount)
        {
            var jobParams = new JobCreateParameters
            {
                Properties = new JobCreateProperties
                {
                    Runbook = new RunbookAssociationProperty
                    {
                        Name = Constants.sourceControlRunbook
                    },
                    Parameters = null
                }
            };

            var jobResponse = await automationClient.Jobs.CreateAsync(resourceGroup,
                                automationAccount, jobParams, new CancellationToken());
            return jobResponse;
        }
    }
}
