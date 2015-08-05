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
            OutputTextBox.Text = "Job created with ID: " + jobCreateResponse.RequestId;
            OutputTextBox.Text += "\r\nWaiting for updates...";
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

            this.Dispatcher.Invoke((Action)(() =>
            {
                OutputTextBox.Text += "\r\nStatus: " + response.TestJob.Status;
                if(response.TestJob.StatusDetails != "None")
                    OutputTextBox.Text += "\r\n\tDetails: " + response.TestJob.StatusDetails;
            }));
        }
        private void RefreshJobButton_Click(object sender, RoutedEventArgs e)
        {
            checkJob(sender, null);
        }
    }
}
