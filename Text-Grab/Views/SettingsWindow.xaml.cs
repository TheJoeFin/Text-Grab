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
        }

        private void ShowToastCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (IsLoaded == false)
                return;

            Settings.Default.ShowToast = (bool)ShowToastCheckBox.IsChecked;
            Settings.Default.Save();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ShowToastCheckBox.IsChecked = Settings.Default.ShowToast;
            ErrorCorrectBox.IsChecked = Settings.Default.CorrectErrors;

            switch (Settings.Default.DefaultLaunch)
            {
                case "Fullscreen":
                    FullScreenRDBTN.IsChecked = true;
                    break;
                case "GrabFrame":
                    GrabFrameRDBTN.IsChecked = true;
                    break;
                case "EditText":
                    EditTextRDBTN.IsChecked = true;
                    break;
                default:
                    FullScreenRDBTN.IsChecked = true;
                    break;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void EditTextRDBTN_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded != true)
                return;
            Settings.Default.DefaultLaunch = "EditText";
            Settings.Default.Save();
        }

        private void GrabFrameRDBTN_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded != true)
                return; 
            Settings.Default.DefaultLaunch = "GrabFrame";
            Settings.Default.Save();
        }

        private void FullScreenRDBTN_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded != true)
                return;
            Settings.Default.DefaultLaunch = "Fullscreen";
            Settings.Default.Save();
        }

        private void ErrorCorrectBox_Click(object sender, RoutedEventArgs e)
        {
            if (IsLoaded == false)
                return;

            Settings.Default.CorrectErrors = (bool)ErrorCorrectBox.IsChecked;
            Settings.Default.Save();
        }
    }
}
