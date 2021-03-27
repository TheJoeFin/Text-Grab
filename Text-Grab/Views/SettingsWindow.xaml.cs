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

            switch (Settings.Default.DefaultLaunch)
            {
                case "Fullscreen":
                    FullScreenRDBTN.IsChecked = true;
                    break;
                case "GrabFrame":
                    GrabFrameRDBTN.IsChecked = true;
                    break;
                default:
                    FullScreenRDBTN.IsChecked = true;
                    break;
            }
        }

        private void ShowToastCheckBox_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.ShowToast = (bool)ShowToastCheckBox.IsChecked;
            Settings.Default.Save();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ShowToastCheckBox.IsChecked = Settings.Default.ShowToast;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded != true)
                return;

            if ((bool)GrabFrameRDBTN.IsChecked)
                Settings.Default.DefaultLaunch = "GrabFrame";
            else
                Settings.Default.DefaultLaunch = "Fullscreen";

            Settings.Default.Save();
        }
    }
}
