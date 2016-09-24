using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
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
        private static int TIMEOUT_MS = 30000;

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

            // Get current properties if is not a new runbook and set these on the draft also so they are preserved.
            RunbookGetResponse response = null;
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            if (runbook.SyncStatus != AutomationAuthoringItem.Constants.SyncStatus.LocalOnly)
            {
                response = await automationManagementClient.Runbooks.GetAsync(resourceGroupName, account.Name, runbook.Name, cts.Token);
            }

            // Create draft properties
            RunbookCreateOrUpdateDraftParameters draftParams = new RunbookCreateOrUpdateDraftParameters(draftProperties);
            draftParams.Name = runbook.Name;
            draftParams.Location = account.Location;

            // If this is not a new runbook, set the existing properties of the runbook
            if (response != null)
            {
                draftParams.Tags = response.Runbook.Tags;
                draftParams.Properties.LogProgress = response.Runbook.Properties.LogProgress;
                draftParams.Properties.LogVerbose = response.Runbook.Properties.LogVerbose;
                draftProperties.Description = response.Runbook.Properties.Description;
                draftProperties.RunbookType = response.Runbook.Properties.RunbookType;
            }
            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            await automationManagementClient.Runbooks.CreateOrUpdateWithDraftAsync(resourceGroupName, account.Name, draftParams, cts.Token);
            /* Update the runbook content from .ps1 file */

            RunbookDraftUpdateParameters draftUpdateParams = new RunbookDraftUpdateParameters()
            {
                Name = runbook.Name,
                Stream = PSScriptText
            };
            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            await automationManagementClient.RunbookDraft.UpdateAsync(resourceGroupName, account.Name, draftUpdateParams, cts.Token);
            /* Ensure the correct sync status is detected */
            RunbookDraft draft = await GetRunbookDraft(runbook.Name, automationManagementClient, resourceGroupName, account.Name);
            runbook.localFileInfo.LastWriteTime = draft.LastModifiedTime.LocalDateTime;
            runbook.LastModifiedLocal = draft.LastModifiedTime.LocalDateTime;
            runbook.LastModifiedCloud = draft.LastModifiedTime.LocalDateTime;
        }

        /* This is the only way I can see to "check out" a runbook (get it from Published to Edit state) using the SDK. */
        public static async Task CheckOutRunbook(AutomationRunbook runbook, AutomationManagementClient automationManagementClient, string resourceGroupName, AutomationAccount account)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            RunbookGetResponse response = await automationManagementClient.Runbooks.GetAsync(resourceGroupName, account.Name, runbook.Name, cts.Token);
            if (response.Runbook.Properties.State != "Published")
                return;
            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            RunbookContentResponse runbookContentResponse = await automationManagementClient.Runbooks.ContentAsync(resourceGroupName, account.Name, runbook.Name, cts.Token);
            // Create draft properties
            RunbookCreateOrUpdateDraftParameters draftParams = new RunbookCreateOrUpdateDraftParameters();
            draftParams.Properties = new RunbookCreateOrUpdateDraftProperties();
            draftParams.Properties.Description = response.Runbook.Properties.Description;
            draftParams.Properties.LogProgress = response.Runbook.Properties.LogProgress;
            draftParams.Properties.LogVerbose = response.Runbook.Properties.LogVerbose;
            draftParams.Properties.RunbookType = response.Runbook.Properties.RunbookType;
            draftParams.Properties.Draft = new RunbookDraft();
            draftParams.Tags = response.Runbook.Tags;
            draftParams.Name = runbook.Name;
            draftParams.Location = account.Location;

            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            await automationManagementClient.Runbooks.CreateOrUpdateWithDraftAsync(resourceGroupName, account.Name, draftParams, cts.Token);
            RunbookDraftUpdateParameters draftUpdateParams = new RunbookDraftUpdateParameters()
            {
                Name = runbook.Name,
                Stream = runbookContentResponse.Stream.ToString()
            };
            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            await automationManagementClient.RunbookDraft.UpdateAsync(resourceGroupName, account.Name, draftUpdateParams, cts.Token);
            /* Ensure the correct sync status is detected */
            if (runbook.localFileInfo != null)
            {
                RunbookDraft draft = await GetRunbookDraft(runbook.Name, automationManagementClient, resourceGroupName, account.Name);
                runbook.localFileInfo.LastWriteTime = draft.LastModifiedTime.LocalDateTime;
                runbook.LastModifiedLocal = draft.LastModifiedTime.LocalDateTime;
                runbook.LastModifiedCloud = draft.LastModifiedTime.LocalDateTime;
            }
        }

        public static async Task<LongRunningOperationResultResponse> PublishRunbook(AutomationRunbook runbook, AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            RunbookDraftPublishParameters publishParams = new RunbookDraftPublishParameters
            {
                Name = runbook.Name,
                PublishedBy = "ISE User: " + System.Security.Principal.WindowsIdentity.GetCurrent().Name
            };
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            LongRunningOperationResultResponse resultResponse = await automationManagementClient.RunbookDraft.PublishAsync(resourceGroupName, accountName, publishParams, cts.Token);
            /* Ensure the correct sync status is detected */
            if (runbook.localFileInfo != null)
            {
                cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                RunbookGetResponse response = await automationManagementClient.Runbooks.GetAsync(resourceGroupName, accountName, runbook.Name, cts.Token);
                runbook.localFileInfo.LastWriteTime = response.Runbook.Properties.LastModifiedTime.LocalDateTime;
                runbook.LastModifiedLocal = response.Runbook.Properties.LastModifiedTime.LocalDateTime;
                runbook.LastModifiedCloud = response.Runbook.Properties.LastModifiedTime.LocalDateTime;
            }
            /* Return the publish response */
            return resultResponse;
        }

        public static async Task DownloadRunbook(AutomationRunbook runbook, AutomationManagementClient automationManagementClient, string workspace, string resourceGroupName, AutomationAccount account)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            RunbookGetResponse response = await automationManagementClient.Runbooks.GetAsync(resourceGroupName, account.Name, runbook.Name, cts.Token);
            RunbookDraftGetResponse draftResponse = null;
            RunbookContentResponse runbookContentResponse = null;
            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            if (response.Runbook.Properties.State == "Published")
            {
                runbookContentResponse = await automationManagementClient.Runbooks.ContentAsync(resourceGroupName, account.Name, runbook.Name, cts.Token);
            }
            else
            {
                runbookContentResponse = await automationManagementClient.RunbookDraft.ContentAsync(resourceGroupName, account.Name, runbook.Name, cts.Token);
                cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                draftResponse = await automationManagementClient.RunbookDraft.GetAsync(resourceGroupName, account.Name, runbook.Name, cts.Token);
            }
            String runbookFilePath = System.IO.Path.Combine(workspace, runbook.Name + ".ps1");
            File.WriteAllText(runbookFilePath, runbookContentResponse.Stream.ToString(),Encoding.UTF8);
            runbook.AuthoringState = AutomationRunbook.AuthoringStates.InEdit;
            runbook.localFileInfo = new FileInfo(runbookFilePath);
            /* This is the only way I can see to "check out" the runbook using the SDK.
             * Hopefully there's a better way but for now this works */
            if (response.Runbook.Properties.State == "Published")
            {
                await UploadRunbookAsDraft(runbook, automationManagementClient, resourceGroupName, account);
                cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                draftResponse = await automationManagementClient.RunbookDraft.GetAsync(resourceGroupName, account.Name, runbook.Name, cts.Token);
            }
            /* Ensures the correct sync status is detected */
            if (draftResponse != null)
            {
                runbook.localFileInfo.LastWriteTime = draftResponse.RunbookDraft.LastModifiedTime.LocalDateTime;
                runbook.LastModifiedLocal = draftResponse.RunbookDraft.LastModifiedTime.LocalDateTime;
                runbook.LastModifiedCloud = draftResponse.RunbookDraft.LastModifiedTime.LocalDateTime;
            }
        }

        public static async Task<ISet<AutomationRunbook>> GetAllRunbookMetadata(AutomationManagementClient automationManagementClient, string workspace, string resourceGroupName, string accountName, Dictionary<string, string> localScriptsParsed)
        {
            ISet<AutomationRunbook> result = new SortedSet<AutomationRunbook>();            
            IList<Runbook> cloudRunbooks = await DownloadRunbookMetadata(automationManagementClient, resourceGroupName, accountName);

            /* Create a Dictionary of (filename, filepath) tuples found on disk. This will come in handy */
            Dictionary<string, string> filePathForRunbook = new Dictionary<string, string>();
            if (localScriptsParsed != null)
            {
                foreach (string path in localScriptsParsed.Keys)
                {
                    if (localScriptsParsed[path] == ("script"))
                        filePathForRunbook.Add(System.IO.Path.GetFileNameWithoutExtension(path), path);
                }
            }
            /* Start by checking the downloaded runbooks */
            foreach (Runbook cloudRunbook in cloudRunbooks)
            {
                /* Don't bother with graphical runbooks, since the ISE can't do anything with them */
                if (cloudRunbook.Properties.RunbookType != Constants.RunbookType.Graphical && cloudRunbook.Properties.RunbookType != Constants.RunbookType.GraphPowerShell)
                {
                    RunbookDraftGetResponse draftResponse;
                    try
                    {
                        CancellationTokenSource cts = new CancellationTokenSource();
                        cts.CancelAfter(TIMEOUT_MS);
                        draftResponse = await automationManagementClient.RunbookDraft.GetAsync(resourceGroupName, accountName, cloudRunbook.Name, cts.Token);
                    }
                    catch
                    {
                        draftResponse = null;
                        continue;
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
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            RunbookDraftGetResponse response = await automationManagementClient.RunbookDraft.GetAsync(resourceGroupName, accountName, runbookName, cts.Token);
            return response.RunbookDraft;
        }

        private static async Task<IList<Runbook>> DownloadRunbookMetadata(AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            IList<Runbook> runbooks = new List<Runbook>();
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            RunbookListResponse cloudRunbooks = await automationManagementClient.Runbooks.ListAsync(resourceGroupName, accountName, cts.Token);
            foreach (var runbook in cloudRunbooks.Runbooks)
            {
                runbooks.Add(runbook);
            }

            while (cloudRunbooks.NextLink != null)
            {
                cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                cloudRunbooks = await automationManagementClient.Runbooks.ListNextAsync(cloudRunbooks.NextLink, cts.Token);
                foreach (var runbook in cloudRunbooks.Runbooks)
                {
                    runbooks.Add(runbook);
                }
            }
            return runbooks;
        }

        public static void CreateLocalRunbook(string runbookName, string workspace, string runbookType)
        {
            String runbookFilePath = System.IO.Path.Combine(workspace, runbookName + ".ps1");
            if (File.Exists(runbookFilePath))
                throw new Exception("A runbook with that name already exists");

            // Create the file with a UTF8 Byte Order Mark
            using (FileStream stream = new FileStream(runbookFilePath, FileMode.Create))
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    writer.Write(Encoding.UTF8.GetPreamble());
                }
            }

            if (runbookType.Equals(Constants.RunbookType.Workflow))
            {
                File.WriteAllText(runbookFilePath, "workflow " + runbookName + "\r\n{\r\n}",Encoding.UTF8);
            }
            else if (runbookType.Equals(Constants.RunbookType.PowerShellScript))
            {
                File.WriteAllText(runbookFilePath, " ",Encoding.UTF8);
            }
            else
            {
                throw new Exception("Cannot create local runbook of type " + runbookType);
            }
        }
        public static void DeleteLocalRunbook(AutomationRunbook runbook)
        {
            File.Delete(runbook.localFileInfo.FullName);
        }

        public static async Task DeleteCloudRunbook(AutomationRunbook runbook, AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            await automationManagementClient.Runbooks.DeleteAsync(resourceGroupName, accountName, runbook.Name, cts.Token);
        }
    }
}
