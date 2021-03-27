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
using Text_Grab.Utilities;

namespace Text_Grab.Views
{
    /// <summary>
    /// Interaction logic for PersistentWindow.xaml
    /// </summary>
    public partial class GrabFrame : Window
    {
        public GrabFrame()
        {
            InitializeComponent();
        }

        private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private async void GrabBTN_Click(object sender, RoutedEventArgs e)
        {
            Point windowPosition = this.GetAbsolutePosition();
             
             System.Drawing.Rectangle rectCanvasSize = new System.Drawing.Rectangle
             {
                 Width = (int)RectanglesCanvas.Width,
                 Height = (int)RectanglesCanvas.Height,
                 X = (int)windowPosition.X + 2,
                 Y = (int)windowPosition.Y + 24
             };
             string frameText = await ImageMethods.GetRegionsText(this, rectCanvasSize);
             NotificationUtilities.ShowToast(frameText);
        }
    }
}
