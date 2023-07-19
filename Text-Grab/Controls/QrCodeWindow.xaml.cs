using Humanizer;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
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
        #region Private Fields

        private string qrCodeFileName = string.Empty;
        private string tempPath = string.Empty;
        private IntPtr hBitmap;

        #endregion Private Fields

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
            int maxLength = 20;
            int trimLength = TextOfCode.Length < maxLength ? TextOfCode.Length : maxLength;
            qrCodeFileName = $"QR-{TextOfCode.Substring(0, trimLength).ReplaceReservedCharacters()}.png";
            tempPath = Path.Combine(Path.GetTempPath(), qrCodeFileName);

            QrBitmap.Save(tempPath, ImageFormat.Png);
            hBitmap = QrBitmap.GetHbitmap();
        }

        #endregion Public Constructors

        #region Public Properties

        public Bitmap QrBitmap { get; set; }
        public string TextOfCode { get; set; }

        #endregion Public Properties

        #region Private Methods

        private void CodeImage_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                // DoDragDrop with file thumbnail as drag image
                var dataObject = DragDataObject.FromFile(tempPath);
                dataObject.SetDragImage(hBitmap, QrBitmap.Width, QrBitmap.Height);
                DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
            }
            catch
            {
                // DoDragDrop without drag image
                IDataObject dataObject = new DataObject(DataFormats.FileDrop, new[] { tempPath });
                DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetData(DataFormats.Bitmap, QrBitmap);
        }

        private void FluentWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            NativeMethods.DeleteObject(hBitmap);
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch { }
            }
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
