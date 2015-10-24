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
            }
            else
            {
                this.Title = "New Connection Asset";
                connectionTypeComboBox.SelectedIndex = 0;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            //_username = UsernameTextbox.Text;

            _connectionType = (String)connectionTypeComboBox.SelectedValue;

            this.DialogResult = true;
        }
    }
}
