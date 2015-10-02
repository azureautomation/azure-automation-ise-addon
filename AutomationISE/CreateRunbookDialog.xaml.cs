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
    /// Interaction logic for CreateRunbookDialog.xaml
    /// </summary>
    public partial class CreateRunbookDialog : Window
    {
        public string runbookName { get; set; }
        public string runbookType { get; set; }
        public CreateRunbookDialog()
        {
            InitializeComponent();
            NameTextBox.Focus();
        }

        private void ScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (textBoxFilled())
            {
                runbookName = NameTextBox.Text;
                runbookType = Constants.RunbookType.PowerShellScript;
                this.DialogResult = true;
            }
            else
            {
                MessageBox.Show("You must enter a name for the new runbook.");
            }
        }

        private void WorkflowButton_Click(object sender, RoutedEventArgs e)
        {
            if (textBoxFilled())
            {
                runbookName = NameTextBox.Text;
                runbookType = Constants.RunbookType.Workflow;
                this.DialogResult = true;
            }
            else
            {
                MessageBox.Show("You must enter a name for the new runbook.");
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
