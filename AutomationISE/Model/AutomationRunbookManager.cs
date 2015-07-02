using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Automation;
using Microsoft.Azure.Management.Automation.Models;

namespace AutomationISE.Model
{
    public static class AutomationRunbookManager
    {
        public static async Task<ISet<AutomationRunbook>> GetAllRunbooks(AutomationManagementClient automationManagementClient, string workspace, string resourceGroupName, string accountName)
        {
            ISet<AutomationRunbook> result = new SortedSet<AutomationRunbook>();
            IList<Runbook> cloudRunbooks = await DownloadRunbooks(automationManagementClient, resourceGroupName, accountName);
            /* Get the runbooks on disk */
            /* For each runbook, construct the AutomationRunbook objects: "merge" the local and cloud runbooks appropriately */
            return result;
        }

        private static async Task<IList<Runbook>> DownloadRunbooks(AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            RunbookListResponse cloudRunbooks = await automationManagementClient.Runbooks.ListAsync(resourceGroupName, accountName);
            return cloudRunbooks.Runbooks;
        }
    }
}
