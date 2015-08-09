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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.Timers;
using System.Diagnostics;
using Microsoft.Azure.Management.Automation.Models;
using AutomationISE.Model;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for TestJobOutputWindow.xaml
    /// </summary>
    public partial class TestJobOutputWindow : Window
    {
        private TestJobCreateResponse jobCreateResponse;
        private AutomationISEClient iseClient;
        private String runbookName;
        /* These values are the defaults for the settings visible using > (Get-Host).PrivateData */
        public static String ErrorForegroundColorCode = "#FFFF0000";
        public static String ErrorBackgroundColorCode = "#00FFFFFF";
        public static String WarningForegroundColorCode = "#FFFF8C00";
        public static String WarningBackgroundColorCode = "#00FFFFFF";
        public static String VerboseForegroundColorCode = "#FF00FFFF";
        public static String VerboseBackgroundColorCode = "#00FFFFFF";

        public TestJobOutputWindow(String name, TestJobCreateResponse response, AutomationISEClient client)
        {
            InitializeComponent();
            runbookName = name;
            jobCreateResponse = response;
            iseClient = client;
            OutputTextBlock.Inlines.Add("Test job created at " + jobCreateResponse.TestJob.CreationTime);
            OutputTextBlock.Inlines.Add("\r\nTip: not seeing Verbose output? Add the line \"$VerbosePreference='Continue'\" to your runbook.");
            OutputTextBlock.Inlines.Add("\r\nClick 'Refresh' to check for updates.");
        }

        private async Task checkJob()
        {
            TestJobGetResponse response = await iseClient.automationManagementClient.TestJobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name, 
                iseClient.currAccount.Name, runbookName, new System.Threading.CancellationToken());
            JobStreamListResponse jslResponse = await iseClient.automationManagementClient.JobStreams.ListTestJobStreamsAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                iseClient.currAccount.Name, runbookName, null, new System.Threading.CancellationToken());
            updateJobOutputTextBlock(response, jslResponse);
        }

        private void updateJobOutputTextBlock(TestJobGetResponse response, JobStreamListResponse jslResponse)
        {
            OutputTextBlock.Inlines.Add("\r\nStatus: " + response.TestJob.Status);
            if (response.TestJob.StatusDetails != "None")
                OutputTextBlock.Inlines.Add("\r\n\tDetails: " + response.TestJob.StatusDetails);
            foreach (JobStream stream in jslResponse.JobStreams)
            {
                /* Form of the 'summary' string, which is really the text we're interested in: [guid]:[hostname in brackets]:[text entered by user] */
                /* Strip this down to just the text entered by the user: */
                int secondColonIndex = stream.Properties.Summary.IndexOf(':', stream.Properties.Summary.IndexOf(':') + 1);
                String streamText = stream.Properties.Summary.Substring(secondColonIndex + 1);
                OutputTextBlock.Inlines.Add("\r\n");
                if (stream.Properties.StreamType == "Output")
                {
                    OutputTextBlock.Inlines.Add(streamText);
                }
                else if (stream.Properties.StreamType == "Verbose")
                {
                    streamText = "VERBOSE: " + streamText;
                    OutputTextBlock.Inlines.Add(new Run(streamText)
                    {
                        Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(VerboseForegroundColorCode)),
                        Background = (SolidColorBrush)(new BrushConverter().ConvertFrom(VerboseBackgroundColorCode))
                    });
                }
                else if (stream.Properties.StreamType == "Error")
                {
                    streamText = "ERROR: " + streamText;
                    OutputTextBlock.Inlines.Add(new Run(streamText)
                    {
                        Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(ErrorForegroundColorCode)),
                        Background = (SolidColorBrush)(new BrushConverter().ConvertFrom(ErrorBackgroundColorCode))
                    });
                }
                else if (stream.Properties.StreamType == "Warning")
                {
                    streamText = "WARNING: " + streamText;
                    OutputTextBlock.Inlines.Add(new Run(streamText)
                    {
                        Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(WarningForegroundColorCode)),
                        Background = (SolidColorBrush)(new BrushConverter().ConvertFrom(WarningBackgroundColorCode))
                    });
                }
                else
                {
                    Debug.WriteLine("Unknown stream type couldn't be colored properly: " + stream.Properties.StreamType);
                    OutputTextBlock.Inlines.Add(stream.Properties.StreamType.ToUpper() + ":  " + streamText);
                }
            }
        }

        private async void RefreshJobButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshJobButton.IsEnabled = false;
                RefreshJobButton.Content = "Refreshing...";
                await checkJob();
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
