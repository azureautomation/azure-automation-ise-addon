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

using AutomationISE.Model;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for CreateConfigurationDialog.xaml
    /// </summary>
    public partial class CreateConfigurationDialog : Window
    {
        public string configurationName { get; set; }
        public string configurationType { get; set; }
        public CreateConfigurationDialog()
        {
            InitializeComponent();
            NameTextBox.Focus();
        }

        private void ConfigurationButton_Click(object sender, RoutedEventArgs e)
        {
            if (textBoxFilled())
            {
                configurationName = NameTextBox.Text;
                configurationType = Constants.RunbookType.PowerShellScript;
                this.DialogResult = true;
            }
            else
            {
                MessageBox.Show("You must enter a name for the new configuration.");
            }
        }

        private void ConfigurationDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (textBoxFilled())
            {
                configurationName = NameTextBox.Text + "_Configuration_Data";
                configurationType = Constants.RunbookType.PowerShellScript;
                this.DialogResult = true;
            }
            else
            {
                MessageBox.Show("You must enter a name for the new configuration data.");
            }
        }
        private bool textBoxFilled()
        {
            if (String.IsNullOrEmpty(NameTextBox.Text))
                return false;
            return true;
        }
    }
}
