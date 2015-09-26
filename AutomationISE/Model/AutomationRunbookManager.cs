using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Automation;
using Microsoft.Azure.Management.Automation.Models;

using System.Diagnostics;

namespace AutomationISE.Model
{
    /*
     * Responsible for syncing runbooks between the cloud and on disk.
     */
    public static class AutomationRunbookManager
    {
        public static async Task UploadRunbookAsDraft(AutomationRunbook runbook, AutomationManagementClient automationManagementClient, string resourceGroupName, AutomationAccount account)
        {
            RunbookCreateOrUpdateDraftProperties draftProperties;

            // Parse the script to determine if it is a PS workflow or native script
            String PSScriptText = File.ReadAllText(runbook.localFileInfo.FullName);
            System.Management.Automation.Language.Token[] AST;
            System.Management.Automation.Language.ParseError[] ASTError = null;
            var ASTScript = System.Management.Automation.Language.Parser.ParseInput(PSScriptText, out AST, out ASTError);

            // If the script starts with workflow, then create a PS Workflow script runbook or else create a native PS script runbook
            if (ASTScript.EndBlock.Extent.Text.ToLower().StartsWith("workflow"))
            {
                draftProperties = new RunbookCreateOrUpdateDraftProperties(Constants.RunbookType.Workflow, new RunbookDraft());
            }
            else
                draftProperties = new RunbookCreateOrUpdateDraftProperties(Constants.RunbookType.PowerShellScript, new RunbookDraft());

            draftProperties.Description = runbook.Description;
            RunbookCreateOrUpdateDraftParameters draftParams = new RunbookCreateOrUpdateDraftParameters(draftProperties);
            draftParams.Name = runbook.Name;
            draftParams.Location = account.Location;
            await automationManagementClient.Runbooks.CreateOrUpdateWithDraftAsync(resourceGroupName, account.Name, draftParams);
            /* Update the runbook content from .ps1 file */

            RunbookDraftUpdateParameters draftUpdateParams = new RunbookDraftUpdateParameters()
            {
                Name = runbook.Name,
                Stream = PSScriptText
            };
            await automationManagementClient.RunbookDraft.UpdateAsync(resourceGroupName, account.Name, draftUpdateParams);
            /* Ensure the correct sync status is detected */
            RunbookDraft draft = await GetRunbookDraft(runbook.Name, automationManagementClient, resourceGroupName, account.Name);
            runbook.localFileInfo.LastWriteTime = draft.LastModifiedTime.LocalDateTime;
            runbook.LastModifiedLocal = draft.LastModifiedTime.LocalDateTime;
            runbook.LastModifiedCloud = draft.LastModifiedTime.LocalDateTime;
        }

        public static async Task<LongRunningOperationResultResponse> PublishRunbook(AutomationRunbook runbook, AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            RunbookDraftPublishParameters publishParams = new RunbookDraftPublishParameters
            {
                Name = runbook.Name,
                PublishedBy = "ISE User: " + System.Security.Principal.WindowsIdentity.GetCurrent().Name
            };
            LongRunningOperationResultResponse resultResponse = await automationManagementClient.RunbookDraft.PublishAsync(resourceGroupName, accountName, publishParams);
            return resultResponse;
        }

