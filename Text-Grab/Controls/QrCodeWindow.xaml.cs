using Humanizer;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;
using ZXing.QrCode.Internal;
using static ZXing.Rendering.SvgRenderer;
// using static System.Net.Mime.MediaTypeNames;

namespace Text_Grab.Controls
{
    /// <summary>
    /// Interaction logic for QrCodeWindow.xaml
    /// </summary>
    public partial class QrCodeWindow : FluentWindow
    {
        #region Fields

        private IntPtr hBitmap;
        private string qrCodeFileName = string.Empty;
        private string tempPath = string.Empty;
        private DispatcherTimer textDebounceTimer = new();
        private ErrorCorrectionLevel errorCorrectionLevel = ErrorCorrectionLevel.L;
        #endregion Fields

        #region Constructors

        public QrCodeWindow(string textOfCode)
        {
            InitializeComponent();

            textOfCode = textOfCode.MakeStringSingleLine();
            QrCodeTextBox.Text = textOfCode;
            textDebounceTimer.Interval = new(0, 0, 0, 0, 200);
            textDebounceTimer.Tick += TextDebounceTimer_Tick;
            SetQrCodeToText(textOfCode);
        }

        private void TextDebounceTimer_Tick(object? sender, EventArgs e)
        {
            SetQrCodeToText(TextOfCode);
        }
        #endregion Constructors

        #region Properties

        public Bitmap? QrBitmap { get; set; }
        public string TextOfCode { get; set; } = string.Empty;

        #endregion Properties

        #region Methods

        private void CodeImage_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (QrBitmap is null)
                return;

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

        private void ErrorCorrectionComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox 
                || comboBox.SelectedItem is not ComboBoxItem selectedItem
                || selectedItem.Tag is not string tagLevel)
                return;

            errorCorrectionLevel = tagLevel switch
            {
                "L" => ErrorCorrectionLevel.L,
                "M" => ErrorCorrectionLevel.M,
                "Q" => ErrorCorrectionLevel.Q,
                "H" => ErrorCorrectionLevel.H,
                _ => ErrorCorrectionLevel.L
            };

            SetQrCodeToText();
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

        private void QrCodeTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            textDebounceTimer.Stop();
            // if (sender is not TextBox senderBox || senderBox.Text is not string textboxString)
            //     return;

            TextOfCode = QrCodeTextBox.Text;
            textDebounceTimer.Start();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (QrBitmap is null)
                return;

            SaveFileDialog dialog = new()
            {
                FileName = qrCodeFileName + ".png",
                Filter = "Image | *.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() is not true)
                return;

            QrBitmap.Save(dialog.FileName);
        }

        private void SetQrCodeToText(string textOfCode = "")
        {
            if (!string.IsNullOrEmpty(textOfCode))
               TextOfCode = textOfCode;

            if (string.IsNullOrEmpty(TextOfCode))
                return;

            bool showError = false;
            int maxCharLength = 2953;
            if (TextOfCode.Length > maxCharLength)
            {
                TextOfCode = TextOfCode.Substring(0, maxCharLength);
                showError = true;
            }
            QrBitmap = BarcodeUtilities.GetQrCodeForText(TextOfCode, errorCorrectionLevel);
            CodeImage.ToolTip = textOfCode;
            CodeImage.Source = ImageMethods.BitmapToImageSource(QrBitmap);

            if (showError)
                LengthErrorTextBlock.Visibility = Visibility.Visible;

            int maxLength = 50;
            UiTitleBar.Title = $"QR Code: {TextOfCode.Truncate(30)}";
            int trimLength = TextOfCode.Length < maxLength ? TextOfCode.Length : maxLength;
            qrCodeFileName = $"QR-{TextOfCode.Substring(0, trimLength).ReplaceReservedCharacters()}";
            tempPath = Path.Combine(Path.GetTempPath(), qrCodeFileName + ".png");

            QrBitmap.Save(tempPath, ImageFormat.Png);
            hBitmap = QrBitmap.GetHbitmap();
        }
        private async void SvgButton_Click(object sender, RoutedEventArgs e)
        {
            if (QrBitmap is null)
                return;

            SaveFileDialog dialog = new()
            {
                FileName = qrCodeFileName + ".svg",
                Filter = "SVG | *.svg",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() is not true)
                return;

            SvgImage svgImage = BarcodeUtilities.GetSvgQrCodeForText(TextOfCode, errorCorrectionLevel);

            if (string.IsNullOrWhiteSpace(svgImage.Content))
                return;

            await FileUtilities.SaveTextFile(svgImage.Content, dialog.FileName, FileStorageKind.Absolute);
        }
        #endregion Methods
    }
}
