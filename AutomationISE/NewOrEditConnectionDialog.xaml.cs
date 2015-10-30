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
            IDictionary<string, FieldDefinition> connectionFieldDefinitions = new Dictionary<string, FieldDefinition>();
            foreach (var connectionType in _connectionTypes)
            {
                if (connectionType.Name.Equals(connectionTypeName))
                {
                    connectionFieldDefinitions = connectionType.Properties.FieldDefinitions;
                }
            }
            
            /* Remove old added fields */
            if (ParametersGrid.RowDefinitions.Count > 2)
            {
                ParametersGrid.RowDefinitions.RemoveRange(1, ParametersGrid.RowDefinitions.Count - 2);


                var i = 0;
                var toRemove = new HashSet<UIElement>();
                foreach (UIElement element in ParametersGrid.Children)
                {
                    if(i > 1 && (!element.GetType().Name.Equals("WrapPanel")))
                    {
                        // removes everything that is not the connection type selector at the top or the ok / cancel button in the WrapPanel at the bottom
                        toRemove.Add(element);
                    }   
 
                    i++;
                }

                foreach (UIElement removeElement in toRemove)
                {
                    ParametersGrid.Children.Remove(removeElement);
                }
            }

            /* Update the UI Grid to fit everything */
            for (int i = 0; i < connectionFieldDefinitions.Count * 2; i++)
            {
                RowDefinition rowDef = new RowDefinition();
                rowDef.Height = System.Windows.GridLength.Auto;
                ParametersGrid.RowDefinitions.Add(rowDef);
            }
            Grid.SetRow(ButtonsPanel, ParametersGrid.RowDefinitions.Count - 1);

            /* Fill the UI with parameter data */
            int count = 0;
            foreach (string paramName in connectionFieldDefinitions.Keys)
            {
                /* Parameter Name and Type */
                Label parameterNameLabel = new Label();
                parameterNameLabel.Content = paramName;

                Label parameterTypeLabel = new Label();
                parameterTypeLabel.Content = "(" + connectionFieldDefinitions[paramName].Type + ")\t";
                if (!connectionFieldDefinitions[paramName].IsOptional)
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
                    connectionFieldDefinitions[paramName].Type.Equals(Constants.ConnectionTypeFieldType.String) || 
                    connectionFieldDefinitions[paramName].Type.Equals(Constants.ConnectionTypeFieldType.Int)
                )
                {
                    if (connectionFieldDefinitions[paramName].IsEncrypted)
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
                else if (connectionFieldDefinitions[paramName].Type.Equals(Constants.ConnectionTypeFieldType.Boolean))
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
                        catch
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

        /* 
         * This method assumes that:
         *   1. The window has already been populated with the parameter fields
         *   2. Each input field (text box) has the same name as the parameter it is for
         * 
         */
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _connectionType = (String)connectionTypeComboBox.SelectedValue;

            IDictionary<string, FieldDefinition> connectionFieldDefinitions = new Dictionary<string, FieldDefinition>();
            foreach (var connectionType in _connectionTypes)
            {
                if (connectionType.Name.Equals(_connectionType))
                {
                    connectionFieldDefinitions = connectionType.Properties.FieldDefinitions;
                    break;
                }
            }

            _connectionFields = new Dictionary<string, Object>();
            string validationErrors = null;

            /* Validate parameters and return */
            foreach (UIElement element in ParametersGrid.Children)
            {
                try
                {
                    Control inputField = (Control)element;
                    var fieldName = inputField.Name;

                    if (connectionFieldDefinitions[fieldName] == null)
                    {
                        // not one of the connection field inputs, skip it
                        continue;
                    }

                    if (
                        connectionFieldDefinitions[fieldName].Type.Equals(Constants.ConnectionTypeFieldType.String) ||
                        connectionFieldDefinitions[fieldName].Type.Equals(Constants.ConnectionTypeFieldType.Int)
                    )
                    {
                        if (connectionFieldDefinitions[fieldName].IsEncrypted)
                        {
                            _connectionFields[fieldName] = ((PasswordBox)inputField).Password;
                        }
                        else
                        {
                            _connectionFields[fieldName] = ((TextBox)inputField).Text;
                        }

                        if (_connectionFields[fieldName].ToString().Length == 0)
                        {
                            _connectionFields[fieldName] = null;

                            if (!connectionFieldDefinitions[fieldName].IsOptional)
                            {
                                validationErrors += ("Connection field '" + fieldName + "' is required. ");
                                continue;
                            }
                        }

                        if (_connectionFields[fieldName] != null && connectionFieldDefinitions[fieldName].Type.Equals(Constants.ConnectionTypeFieldType.Int))
                        {
                            try
                            {
                                _connectionFields[fieldName] = Int32.Parse((string)_connectionFields[fieldName]);
                            }
                            catch
                            {
                                var valToShow = "The value '" + _connectionFields[fieldName] + "'";

                                if (connectionFieldDefinitions[fieldName].IsEncrypted)
                                {
                                    valToShow = "The entered value";
                                }

                                validationErrors += (valToShow + " for connection field '" + fieldName + "' is not an integer. ");
                                continue;
                            }
                        }
                    }
                    else if (connectionFieldDefinitions[fieldName].Type.Equals(Constants.ConnectionTypeFieldType.Boolean))
                    {
                        _connectionFields[fieldName] = ((ComboBox)inputField).SelectedValue.Equals("True") ? true : false;
                    }
                }
                catch { /* not an input field */ }
            }

            if(String.IsNullOrEmpty(validationErrors))
            {
                this.DialogResult = true;
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Could not update local connection asset. The following errors were found:\r\n\r\n" + validationErrors);
            }
        }
    }
}
