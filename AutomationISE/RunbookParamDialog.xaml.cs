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

using Microsoft.Azure.Management.Automation.Models;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for RunbookParamDialog.xaml
    /// </summary>
    public partial class RunbookParamDialog : Window
    {
        private IDictionary<string, RunbookParameter> parameterDict;
        private IList<HybridRunbookWorkerGroup> hybridGroups;
        private IDictionary<string, string> existingParamsDict;
        private String existingRunOn;
        private String runbookType;
        private IDictionary<string, string> _paramValues;
        public IDictionary<string, string> paramValues { get { return _paramValues; } }
        private string _runOnSelection;
        public string runOnSelection { get { return _runOnSelection; } }

        public RunbookParamDialog(IDictionary<string, RunbookParameter> parameterDict, IDictionary<string, string> existingParamsDict, String lastRunOn, IList<HybridRunbookWorkerGroup> hybridGroups, String runbookType)
        {
            InitializeComponent();
            this.hybridGroups = hybridGroups;
            this.parameterDict = parameterDict;
            this.existingParamsDict = existingParamsDict;
            this.existingRunOn = lastRunOn;
            if (runbookType == "Python2")
            {
                this.runbookType = runbookType;
                AddPthonForms();
            }
            else
            {
                if (parameterDict.Count > 0)
                    AddParamForms();
            }
            if (hybridGroups.Count > 0)
                AddRunOnForms();
        }

        private void AddPthonForms()
        {
            /* Update the UI Grid to fit everything */
            for (int i = 0; i < 2; i++)
            {
                RowDefinition rowDef = new RowDefinition();
                rowDef.Height = System.Windows.GridLength.Auto;
                ParametersGrid.RowDefinitions.Add(rowDef);
            }
            Grid.SetRow(ButtonsPanel, ParametersGrid.RowDefinitions.Count - 1);

            Label argLabel = new Label();
            argLabel.Content = "Enter any arguments";
            Grid.SetRow(argLabel, ParametersGrid.RowDefinitions.Count - 3);
            Grid.SetColumn(argLabel, 0);
            Grid.SetColumnSpan(argLabel, 2);
            /* Input field */
            TextBox parameterValueBox = new TextBox();
            // Set previous value for this parameter if available
            String argLine = null;
            if (existingParamsDict != null)
            {
                foreach (var param in existingParamsDict.Values)
                {
                    try
                    {
                        argLine = argLine + JsonConvert.DeserializeObject(param) + " ";
                    }
                    catch
                    {
                        argLine = argLine + param + " ";
                    }
                }
                parameterValueBox.Text = argLine;
          //     var paramValue = existingParamsDict.FirstOrDefault(x => x.Key == paramName).Value;
          //      if (paramValue != null) parameterValueBox.Text = paramValue;
            }
            parameterValueBox.MinWidth = 200;
            parameterValueBox.Margin = new System.Windows.Thickness(0, 5, 5, 5);
            Grid.SetColumn(parameterValueBox, 0);
            Grid.SetRow(parameterValueBox, ParametersGrid.RowDefinitions.Count - 2);
            Grid.SetColumnSpan(parameterValueBox, 2);
            /* Add to Grid */
            ParametersGrid.Children.Add(argLabel);
            ParametersGrid.Children.Add(parameterValueBox);
        }

        [DllImport("shell32.dll", SetLastError = true)]
        static extern IntPtr CommandLineToArgvW(
        [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

        public static string[] ReturnArgs(string arguments)
        {
            int argc;
            var argv = CommandLineToArgvW(arguments, out argc);
            if (argv == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception();
            try
            {
                var args = new string[argc];
                for (var i = 0; i < args.Length; i++)
                {
                    var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    args[i] = Marshal.PtrToStringUni(p);
                }

                return args;
            }
            finally
            {
                Marshal.FreeHGlobal(argv);
            }
        }
        /*
        private static MatchCollection ReturnArgs(string arguments)
        {
            var pattern = new Regex(@"("".*?""|[^ ""]+)+");
            return pattern.Matches(arguments);
        }
        */
        private void AddRunOnForms()
        {
            /* Update the UI Grid to fit everything */
            for (int i = 0; i < 2; i++)
            {
                RowDefinition rowDef = new RowDefinition();
                rowDef.Height = System.Windows.GridLength.Auto;
                ParametersGrid.RowDefinitions.Add(rowDef);
            }
            Grid.SetRow(ButtonsPanel, ParametersGrid.RowDefinitions.Count - 1);

            Label runOnLabel = new Label();
            runOnLabel.Content = "Choose where to run the test job:";
            Grid.SetRow(runOnLabel, ParametersGrid.RowDefinitions.Count - 3);
            Grid.SetColumn(runOnLabel, 0);
            Grid.SetColumnSpan(runOnLabel, 2);

            IList<string> runOnOptions = new List<string>();
            runOnOptions.Add("Azure");
            foreach (HybridRunbookWorkerGroup group in this.hybridGroups)
            {
                runOnOptions.Add(group.Name);
            }
            ComboBox runOnComboBox = new ComboBox();
            runOnComboBox.ItemsSource = runOnOptions;
            if (existingRunOn != null)
            {
                runOnComboBox.SelectedItem = existingRunOn;
                _runOnSelection = existingRunOn;
            }
            else runOnComboBox.SelectedIndex = 0;
            runOnComboBox.SelectionChanged += changeRunOnSelection;
            Grid.SetRow(runOnComboBox, ParametersGrid.RowDefinitions.Count - 2);
            Grid.SetColumn(runOnComboBox, 0);
            Grid.SetColumnSpan(runOnComboBox, 2);

            /* Add to Grid */
            ParametersGrid.Children.Add(runOnLabel);
            ParametersGrid.Children.Add(runOnComboBox);
        }

        private void changeRunOnSelection(object sender, RoutedEventArgs e)
        {
            _runOnSelection = ((sender as ComboBox).SelectedItem as string);
        }

        private void AddParamForms()
        {
            /* Update the UI Grid to fit everything */
            for (int i = 0; i < parameterDict.Count * 2; i++)
            {
                RowDefinition rowDef = new RowDefinition();
                rowDef.Height = System.Windows.GridLength.Auto;
                ParametersGrid.RowDefinitions.Add(rowDef);
            }
            Grid.SetRow(ButtonsPanel, ParametersGrid.RowDefinitions.Count - 1);
            /* Fill the UI with parameter data */
            int count = 0;
            foreach (string paramName in parameterDict.Keys)
            {
                /* Parameter Name and Type */
                Label parameterNameLabel = new Label();
                parameterNameLabel.Content = paramName;
                Label parameterTypeLabel = new Label();
                parameterTypeLabel.Content = "(" + parameterDict[paramName].Type + ")\t";
                if (parameterDict[paramName].IsMandatory)
                {
                    parameterTypeLabel.Content += "[REQUIRED]";
                }
                else
                {
                    parameterTypeLabel.Content += "[OPTIONAL]";
                }
                
                Grid.SetRow(parameterNameLabel, count * 2);
                Grid.SetRow(parameterTypeLabel, count * 2);
                Grid.SetColumn(parameterNameLabel, 0);
                Grid.SetColumn(parameterTypeLabel, 1);
                /* Input field */
                TextBox parameterValueBox = new TextBox();
                parameterValueBox.Name = paramName;
                // Set previous value for this parameter if available
                if (existingParamsDict != null)
                {
                    var paramValue = existingParamsDict.FirstOrDefault(x => x.Key == paramName).Value;
                    if (paramValue != null) parameterValueBox.Text = paramValue;
                }
                parameterValueBox.MinWidth = 200;
                parameterValueBox.Margin = new System.Windows.Thickness(0,5,5,5);
                Grid.SetColumn(parameterValueBox, 0);
                Grid.SetRow(parameterValueBox, count * 2 + 1);
                Grid.SetColumnSpan(parameterValueBox, 2);
                /* Add to Grid */
                ParametersGrid.Children.Add(parameterNameLabel);
                ParametersGrid.Children.Add(parameterTypeLabel);
                ParametersGrid.Children.Add(parameterValueBox);
                count++;
            }
            // Set focus to first parameter textbox
            if(count > 0) ParametersGrid.Children[3].Focus();
        }

        /* 
         * This method assumes that:
         *   1. The window has already been populated with the parameter fields
         *   2. Each input field (text box) has the same name as the parameter it is for
         * 
         */
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            /* Validate parameters and return */
            _paramValues = new Dictionary<string, string>();
            string validationErrors = null;
            int count = 0;
            foreach (UIElement element in ParametersGrid.Children)
            {
                try
                {
                    TextBox inputField = (TextBox)element;
                    if (this.runbookType == "Python2")
                    {
                        var args = ReturnArgs("AddOn.exe " + inputField.Text);
                        if (args.Count() > 1)
                        {
                            // Remove AddOn.exe from the argument list
                            args = args.Where((val, index) => index != 0).ToArray();
                            foreach (var arg in args)
                            {
                                count++;
                                if (!String.IsNullOrEmpty(arg.ToString()))
                                    _paramValues.Add("[Parameter " + count.ToString() + "]", JsonConvert.SerializeObject(arg.ToString()));

                            }
                        }
                    }
                    else
                    {
                        if (String.IsNullOrEmpty(inputField.Text) && parameterDict[inputField.Name].IsMandatory == true)
                            validationErrors += "A value was not provided for the required parameter:  " + inputField.Name + "\r\n";
                        if (!String.IsNullOrEmpty(inputField.Text))
                            _paramValues.Add(inputField.Name, inputField.Text);
                    }
                }
                catch { /* not an input field */ }
            }
            if (String.IsNullOrEmpty(validationErrors))
            {
                this.DialogResult = true;
            }
            else {
                System.Windows.Forms.MessageBox.Show("Could not submit test job. The following errors were found:\r\n\r\n" + validationErrors);
            }
        }
    }
}
