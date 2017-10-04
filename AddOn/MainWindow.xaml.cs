using System.Windows;
using AutomationISE;

namespace AddOn
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var application = new AutomationISE.AutomationISEControl();
            application.InitializeComponent();
            this.Content = application;
            this.Title = "Azure Automation Add-On";
            this.Left = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Right - this.Width;
            this.Top = 0;
            this.Height = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height;
        }

    }
}
