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

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for DeleteConfigurationDialog.xaml
    /// </summary>
    public partial class DeleteConfigurationDialog : Window
    {
        public bool deleteLocalOnly { get; set; }
        public DeleteConfigurationDialog(String ConfigurationName)
        {
            InitializeComponent();
            this.PromptLabel.Content = "Delete " + ConfigurationName + " from where?";
            deleteLocalOnly = true;
        }

        private void LocalButton_Click(object sender, RoutedEventArgs e)
        {
            deleteLocalOnly = true;
            this.DialogResult = true;
        }

        private void CloudButton_Click(object sender, RoutedEventArgs e)
        {
            deleteLocalOnly = false;
            this.DialogResult = true;
        }
    }
}
