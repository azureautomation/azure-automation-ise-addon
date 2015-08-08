using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AutomationISE.Model;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for NewOrEditCredentialDialog.xaml
    /// </summary>
    public partial class NewOrEditVariableDialog : Window
    {
        private Object _value;
        private bool _encrypted;

        public Object value { get { return _value; } }
        public bool encrypted { get { return _encrypted; } }

        public NewOrEditVariableDialog(AutomationVariable variable)
        {
            InitializeComponent();

            variableTypeComboBox.Items.Add(Constants.VariableType.String);
            variableTypeComboBox.Items.Add(Constants.VariableType.Number);

            variableEncryptedComboBox.Items.Add(Constants.EncryptedState.Encrypted);
            variableEncryptedComboBox.Items.Add(Constants.EncryptedState.PlainText);

            // TODO: default the type, default the encrypted

            if (variable != null)
            {
                // TODO: set the type and encrypted fields as well
                
                if(variable.Encrypted)
                {
                    encryptedValueTextbox.Password = variable.getValue().ToString();
                }
                else
                {
                    valueTextbox.Text = variable.getValue().ToString();
                }

                setEncrypted(variable.Encrypted);
                
                this.Title = "Edit Variable Asset";
            }
            else
            {
                this.Title = "New Variable Asset";
            }
        }

        private void setEncrypted(bool encrypted)
        {
            // TODO: change z coordinate of which is on top
            if (encrypted)
            {
                encryptedValueTextbox.Opacity = 100;
                valueTextbox.Opacity = 0;
                valueTextbox.Text = "";
            }
            else
            {
                valueTextbox.Opacity = 100;
                encryptedValueTextbox.Opacity = 0; 
                encryptedValueTextbox.Password = "";
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            bool done = true;
            _encrypted = variableEncryptedComboBox.SelectedValue == Constants.EncryptedState.Encrypted;
            
            if(_encrypted)
            {
                _value = encryptedValueTextbox.Password;
            }
            else
            {
                _value = valueTextbox.Text;
            }

            if(variableTypeComboBox.SelectedValue == Constants.VariableType.Number)
            {
                try
                {
                    _value = Double.Parse((string)_value);
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show("Error: '" + _value + "' is not a number.");
                    done = false;
                }
            }

            this.DialogResult = done;
        }

        private async void VariableEncryptedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            setEncrypted(variableEncryptedComboBox.SelectedValue == Constants.EncryptedState.Encrypted); 
        }

        private class Constants
        {
            public class VariableType
            {
                public const String String = "String";
                public const String Number = "Number";
            }

            public class EncryptedState
            {
                public const String Encrypted = "Yes";
                public const String PlainText = "No";
            }
        }
    }
}
