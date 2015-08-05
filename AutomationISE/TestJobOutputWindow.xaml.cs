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

        public TestJobOutputWindow(String name, TestJobCreateResponse response, AutomationISEClient client)
        {
            InitializeComponent();
            runbookName = name;
            jobCreateResponse = response;
            iseClient = client;
            OutputTextBlock.Inlines.Add("Test job created at " + jobCreateResponse.TestJob.CreationTime);
            OutputTextBlock.Inlines.Add("\r\nTip: not seeing Verbose output? Add the line \"$VerbosePreference='Continue'\" to your runbook.");
            OutputTextBlock.Inlines.Add("\r\nClick 'Refresh' to check for updates.");
            //beginPolling();
        }

        private void beginPolling()
        {
            Timer timer = new Timer();
            /* Interval in milliseconds */
            timer.Interval = 3000;
            timer.Elapsed += new ElapsedEventHandler(checkJob);
            timer.Start();
        }
        private async void checkJob(object source, ElapsedEventArgs e)
        {
            TestJobGetResponse response = await iseClient.automationManagementClient.TestJobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name, 
                iseClient.currAccount.Name, runbookName, new System.Threading.CancellationToken());

            JobStreamListParameters jslParams = new JobStreamListParameters();

            JobStreamListResponse jslResponse = await iseClient.automationManagementClient.JobStreams.ListTestJobStreamsAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                iseClient.currAccount.Name, runbookName, null, new System.Threading.CancellationToken());

            this.Dispatcher.Invoke((Action)(() =>
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
                        OutputTextBlock.Inlines.Add(new Run(streamText) { 
                            Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF00FFFF")),
                            Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#00FFFFFF"))
                        });
                    }
                    else if (stream.Properties.StreamType == "Error")
                    {
                        streamText = "ERROR: " + streamText;
                        OutputTextBlock.Inlines.Add(new Run(streamText)
                        {
                            Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF0000")),
                            Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#00FFFFFF"))
                        });
                    }
                    else if (stream.Properties.StreamType == "Warning")
                    {
                        streamText = "WARNING: " + streamText;
                        OutputTextBlock.Inlines.Add(new Run(streamText)
                        {
                            Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF8C00")),
                            Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#00FFFFFF"))
                        });
                    }
                    else
                    {
                        Debug.WriteLine("Unknown stream type couldn't be colored properly: " + stream.Properties.StreamType);
                        OutputTextBlock.Inlines.Add(stream.Properties.StreamType.ToUpper() + ":  " + streamText);
                    }
                }
            }));
        }
        private void RefreshJobButton_Click(object sender, RoutedEventArgs e)
        {
            checkJob(sender, null);
        }
    }
}
/*
 * Relevant data from $ (get-host).PrivateData:
FontName                                  : Lucida Console
ErrorForegroundColor                      : #FFFF0000
ErrorBackgroundColor                      : #00FFFFFF
WarningForegroundColor                    : #FFFF8C00
WarningBackgroundColor                    : #00FFFFFF
VerboseForegroundColor                    : #FF00FFFF
VerboseBackgroundColor                    : #00FFFFFF
DebugForegroundColor                      : #FF00FFFF
DebugBackgroundColor                      : #00FFFFFF
ConsolePaneBackgroundColor                : #FF012456
ConsolePaneTextBackgroundColor            : #FF012456
ConsolePaneForegroundColor                : #FFF5F5F5
ScriptPaneBackgroundColor                 : #FFFFFFFF
ScriptPaneForegroundColor                 : #FF000000
 * 
 */