        public static async Task DownloadRunbook(AutomationRunbook runbook, AutomationManagementClient automationManagementClient, string workspace, string resourceGroupName, AutomationAccount account)
        {
            RunbookGetResponse response = await automationManagementClient.Runbooks.GetAsync(resourceGroupName, account.Name, runbook.Name);
            RunbookDraftGetResponse draftResponse = null;
            RunbookContentResponse runbookContentResponse = null;
            if (response.Runbook.Properties.State == "Published")
            {
                runbookContentResponse = await automationManagementClient.Runbooks.ContentAsync(resourceGroupName, account.Name, runbook.Name);
            }
            else
            {
                runbookContentResponse = await automationManagementClient.RunbookDraft.ContentAsync(resourceGroupName, account.Name, runbook.Name);
                draftResponse = await automationManagementClient.RunbookDraft.GetAsync(resourceGroupName, account.Name, runbook.Name);
            }
            String runbookFilePath = System.IO.Path.Combine(workspace, runbook.Name + ".ps1");
            File.WriteAllText(runbookFilePath, runbookContentResponse.Stream.ToString());
            runbook.localFileInfo = new FileInfo(runbookFilePath);
            /* This is the only way I can see to "check out" the runbook using the SDK.
             * Hopefully there's a better way but for now this works */
            if (response.Runbook.Properties.State == "Published")
            {
                await UploadRunbookAsDraft(runbook, automationManagementClient, resourceGroupName, account);
                draftResponse = await automationManagementClient.RunbookDraft.GetAsync(resourceGroupName, account.Name, runbook.Name);
            }
            /* Ensures the correct sync status is detected */
            if (draftResponse != null)
            {
                runbook.localFileInfo.LastWriteTime = draftResponse.RunbookDraft.LastModifiedTime.LocalDateTime;
                runbook.LastModifiedLocal = draftResponse.RunbookDraft.LastModifiedTime.LocalDateTime;
                runbook.LastModifiedCloud = draftResponse.RunbookDraft.LastModifiedTime.LocalDateTime;
            }
        }

        public static async Task<ISet<AutomationRunbook>> GetAllRunbookMetadata(AutomationManagementClient automationManagementClient, string workspace, string resourceGroupName, string accountName)
        {
            ISet<AutomationRunbook> result = new SortedSet<AutomationRunbook>();
            IList<Runbook> cloudRunbooks = await DownloadRunbookMetadata(automationManagementClient, resourceGroupName, accountName);
            
            /* Create a Dictionary of (filename, filepath) tuples found on disk. This will come in handy */
            Dictionary<string, string> filePathForRunbook = new Dictionary<string, string>();
            if (Directory.Exists(workspace))
            {
                string[] localRunbookFilePaths = Directory.GetFiles(workspace, "*.ps1");
                foreach (string path in localRunbookFilePaths)
                {
                    filePathForRunbook.Add(System.IO.Path.GetFileNameWithoutExtension(path), path);
                }
            }
            /* Start by checking the downloaded runbooks */
            foreach (Runbook cloudRunbook in cloudRunbooks)
            {
                /* Don't bother with graphical runbooks, since the ISE can't do anything with them */
                if (cloudRunbook.Properties.RunbookType != Constants.RunbookType.Graphical)
                {
                    RunbookDraftGetResponse draftResponse;
                    try
                    {
                        draftResponse = await automationManagementClient.RunbookDraft.GetAsync(resourceGroupName, accountName, cloudRunbook.Name);
                    }
                    catch
                    {
                        draftResponse = null;
                    }
                    if (filePathForRunbook.ContainsKey(cloudRunbook.Name))
                    {
                        result.Add(new AutomationRunbook(new FileInfo(filePathForRunbook[cloudRunbook.Name]), cloudRunbook, draftResponse.RunbookDraft));
                    }
                    else
                    {
                        result.Add(new AutomationRunbook(cloudRunbook, draftResponse.RunbookDraft));
                    }
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
        
        public static async Task<RunbookDraft> GetRunbookDraft(string runbookName, AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            RunbookDraftGetResponse response = await automationManagementClient.RunbookDraft.GetAsync(resourceGroupName, accountName, runbookName);
            return response.RunbookDraft;
        }

        private static async Task<IList<Runbook>> DownloadRunbookMetadata(AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            IList<Runbook> runbooks = new List<Runbook>();
            RunbookListResponse cloudRunbooks = await automationManagementClient.Runbooks.ListAsync(resourceGroupName, accountName);
            foreach (var runbook in cloudRunbooks.Runbooks)
            {
                runbooks.Add(runbook);
            }

            while (cloudRunbooks.NextLink != null)
            {
                cloudRunbooks = await automationManagementClient.Runbooks.ListNextAsync(cloudRunbooks.NextLink);
                foreach (var runbook in cloudRunbooks.Runbooks)
                {
                    runbooks.Add(runbook);
                }
            }
            return runbooks;
        }
    }
}
