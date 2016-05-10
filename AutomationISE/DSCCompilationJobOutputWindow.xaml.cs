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
using Microsoft.Azure.Management.Automation;
using System.IO;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using Newtonsoft.Json;
using System.Collections;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for DSCCompilationJobOutputWindow.xaml
    /// </summary>
    public partial class DSCCompilationJobOutputWindow : Window
    {
        private AutomationISEClient iseClient;
        private String configurationName;
        private System.Timers.Timer refreshTimer;
        private static int TIMEOUT_MS = 30000;
        private JobStreamListParameters jobStreamParams = new JobStreamListParameters();
        private bool cancelOutput = false;
        Guid lastJobID = Guid.Empty;
        private List<String> processedStreamIDs = new List<String>();
        string[] localRunbookFilePaths;

        /* These values are the defaults for the settings visible using PS>(Get-Host).PrivateData */
        public static String ErrorForegroundColorCode = "#FFFF0000";
        public static String ErrorBackgroundColorCode = "#00FFFFFF";
        public static String WarningForegroundColorCode = "#FFFF8C00";
        public static String WarningBackgroundColorCode = "#00FFFFFF";
        public static String VerboseForegroundColorCode = "#FF00FFFF";
        public static String VerboseBackgroundColorCode = "#00FFFFFF";

        public DSCCompilationJobOutputWindow(String name, AutomationISEClient client, int refreshTimerValue)
        {
            InitializeComponent();
            CompileConfigurationButton.IsEnabled = true;
            this.Title = name + " DSC Compilation Job";
            configurationName = name;
            iseClient = client;
            Task t = checkCompilationJob(true);

            refreshTimer = new System.Timers.Timer();
            refreshTimer.Interval = refreshTimerValue;
            refreshTimer.Elapsed += new ElapsedEventHandler(refresh);
            localRunbookFilePaths = Directory.GetFiles(iseClient.currWorkspace, "*.ps1");
        }

        private async Task checkCompilationJob(bool showPreviousJobOutput = false)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);

            if (showPreviousJobOutput)
            {
                DscCompilationJobListParameters jobParams = new DscCompilationJobListParameters();

                jobParams.ConfigurationName = configurationName;
                // Look for jobs compiled in the last hour so we can show the status of the last job and also the paramaters
                jobParams.StartTime = DateTime.Now.AddMinutes(-60).ToString("o");

                DscCompilationJobListResponse listResponse = await iseClient.automationManagementClient.CompilationJobs.ListAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                                                    iseClient.currAccount.Name, jobParams, cts.Token);
                if (listResponse.DscCompilationJobs.Count > 0) lastJobID = listResponse.DscCompilationJobs.LastOrDefault().Properties.JobId;
                else return;
            }

            DscCompilationJobGetResponse response = await iseClient.automationManagementClient.CompilationJobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                                    iseClient.currAccount.Name, lastJobID, cts.Token);

            // Set cancel output to false so we show the output of this job
            cancelOutput = false;
            JobDetails.FontWeight = FontWeights.Normal;
            JobDetails.Content = configurationName + " compilation job created at " + response.DscCompilationJob.Properties.CreationTime.LocalDateTime;
            JobDetails.Content += "\r\nLast refreshed at " + DateTime.Now;
            JobStatus.Content = response.DscCompilationJob.Properties.Status;
            if (response.DscCompilationJob.Properties.Status == "Failed")
            {
                updateJobOutputTextBlockWithException(response.DscCompilationJob.Properties.Exception);
                CompileConfigurationButton.IsEnabled = true;
            }
            else if (response.DscCompilationJob.Properties.Status == "Suspended")
            {
                updateJobOutputTextBlockWithException(response.DscCompilationJob.Properties.Exception);
                CompileConfigurationButton.IsEnabled = true;
            }
            else
            { 
                cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                JobStreamListResponse jslResponse = await iseClient.automationManagementClient.JobStreams.ListAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                                                        iseClient.currAccount.Name, lastJobID, jobStreamParams, cts.Token);

                if (jslResponse.JobStreams.Count > 0)
                {
                    jobStreamParams.Time = jslResponse.JobStreams.Last().Properties.Time.UtcDateTime.ToString("o");
                }

                /* Write out each stream's output */
                foreach (JobStream stream in jslResponse.JobStreams)
                {
                    // If cancelOutput is set to true, then we should break out and stop writing output
                    if (cancelOutput) break;
                    cts = new CancellationTokenSource();
                    cts.CancelAfter(TIMEOUT_MS);
                    var jslStream = await iseClient.automationManagementClient.JobStreams.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                            iseClient.currAccount.Name, lastJobID, stream.Properties.JobStreamId, cts.Token);
                    // Current issue sending back previous streams so ensuring we have not already processed the job before outputing
                    if ((processedStreamIDs.IndexOf(stream.Properties.JobStreamId) == -1))
                    {
                        processedStreamIDs.Add(stream.Properties.JobStreamId);
                        updateJobOutputTextBlock(jslStream);
                    }
                }
                if (response.DscCompilationJob.Properties.Status == "Completed")
                {
                    CompileConfigurationButton.IsEnabled = true;
                }
                else if (response.DscCompilationJob.Properties.Status == "Stopped")
                {
                    CompileConfigurationButton.IsEnabled = true;
                }
                else if (!IsRetryStatusCode(response.StatusCode))
                {
                    CompileConfigurationButton.IsEnabled = true;
                }
                else
                {
                    CompileConfigurationButton.IsEnabled = false;
                    refreshTimer.Enabled = true;
                }
            }
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
                    t = checkCompilationJob();
                });
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Refresh Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                refreshTimer.Enabled = true;
            }
        }

        private async Task<IDictionary<string, string>> GetLastCompilationJobParams()
        {
            try {
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TIMEOUT_MS);
                IDictionary<string, string> jobParameters = null;
                IDictionary<string, string> jobParametersResult = new Dictionary<string, string>();
                if (lastJobID != Guid.Empty)
                {
                    DscCompilationJobGetResponse response = await iseClient.automationManagementClient.CompilationJobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                        iseClient.currAccount.Name, lastJobID, cts.Token);
                    jobParameters = response.DscCompilationJob.Properties.Parameters;
                    foreach (var param in jobParameters)
                    {
                        jobParametersResult.Add(param.Key.ToString(), param.Value);
                    }
                    return jobParametersResult;
                }
                else return null;
            }
            catch
            {
                return null;
            }
        }

        private async void CompileConfigurationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CompileConfigurationButton.IsEnabled = false;
                processedStreamIDs.Clear();
                cancelOutput = true;
                refreshTimer.Stop();
                DscCompilationJobCreateResponse response = await createDSCJob();
                if (response != null)
                {
                    OutputTextBlockParagraph.Inlines.Clear();
                    JobDetails.FontWeight = FontWeights.Regular;
                    JobDetails.Content = configurationName + " compilation job created at " + response.DscCompilationJob.Properties.CreationTime.LocalDateTime;
                    JobStatus.Content = response.DscCompilationJob.Properties.Status;
                }
                else
                {
                    CompileConfigurationButton.IsEnabled = true;
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Compilation Job Start Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                refreshTimer.Start();
            }
        }
        private async Task<DscCompilationJobCreateResponse> createDSCJob()
        {
            DscConfiguration draft = await AutomationDSCManager.GetConfigurationDraft(configurationName, iseClient.automationManagementClient,
                            iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);

            Dictionary<string, string> filePathForRunbook = new Dictionary<string, string>();
            if (Directory.Exists(iseClient.currWorkspace))
            {
                foreach (string path in localRunbookFilePaths)
                {
                    if (path.EndsWith(Constants.nodeConfigurationIdentifier + ".ps1"))
                        filePathForRunbook.Add(System.IO.Path.GetFileNameWithoutExtension(path), path);
                }
            }


            var jobCreationParams = new DscCompilationJobCreateParameters()
            {
                Properties = new DscCompilationJobCreateProperties()
                {
                    Configuration = new DscConfigurationAssociationProperty()
                    {
                        Name = configurationName
                    },
                    Parameters = null
                }
            };

            jobCreationParams.Name = configurationName;
            if ((draft.Properties.Parameters.Count > 0) || filePathForRunbook.Count > 0)
            {
                /* User needs to specify some things */
                var existingParams = await GetLastCompilationJobParams();
                DSCConfigurationParamDialog paramDialog = new DSCConfigurationParamDialog(draft.Properties.Parameters, existingParams,filePathForRunbook);
                string configData = null;

                if (paramDialog.ShowDialog() == true)
                {
                    if (!String.IsNullOrEmpty(paramDialog.configDataSelection) && !paramDialog.configDataSelection.Equals("None"))
                    {
                        configData = getConfigurationData(paramDialog.configDataSelection);
                    }
                    jobCreationParams.Properties.Parameters = GetDSCParameters(paramDialog.paramValues, configData);
                }
                else
                {
                    return null;
                }
            }
            /* start the compilation job */
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TIMEOUT_MS);
            DscCompilationJobCreateResponse jobResponse = await iseClient.automationManagementClient.CompilationJobs.CreateAsync(
                            iseClient.accountResourceGroups[iseClient.currAccount].Name,
                            iseClient.currAccount.Name, jobCreationParams, cts.Token);
            if (jobResponse == null || jobResponse.StatusCode != System.Net.HttpStatusCode.Created)
                throw new Exception("The DSC compilation job could not be created: received HTTP status code " + jobResponse.StatusCode);
            lastJobID = jobResponse.DscCompilationJob.Properties.JobId;
            return jobResponse;
        }


        private IDictionary<string, string> GetDSCParameters(IDictionary<string, string> parameters, string configurationData)
        {
            parameters = parameters ?? new Dictionary<string, string>();
            if (configurationData != null)
            {
                parameters.Add("ConfigurationData", configurationData);
            }
            return parameters;
        }
        private string getConfigurationData(string configDataFile)
        {
            string scriptText = null;
            if (Directory.Exists(iseClient.currWorkspace))
            {
                foreach (string path in localRunbookFilePaths)
                {
                    if (path.EndsWith(configDataFile + ".ps1"))
                        scriptText = File.ReadAllText(path);
                }
            }

            Runspace runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();
            Pipeline pipeline = runspace.CreatePipeline();
            pipeline.Commands.AddScript(scriptText);
            Collection<PSObject> results = pipeline.Invoke();
            runspace.Close();

            var configurationData = JsonConvert.SerializeObject(results.First().ImmediateBaseObject);
            return configurationData;
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
