using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
    }
}
