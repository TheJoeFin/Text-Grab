using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Text_Grab.Controls
{
    /// <summary>
    /// Interaction logic for QrCodeWindow.xaml
    /// </summary>
    public partial class QrCodeWindow : Window
    {
        public Bitmap Bitmap { get; set; }
        public string TextOfCode { get; set; }
        public QrCodeWindow(Bitmap bitmap, string textOfCode)
        {
            InitializeComponent();
            Bitmap = bitmap;
            TextOfCode = textOfCode;
            CodeImage.ToolTip = textOfCode;
            CodeImage.Source = ImageMethods.BitmapToImageSource(Bitmap);
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
