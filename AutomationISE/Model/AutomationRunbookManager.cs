using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Automation;
using Microsoft.Azure.Management.Automation.Models;

namespace AutomationISE.Model
{
    /*
     * Responsible for syncing runbooks between the cloud and on disk.
     */
    public static class AutomationRunbookManager
    {
        public static async Task<ISet<AutomationRunbook>> GetAllRunbooks(AutomationManagementClient automationManagementClient, string workspace, string resourceGroupName, string accountName)
        {
            ISet<AutomationRunbook> result = new SortedSet<AutomationRunbook>();
            IList<Runbook> cloudRunbooks = await DownloadRunbooks(automationManagementClient, resourceGroupName, accountName);
            
            /* Dictionary of (filename, filepath) tuples found on disk. This will come in handy */
            string[] localRunbookFilePaths = Directory.GetFiles(workspace, "*.ps1");
            Dictionary<string, string> filePathForRunbook = new Dictionary<string, string>();
            foreach (string path in localRunbookFilePaths)
            {
                filePathForRunbook.Add(System.IO.Path.GetFileNameWithoutExtension(path), path);
            }
            /* Start by checking the downloaded runbooks */
            foreach (Runbook cloudRunbook in cloudRunbooks)
            {
                if (filePathForRunbook.ContainsKey(cloudRunbook.Name))
                {
                    result.Add(new AutomationRunbook(new FileInfo(filePathForRunbook[cloudRunbook.Name]), cloudRunbook));
                }
                else
                {
                    result.Add(new AutomationRunbook(cloudRunbook));
                }
            }
            /* Now find runbooks on disk that aren't yet accounted for */
            foreach (string localRunbookName in filePathForRunbook.Keys)
            {
                //Not great, but works for now
                if (result.FirstOrDefault(x => x.Name == localRunbookName) == null)
                {
                    result.Add(new AutomationRunbook(new FileInfo(filePathForRunbook[localRunbookName])));
                }
            }
            return result;
        }

        private static async Task<IList<Runbook>> DownloadRunbooks(AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            RunbookListResponse cloudRunbooks = await automationManagementClient.Runbooks.ListAsync(resourceGroupName, accountName);
            return cloudRunbooks.Runbooks;
        }
    }
}
