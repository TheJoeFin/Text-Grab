using Microsoft.Win32;
using System;
using System.Drawing;
using System.Windows;
using Wpf.Ui.Controls.Window;

namespace Text_Grab.Controls
{
    /// <summary>
    /// Interaction logic for QrCodeWindow.xaml
    /// </summary>
    public partial class QrCodeWindow : FluentWindow
    {
        public Bitmap Bitmap { get; set; }
        public string TextOfCode { get; set; }
        public QrCodeWindow(Bitmap bitmap, string textOfCode, bool showError = false)
        {
            InitializeComponent();
            Bitmap = bitmap;
            TextOfCode = textOfCode;
            CodeImage.ToolTip = textOfCode;
            CodeImage.Source = ImageMethods.BitmapToImageSource(Bitmap);

            if (showError)
                LengthErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "Image | *.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() is not true)
                return;

            Bitmap.Save(dialog.FileName);
        }
    }
}
