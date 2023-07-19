using Humanizer;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.IO;
using System.Reflection.Metadata;
using System.Windows;
using System.Windows.Media.Imaging;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Controls
{
    /// <summary>
    /// Interaction logic for QrCodeWindow.xaml
    /// </summary>
    public partial class QrCodeWindow : FluentWindow
    {
        private string qrCodeFileName = string.Empty;

        #region Public Constructors

        public QrCodeWindow(Bitmap bitmap, string textOfCode, bool showError = false)
        {
            InitializeComponent();
            QrBitmap = bitmap;
            TextOfCode = textOfCode;
            CodeImage.ToolTip = textOfCode;
            CodeImage.Source = ImageMethods.BitmapToImageSource(QrBitmap);

            if (showError)
                LengthErrorTextBlock.Visibility = Visibility.Visible;

            UiTitleBar.Title = $"QR Code: {TextOfCode.Truncate(30)}";
            qrCodeFileName = $"QR-{TextOfCode.Truncate(20, "-").ReplaceReservedCharacters()}.png";
        }

        #endregion Public Constructors

        #region Public Properties

        public Bitmap QrBitmap { get; set; }
        public string TextOfCode { get; set; }

        #endregion Public Properties

        #region Private Methods

        private void CodeImage_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string tempPath = Path.Combine(
                Path.GetTempPath(), qrCodeFileName);
            try
            {
                // DoDragDrop with file thumbnail as drag image
                QrBitmap.Save(tempPath);
                var dataObject = DragDataObject.FromFile(tempPath);
                using var bitmap = QrBitmap;
                IntPtr hBitmap = bitmap.GetHbitmap();

                dataObject.SetDragImage(hBitmap, bitmap.Width, bitmap.Height);
                DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);

            }
            catch
            {
                // DoDragDrop without drag image
                IDataObject dataObject = new DataObject(DataFormats.FileDrop, new[] { tempPath });
                DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch { }
                }
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetData(DataFormats.Bitmap, QrBitmap);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                FileName = qrCodeFileName,
                Filter = "Image | *.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() is not true)
                return;

            QrBitmap.Save(dialog.FileName);
        }

        #endregion Private Methods
    }
}
