using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Diagnostics;
using System.Net;
using System.Timers;
using Microsoft.Azure.Management.Automation.Models;
using AutomationISE.Model;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for TestJobOutputWindow.xaml
    /// </summary>
    public partial class JobOutputWindow : Window
    {
        private JobCreateResponse jobCreateResponse = null;
        private AutomationISEClient iseClient;
        private String runbookName;
        private System.Timers.Timer refreshTimer;
        private static int TIMEOUT_MS = 30000;
        private JobStreamListParameters jobParams = new JobStreamListParameters();
        private bool cancelOutput = false;

        /* These values are the defaults for the settings visible using PS>(Get-Host).PrivateData */
        public static String ErrorForegroundColorCode = "#FFFF0000";
        public static String ErrorBackgroundColorCode = "#00FFFFFF";
        public static String WarningForegroundColorCode = "#FFFF8C00";
        public static String WarningBackgroundColorCode = "#00FFFFFF";
        public static String VerboseForegroundColorCode = "#FF00FFFF";
        public static String VerboseBackgroundColorCode = "#00FFFFFF";

        public JobOutputWindow(String name, AutomationISEClient client, int refreshTimerValue)
        {
            InitializeComponent();
            StartJobButton.IsEnabled = true;
            StopJobButton.IsEnabled = false;
            this.Title = name + " Test Job";
            AdditionalInformation.Text = "Tip: not seeing Verbose output? Add the line \"$VerbosePreference='Continue'\" to your runbook.";
            runbookName = name;
            iseClient = client;
            jobParams.Time = DateTime.UtcNow.AddDays(-30).ToString("o");
            Task t = checkTestJob(true);

            refreshTimer = new System.Timers.Timer();
            refreshTimer.Interval = refreshTimerValue;
            refreshTimer.Elapsed += new ElapsedEventHandler(refresh);
        }

        //TODO: refactor this to a different class with some inheritance structure
        public JobOutputWindow(String name, JobCreateResponse response, AutomationISEClient client, int refreshTimerValue)
        {
            InitializeComponent();
            StartJobButton.IsEnabled = true;
            StopJobButton.IsEnabled = false;
            this.Title = "Job: " + name;
            AdditionalInformation.Text = "This is a Global Runbook responsible for syncing your GitHub repo with your Automation Account. Neato!";
            runbookName = name;
            jobCreateResponse = response;
            iseClient = client;
            jobParams.Time = DateTime.UtcNow.AddDays(-30).ToString("o");
            Task t = checkJob();
            refreshTimer = new System.Timers.Timer();
            refreshTimer.Interval = refreshTimerValue;
            refreshTimer.Elapsed += new ElapsedEventHandler(refresh);
        }

        private async Task checkTestJob(bool showWarning = false)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            TestJobGetResponse response = await iseClient.automationManagementClient.TestJobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                                                iseClient.currAccount.Name, runbookName, cts.Token);
            if (showWarning)
            {
                JobDetails.FontWeight = FontWeights.Bold;
                JobDetails.Content = "This is a past test job for " + runbookName + " created at " + response.TestJob.CreationTime.LocalDateTime;
         //       StartJobButton.IsEnabled = false;
            }
            else
            {
                JobDetails.FontWeight = FontWeights.Normal;
                JobDetails.Content = runbookName + " test job created at " + response.TestJob.CreationTime.LocalDateTime;
            }
            JobDetails.Content += "\r\nLast refreshed at " + DateTime.Now;
            JobStatus.Content = response.TestJob.Status;
            if (response.TestJob.Status == "Failed")
            {
                updateJobOutputTextBlockWithException(response.TestJob.Exception);
                StartJobButton.IsEnabled = true;
                StopJobButton.IsEnabled = false;
            }
            else
            { 
                cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                JobStreamListResponse jslResponse = await iseClient.automationManagementClient.JobStreams.ListTestJobStreamsAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                    iseClient.currAccount.Name, runbookName, jobParams, cts.Token);

                JobStream lastJob = null;
                if (jslResponse.JobStreams.Count > 0)
                {
                   lastJob = jslResponse.JobStreams.Last();
                   jobParams.Time = lastJob.Properties.Time.UtcDateTime.ToString("o");
                }

                /* Write out each stream's output */
                foreach (JobStream stream in jslResponse.JobStreams)
                {
                    if (cancelOutput) break;
                    cts = new CancellationTokenSource();
                    cts.CancelAfter(TIMEOUT_MS);
                    var jslStream = await iseClient.automationManagementClient.JobStreams.GetTestJobStreamAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                            iseClient.currAccount.Name, runbookName, stream.Properties.JobStreamId, cts.Token);
                    // If this is the last stream, don't show it as it was displayed already.
                    if ((stream.Properties.JobStreamId != lastJob.Properties.JobStreamId) || (response.TestJob.Status != "Running"))
                        updateJobOutputTextBlock(jslStream);
                }
                if (response.TestJob.Status == "Suspended")
                {
                    updateJobOutputTextBlockWithException(response.TestJob.Exception);
                    StartJobButton.IsEnabled = false;
                    StopJobButton.IsEnabled = true;
                }
                else if (response.TestJob.Status == "Completed")
                {
                    StartJobButton.IsEnabled = true;
                    StopJobButton.IsEnabled = false;
                }
                else if (response.TestJob.Status == "Stopped")
                {
                    StartJobButton.IsEnabled = true;
                    StopJobButton.IsEnabled = false;
                }
                else if (!IsRetryStatusCode(response.StatusCode))
                {
                    StartJobButton.IsEnabled = true;
                    StopJobButton.IsEnabled = false;
                }
                else
                {
                    StartJobButton.IsEnabled = false;
                    StopJobButton.IsEnabled = true;
                    refreshTimer.Enabled = true;
                }
            }
        }

        private async Task checkJob()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            JobGetResponse response = await iseClient.automationManagementClient.Jobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                                                iseClient.currAccount.Name, jobCreateResponse.Job.Properties.JobId, cts.Token);

            JobDetails.Content = runbookName + " test job created at " + response.Job.Properties.CreationTime.LocalDateTime;
            JobDetails.Content += "\r\nLast refreshed at " + DateTime.Now;
            JobStatus.Content = response.Job.Properties.Status;

            cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            JobStreamListResponse jslResponse = await iseClient.automationManagementClient.JobStreams.ListAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                iseClient.currAccount.Name, jobCreateResponse.Job.Properties.JobId, jobParams, cts.Token);

            foreach (JobStream stream in jslResponse.JobStreams)
            {
                cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                var jslStream = await iseClient.automationManagementClient.JobStreams.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                        iseClient.currAccount.Name, jobCreateResponse.Job.Properties.JobId, stream.Properties.JobStreamId, cts.Token);
                if (jslStream.JobStream.Properties.Time.DateTime > Convert.ToDateTime(jobParams.Time))
                {
                    jobParams.Time = stream.Properties.Time.ToString("o");
                    updateJobOutputTextBlock(jslStream);
                }
            }
            refreshTimer.Enabled = true;

        }

        private void updateJobOutputTextBlock(JobStreamGetResponse stream)
        {
            String streamText = stream.JobStream.Properties.StreamText;
            OutputTextBlockParagraph.Inlines.Add("\r\n");
            if (stream.JobStream.Properties.StreamType == "Output")
            {
                OutputTextBlockParagraph.Inlines.Add(streamText);
            }
            else if (stream.JobStream.Properties.StreamType == "Verbose")
            {
                streamText = "VERBOSE: " + streamText;
                OutputTextBlockParagraph.Inlines.Add(new Run(streamText)
                {
                    Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(VerboseForegroundColorCode)),
                    Background = (SolidColorBrush)(new BrushConverter().ConvertFrom(VerboseBackgroundColorCode))
                });
            }
            else if (stream.JobStream.Properties.StreamType == "Error")
            {
                streamText = "ERROR: " + streamText;
                OutputTextBlockParagraph.Inlines.Add(new Run(streamText)
                {
                    Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(ErrorForegroundColorCode)),
                    Background = (SolidColorBrush)(new BrushConverter().ConvertFrom(ErrorBackgroundColorCode))
                });
            }
            else if (stream.JobStream.Properties.StreamType == "Warning")
            {
                streamText = "WARNING: " + streamText;
                OutputTextBlockParagraph.Inlines.Add(new Run(streamText)
                {
                    Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(WarningForegroundColorCode)),
                    Background = (SolidColorBrush)(new BrushConverter().ConvertFrom(WarningBackgroundColorCode))
                });
            }
            else
            {
                Debug.WriteLine("Unknown stream type couldn't be colored properly: " + stream.JobStream.Properties.StreamType);
                OutputTextBlockParagraph.Inlines.Add(stream.JobStream.Properties.StreamType.ToUpper() + ":  " + streamText);
            }
            OutputTextBlock.ScrollToEnd();
        }

        private void updateJobOutputTextBlockWithException(string exceptionMessage)
        {
            OutputTextBlockParagraph.Inlines.Add("\r\n");
            OutputTextBlockParagraph.Inlines.Add(new Run(exceptionMessage)
            {
                Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(ErrorForegroundColorCode)),
                Background = (SolidColorBrush)(new BrushConverter().ConvertFrom(ErrorBackgroundColorCode))
            });
        }


        private void refresh(object source, ElapsedEventArgs e)
        {
            try
            {
                refreshTimer.Enabled = false;
                this.Dispatcher.Invoke(() =>
                {
                    Task t;
                    if (jobCreateResponse != null)
                        t = checkJob();
                    else
                        t = checkTestJob();
                });
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Refresh Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                refreshTimer.Enabled = true;
            }
        }


        private async void StopJobButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StopJobButton.IsEnabled = false;
                StopJobButton.Content = "Stopping...";
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                Microsoft.Azure.AzureOperationResponse response = await iseClient.automationManagementClient.TestJobs.StopAsync(
                    iseClient.accountResourceGroups[iseClient.currAccount].Name,
                    iseClient.currAccount.Name, runbookName, cts.Token);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    throw new Exception("The job couldn't be stopped.\r\nReceived status code: " + response.StatusCode);
                JobStatus.Content = "Submitted job stop request";
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Job Stop Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                StopJobButton.IsEnabled = true;
                StopJobButton.Content = "Stop Job";
            }
        }

        private async Task<IDictionary<string, string>> GetLastTestJobParams()
        {
            try {
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                TestJobGetResponse response = await iseClient.automationManagementClient.TestJobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                                        iseClient.currAccount.Name, runbookName, cts.Token);
                IDictionary<string, string> jobParams = response.TestJob.Parameters;
                return jobParams;
            }
            catch
            {
                // return null if test job not found.
                return null;
            }
        }

        private async void StartJobButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartJobButton.IsEnabled = false;
                refreshTimer.Stop();
                jobParams.Time = DateTime.UtcNow.AddDays(-30).ToString("o");
                TestJobCreateResponse response = await createTestJob();
                if (response != null)
                {
                    OutputTextBlockParagraph.Inlines.Clear();
                    JobDetails.FontWeight = FontWeights.Regular;
                    JobDetails.Content = runbookName + " test job created at " + response.TestJob.CreationTime.LocalDateTime;
                    JobStatus.Content = response.TestJob.Status;
                    StopJobButton.IsEnabled = true;
                }
                else
                {
                    StartJobButton.IsEnabled = true;
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Job Start Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                refreshTimer.Start();
            }
        }
        private async Task<TestJobCreateResponse> createTestJob()
        {
            RunbookDraft draft = await AutomationRunbookManager.GetRunbookDraft(runbookName, iseClient.automationManagementClient,
                            iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
            if (draft.InEdit == false)
                throw new Exception("This runbook has no draft to test because it is in a 'Published' state.");

            HybridRunbookWorkerGroupsListResponse hybridGroupResponse = await iseClient.automationManagementClient.HybridRunbookWorkerGroups.ListAsync(
                iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name,
                new CancellationToken());

            TestJobCreateParameters jobCreationParams = new TestJobCreateParameters();
            jobCreationParams.RunbookName = runbookName;
            if (draft.Parameters.Count > 0 || hybridGroupResponse.HybridRunbookWorkerGroups.Count > 0)
            {
                /* User needs to specify some things */
                var existingParams = await GetLastTestJobParams();
                RunbookParamDialog paramDialog = new RunbookParamDialog(draft.Parameters, existingParams, hybridGroupResponse.HybridRunbookWorkerGroups);
                if (paramDialog.ShowDialog() == true)
                {
                    if (draft.Parameters.Count > 0)
                        jobCreationParams.Parameters = paramDialog.paramValues;
                    if (!String.IsNullOrEmpty(paramDialog.runOnSelection) && !paramDialog.runOnSelection.Equals("Azure"))
                        jobCreationParams.RunOn = paramDialog.runOnSelection;
                }
                else
                {
                    return null;
                }
            }
            /* start the test job */
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            TestJobCreateResponse jobResponse = await iseClient.automationManagementClient.TestJobs.CreateAsync(
                            iseClient.accountResourceGroups[iseClient.currAccount].Name,
                            iseClient.currAccount.Name, jobCreationParams, cts.Token);
            if (jobResponse == null || jobResponse.StatusCode != System.Net.HttpStatusCode.Created)
                throw new Exception("The test job could not be created: received HTTP status code " + jobResponse.StatusCode);
            return jobResponse;
        }

        private static bool IsRetryStatusCode(HttpStatusCode statusCode)
        {
            switch (statusCode)
            {
                case HttpStatusCode.OK:
                case HttpStatusCode.Accepted:
                case HttpStatusCode.NoContent:
                    return true;
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                case HttpStatusCode.NotFound:
                case HttpStatusCode.InternalServerError:
                    return false;
                default:
                    return true;
            }
        }
    }
}
