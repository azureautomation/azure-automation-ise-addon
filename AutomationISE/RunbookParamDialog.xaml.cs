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
    /// Interaction logic for RunbookParamDialog.xaml
    /// </summary>
    public partial class RunbookParamDialog : Window
    {
        public string ExampleResult
        {
            get { return "something from UI element"; }
        }

        public RunbookParamDialog(IDictionary<string, RunbookParameter> parameterDict)
        {
            InitializeComponent();
            AddParamForms(parameterDict);
        }
        private void AddParamForms(IDictionary<string, RunbookParameter> parameterDict)
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
                parameterTypeLabel.Content = "(" + parameterDict[paramName].Type + "): ";
                Grid.SetRow(parameterNameLabel, count * 2);
                Grid.SetRow(parameterTypeLabel, count * 2);
                Grid.SetColumn(parameterNameLabel, 0);
                Grid.SetColumn(parameterTypeLabel, 1);
                /* Input field */
                TextBox parameterValueBox = new TextBox();
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
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            /* Validate parameters and return */
        }
    }
}
