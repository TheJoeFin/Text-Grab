using System.Diagnostics;
using System.Windows;
using Text_Grab.Properties;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            ShowToastCheckBox.IsChecked = Settings.Default.ShowToast;
        }

        private void ShowToastCheckBox_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.ShowToast = (bool)ShowToastCheckBox.IsChecked;
            Settings.Default.Save();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            // for .NET Core you need to add UseShellExecute = true
            // see https://docs.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
