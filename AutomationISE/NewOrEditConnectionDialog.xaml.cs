using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AutomationISE.Model;
using System.Collections.Generic;
using Microsoft.Azure.Management.Automation.Models;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for NewOrEditCredentialDialog.xaml
    /// </summary>
    public partial class NewOrEditConnectionDialog : Window
    {
        private IDictionary<string, Object> _connectionFields;
        private string _connectionType;
        private ISet<ConnectionType> _connectionTypes;

        public IDictionary<string, Object> connectionFields { get { return _connectionFields; } }
        public string connectionType { get { return _connectionType; } }
        
        public NewOrEditConnectionDialog(AutomationConnection connection, ISet<ConnectionType> connectionTypes)
        {
            InitializeComponent();

            _connectionTypes = connectionTypes;

            // populate connection types drop down
            foreach (var connectionType in connectionTypes)
            {
                connectionTypeComboBox.Items.Add(connectionType.Name);
            }

            if (connection != null)
            {
                //UsernameTextbox.Text = cred.getUsername();

                this.Title = "Edit Connection Asset";
                connectionTypeComboBox.SelectedValue = connection.ConnectionType;
                AddConnectionFieldInputs(connection.ConnectionType, connection);
            }
            else
            {
                this.Title = "New Connection Asset";
                connectionTypeComboBox.SelectedIndex = 0;
            }
        }

        private void AddConnectionFieldInputs(string connectionTypeName, AutomationConnection startingConnection)
        {
            IDictionary<string, FieldDefinition> connectionFields = new Dictionary<string, FieldDefinition>();
            foreach (var connectionType in _connectionTypes)
            {
                if (connectionType.Name.Equals(connectionTypeName))
                {
                    connectionFields = connectionType.Properties.FieldDefinitions;
                }
            }
            
            /* Remove old added params */
            if (ParametersGrid.RowDefinitions.Count > 2)
            {
                ParametersGrid.RowDefinitions.RemoveRange(1, ParametersGrid.RowDefinitions.Count - 2);
                ParametersGrid.Children.RemoveRange(1, ParametersGrid.RowDefinitions.Count - 2);
            }

            /* Update the UI Grid to fit everything */
            for (int i = 0; i < connectionFields.Count * 2; i++)
            {
                RowDefinition rowDef = new RowDefinition();
                rowDef.Height = System.Windows.GridLength.Auto;
                ParametersGrid.RowDefinitions.Add(rowDef);
            }
            Grid.SetRow(ButtonsPanel, ParametersGrid.RowDefinitions.Count - 1);

            /* Fill the UI with parameter data */
            int count = 0;
            foreach (string paramName in connectionFields.Keys)
            {
                /* Parameter Name and Type */
                Label parameterNameLabel = new Label();
                parameterNameLabel.Content = paramName;

                Label parameterTypeLabel = new Label();
                parameterTypeLabel.Content = "(" + connectionFields[paramName].Type + ")\t";
                if (!connectionFields[paramName].IsOptional)
                {
                    parameterTypeLabel.Content += "[REQUIRED]";
                }
                else
                {
                    parameterTypeLabel.Content += "[OPTIONAL]";
                }

                Grid.SetRow(parameterNameLabel, 1 + count * 2);
                Grid.SetRow(parameterTypeLabel, 1 + count * 2);
                Grid.SetColumn(parameterNameLabel, 0);
                Grid.SetColumn(parameterTypeLabel, 1);

                /* Input field */
                Control parameterValueBox = null;
                Object paramValue = null;

                // Set previous value for this parameter if available
                if (startingConnection != null && startingConnection.ConnectionType.Equals(connectionTypeComboBox.SelectedValue))
                {
                    paramValue = startingConnection.getFields()[paramName];
                }
               
                if (
                    connectionFields[paramName].Type.Equals(Constants.ConnectionTypeFieldType.String) || 
                    connectionFields[paramName].Type.Equals(Constants.ConnectionTypeFieldType.Int)
                )
                {
                    if (connectionFields[paramName].IsEncrypted)
                    {
                        parameterValueBox = new PasswordBox();
                        if(paramValue != null)
                        {
                            ((PasswordBox)parameterValueBox).Password = paramValue.ToString();
                        }
                    }
                    else
                    {
                        parameterValueBox = new TextBox();
                        if (paramValue != null)
                        {
                            ((TextBox)parameterValueBox).Text = paramValue.ToString();
                        }
                    }
                }
                else if (connectionFields[paramName].Type.Equals(Constants.ConnectionTypeFieldType.Boolean))
                {
                    parameterValueBox = new ComboBox();
                    ((ComboBox)parameterValueBox).Items.Add("True");
                    ((ComboBox)parameterValueBox).Items.Add("False");

                    if (paramValue != null)
                    {
                        try
                        {
                            if ((bool)paramValue == true)
                            {
                                ((ComboBox)parameterValueBox).SelectedValue = "True";
                            }
                            else
                            {
                                ((ComboBox)parameterValueBox).SelectedValue = "False";
                            }
                        }
                        catch(Exception e)
                        {
                            // value is not a bool, even though connection type schema says it should be
                        }
                    }
                }

                parameterValueBox.Name = paramName;

                parameterValueBox.MinWidth = 200;
                parameterValueBox.Margin = new System.Windows.Thickness(0, 5, 5, 5);
                Grid.SetColumn(parameterValueBox, 0);
                Grid.SetRow(parameterValueBox, 1 + count * 2 + 1);
                Grid.SetColumnSpan(parameterValueBox, 2);
                
                /* Add to Grid */
                ParametersGrid.Children.Add(parameterNameLabel);
                ParametersGrid.Children.Add(parameterTypeLabel);
                ParametersGrid.Children.Add(parameterValueBox);
                count ++;
            }

            // Set focus to first parameter textbox
            if (count > 0) ParametersGrid.Children[3].Focus();
        }

        private void connectionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AddConnectionFieldInputs((string)connectionTypeComboBox.SelectedValue, null);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            //_username = UsernameTextbox.Text;

            _connectionType = (String)connectionTypeComboBox.SelectedValue;

            this.DialogResult = true;
        }
    }
}
