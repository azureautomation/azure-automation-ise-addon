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

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for DSCConfigurationParamDialog.xaml
    /// </summary>
    public partial class DSCConfigurationParamDialog : Window
    {
        private IDictionary<string, DscConfigurationParameter> parameterDict;
        private IDictionary<string, string> existingParamsDict;
        private IDictionary<string, string> configurationDataFiles;
        private IDictionary<string, string> _paramValues;
        public IDictionary<string, string> paramValues { get { return _paramValues; } }

        private string _configDataSelection;
        public string configDataSelection { get { return _configDataSelection; } }

        public DSCConfigurationParamDialog(IDictionary<string, DscConfigurationParameter> parameterDict, IDictionary<string, string> existingParamsDict, IDictionary<string, string> configurationDataFiles)
        {
            InitializeComponent();
            this.parameterDict = parameterDict;
            this.existingParamsDict = existingParamsDict;
            this.configurationDataFiles = configurationDataFiles;
            if (parameterDict.Count > 0)
                AddParamForms();
            if (configurationDataFiles.Count > 0)
                AddConfigDataForms();
        }

        private void AddConfigDataForms()
        {
            /* Update the UI Grid to fit everything */
            for (int i = 0; i < 2; i++)
            {
                RowDefinition rowDef = new RowDefinition();
                rowDef.Height = System.Windows.GridLength.Auto;
                ParametersGrid.RowDefinitions.Add(rowDef);
            }
            Grid.SetRow(ButtonsPanel, ParametersGrid.RowDefinitions.Count - 1);

            Label configDataLabel = new Label();
            configDataLabel.Content = "Select configuration data to use :";
            Grid.SetRow(configDataLabel, ParametersGrid.RowDefinitions.Count - 3);
            Grid.SetColumn(configDataLabel, 0);
            Grid.SetColumnSpan(configDataLabel, 2);

            IList<string> runOnOptions = new List<string>();
            runOnOptions.Add("None");
            foreach (var configDataFile in this.configurationDataFiles)
            {
                runOnOptions.Add(configDataFile.Key);
            }
            ComboBox configDataComboBox = new ComboBox();
            configDataComboBox.ItemsSource = runOnOptions;
            configDataComboBox.SelectedIndex = 0;
            configDataComboBox.SelectionChanged += changeConfigDataSelection;
            Grid.SetRow(configDataComboBox, ParametersGrid.RowDefinitions.Count - 2);
            Grid.SetColumn(configDataComboBox, 0);
            Grid.SetColumnSpan(configDataComboBox, 2);

            /* Add to Grid */
            ParametersGrid.Children.Add(configDataLabel);
            ParametersGrid.Children.Add(configDataComboBox);
        }

        private void changeConfigDataSelection(object sender, RoutedEventArgs e)
        {
            _configDataSelection = ((sender as ComboBox).SelectedItem as string);
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
                    if (paramValue != null) parameterValueBox.Text = paramValue.ToString();
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
            foreach (UIElement element in ParametersGrid.Children)
            {
                try
                {
                    TextBox inputField = (TextBox)element;
                    if (String.IsNullOrEmpty(inputField.Text) && parameterDict[inputField.Name].IsMandatory == true)
                        validationErrors += "A value was not provided for the required parameter:  " + inputField.Name + "\r\n";
                    if (!String.IsNullOrEmpty(inputField.Text))
                        _paramValues.Add(inputField.Name, inputField.Text);
                }
                catch { /* not an input field */ }
            }
            if (String.IsNullOrEmpty(validationErrors))
            {
                this.DialogResult = true;
            }
            else {
                System.Windows.Forms.MessageBox.Show("Could not submit compilation job. The following errors were found:\r\n\r\n" + validationErrors);
            }
        }
    }
}
