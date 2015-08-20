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
        private bool initialized = false;

        public Object value { get { return _value; } }
        public bool encrypted { get { return _encrypted; } }

        public NewOrEditVariableDialog(AutomationVariable variable)
        {
            InitializeComponent();

            variableTypeComboBox.Items.Add(Constants.VariableType.String);
            variableTypeComboBox.Items.Add(Constants.VariableType.Number);
            variableTypeComboBox.SelectedValue = Constants.VariableType.String;

            variableEncryptedComboBox.Items.Add(Constants.EncryptedState.PlainText);
            variableEncryptedComboBox.Items.Add(Constants.EncryptedState.Encrypted);
            variableEncryptedComboBox.SelectedValue = Constants.EncryptedState.PlainText;

            if (variable != null)
            {
                if(variable.Encrypted)
                {
                    if(variable.getValue() != null) {
                        encryptedValueTextbox.Password = variable.getValue().ToString();
                    }
                    variableEncryptedComboBox.SelectedValue = Constants.EncryptedState.Encrypted;
                }
                else
                {
                    valueTextbox.Text = variable.getValue().ToString();
                    variableEncryptedComboBox.SelectedValue = Constants.EncryptedState.PlainText;
                }

                if (variable.getValue() is String)
                {
                    variableTypeComboBox.SelectedValue = Constants.VariableType.String;
                }
                else if (IsNumber(variable.getValue()))
                {
                    variableTypeComboBox.SelectedValue = Constants.VariableType.Number;
                }

                initialized = true;
                setEncrypted(variable.Encrypted);

                // don't allow user to change encrypted status of an existing variable asset
                variableEncryptedComboBox.IsEnabled = false; 
                variableEncryptedComboBox.IsEditable = false;
                variableEncryptedComboBox.IsHitTestVisible = false;
                variableEncryptedComboBox.Focusable = false;
                
                this.Title = "Edit Variable Asset";
            }
            else
            {
                initialized = true;
                this.Title = "New Variable Asset";
            }
        }

        private void setEncrypted(bool encrypted)
        {
            if (initialized)
            {
                if (encrypted)
                {
                    encryptedValueTextbox.Visibility = System.Windows.Visibility.Visible;
                    valueTextbox.Visibility = System.Windows.Visibility.Collapsed;
                    valueTextbox.Text = "";
                }
                else
                {
                    valueTextbox.Visibility = System.Windows.Visibility.Visible; ;
                    encryptedValueTextbox.Visibility = System.Windows.Visibility.Collapsed;
                    encryptedValueTextbox.Password = "";
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            bool done = true;
            _encrypted = (String)variableEncryptedComboBox.SelectedValue == Constants.EncryptedState.Encrypted;
            
            if(_encrypted)
            {
                _value = encryptedValueTextbox.Password;
            }
            else
            {
                _value = valueTextbox.Text;
            }

            if((String)variableTypeComboBox.SelectedValue == Constants.VariableType.Number)
            {
                try
                {
                    _value = Double.Parse((string)_value);
                }
                catch
                {
                    var valToShow = "'" + _value + "'";
                    
                    if(_encrypted)
                    {
                        valToShow = "the entered value";
                    }
                        
                    System.Windows.Forms.MessageBox.Show("Error: " + valToShow + " is not a number.");
                    done = false;
                }
            }

            if (done)
            {
                this.DialogResult = true;
            }
        }

        private void VariableEncryptedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            setEncrypted((String)variableEncryptedComboBox.SelectedValue == Constants.EncryptedState.Encrypted); 
        }

        private bool IsNumber(object value)
        {
            return value is sbyte
                    || value is byte
                    || value is short
                    || value is ushort
                    || value is int
                    || value is uint
                    || value is long
                    || value is ulong
                    || value is float
                    || value is double
                    || value is decimal;
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
