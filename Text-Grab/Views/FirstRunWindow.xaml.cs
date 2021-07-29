using System.Windows;
using Text_Grab.Properties;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for FirstRunWindow.xaml
    /// </summary>
    public partial class FirstRunWindow : Window
    {
        public FirstRunWindow()
        {
            InitializeComponent();
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void FirstRun_Loaded(object sender, RoutedEventArgs e)
        {
            ShowToastCheckBox.IsChecked = Settings.Default.ShowToast;

            switch (Settings.Default.DefaultLaunch)
            {
                case "Fullscreen":
                    FullScreenRDBTN.IsChecked = true;
                    break;
                case "GrabFrame":
                    GrabFrameRDBTN.IsChecked = true;
                    break;
                case "EditText":
                    EditWindowRDBTN.IsChecked = true;
                    break;
                default:
                    EditWindowRDBTN.IsChecked = true;
                    break;
            }
        }

        private void ShowToastCheckBox_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.ShowToast = (bool)ShowToastCheckBox.IsChecked;
            Settings.Default.Save();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded != true)
                return;

            if ((bool)GrabFrameRDBTN.IsChecked)
                Settings.Default.DefaultLaunch = "GrabFrame";
            else if ((bool)FullScreenRDBTN.IsChecked)
                Settings.Default.DefaultLaunch = "Fullscreen";
            else
                Settings.Default.DefaultLaunch = "EditText";

            Settings.Default.Save();
        }
    }
}
