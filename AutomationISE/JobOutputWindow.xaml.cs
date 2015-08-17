using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using Microsoft.Azure.Management.Automation.Models;
using AutomationISE.Model;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for TestJobOutputWindow.xaml
    /// </summary>
    public partial class JobOutputWindow : Window
    {
        private TestJobCreateResponse TestjobCreateResponse = null;
        private JobCreateResponse jobCreateResponse = null;

        private AutomationISEClient iseClient;
        private String runbookName;
        /* These values are the defaults for the settings visible using >(Get-Host).PrivateData */
        public static String ErrorForegroundColorCode = "#FFFF0000";
        public static String ErrorBackgroundColorCode = "#00FFFFFF";
        public static String WarningForegroundColorCode = "#FFFF8C00";
        public static String WarningBackgroundColorCode = "#00FFFFFF";
        public static String VerboseForegroundColorCode = "#FF00FFFF";
        public static String VerboseBackgroundColorCode = "#00FFFFFF";

        public JobOutputWindow(String name, TestJobCreateResponse response, AutomationISEClient client)
        {
            InitializeComponent();
            runbookName = name;
            TestjobCreateResponse = response;
            iseClient = client;
            OutputTextBlockParagraph.Inlines.Add("Test job created at " + TestjobCreateResponse.TestJob.CreationTime + " for runbook " + runbookName);
            OutputTextBlockParagraph.Inlines.Add("\r\nTip: not seeing Verbose output? Add the line \"$VerbosePreference='Continue'\" to your runbook.");
            OutputTextBlockParagraph.Inlines.Add("\r\nClick 'Refresh' to check for updates.");
        }

        public JobOutputWindow(String name, JobCreateResponse response, AutomationISEClient client)
        {
            InitializeComponent();
            runbookName = name;
            jobCreateResponse = response;
            iseClient = client;
            OutputTextBlockParagraph.Inlines.Add("Job created at " + jobCreateResponse.Job.Properties.CreationTime + " for Runbook " + runbookName);
            OutputTextBlockParagraph.Inlines.Add("\r\nTip: not seeing Verbose output? Add the line \"$VerbosePreference='Continue'\" to your runbook.");
            OutputTextBlockParagraph.Inlines.Add("\r\nClick 'Refresh' to check for updates.");
        }

        private async Task checkTestJob()
        {
            TestJobGetResponse response = await iseClient.automationManagementClient.TestJobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                                                iseClient.currAccount.Name, runbookName, new System.Threading.CancellationToken());

            OutputTextBlockParagraph.Inlines.Add("\r\nStatus: " + response.TestJob.Status);
            if (response.TestJob.Status == "Failed")
            {
                updateJobOutputTextBlockWithException(response.TestJob.Exception);
            }
            else
            {
                JobStreamListResponse jslResponse = await iseClient.automationManagementClient.JobStreams.ListTestJobStreamsAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                    iseClient.currAccount.Name, runbookName, null, new System.Threading.CancellationToken());

                // Write out each stream output
                foreach (JobStream stream in jslResponse.JobStreams)
                {
                    var jslStream = await iseClient.automationManagementClient.JobStreams.GetTestJobStreamAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                            iseClient.currAccount.Name, runbookName, stream.Properties.JobStreamId, new System.Threading.CancellationToken());
                    updateJobOutputTextBlock(jslStream);
                }
                if (response.TestJob.Status == "Suspended")
                {
                    updateJobOutputTextBlockWithException(response.TestJob.Exception);
                }
            }
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

        private async Task checkJob()
        {
            JobGetResponse response = await iseClient.automationManagementClient.Jobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                                                iseClient.currAccount.Name, jobCreateResponse.Job.Properties.JobId, new System.Threading.CancellationToken());

            OutputTextBlockParagraph.Inlines.Add("\r\nStatus: " + response.Job.Properties.Status);

            JobStreamListResponse jslResponse = await iseClient.automationManagementClient.JobStreams.ListAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                iseClient.currAccount.Name, jobCreateResponse.Job.Properties.JobId, null, new System.Threading.CancellationToken());

            // Write out each stream output
            foreach (JobStream stream in jslResponse.JobStreams)
            {
                var jslStream = await iseClient.automationManagementClient.JobStreams.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                        iseClient.currAccount.Name, jobCreateResponse.Job.Properties.JobId, stream.Properties.JobStreamId, new System.Threading.CancellationToken());
                updateJobOutputTextBlock(jslStream);
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
        }

        private async void RefreshJobButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshJobButton.IsEnabled = false;
                RefreshJobButton.Content = "Refreshing...";
                if (jobCreateResponse != null) await checkJob();
                else await checkTestJob();
                RefreshJobButton.IsEnabled = true;
                RefreshJobButton.Content = "Refresh";
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Error");
                return;
            }
        }
    }
}
