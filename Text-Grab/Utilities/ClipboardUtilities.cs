using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Text_Grab.Utilities;

public class ClipboardUtilities
{
    public static async Task<(bool, string)> TryGetClipboardText()
    {
        DataPackageView? dataPackageView = null;
        string clipboardText = "";

        try
        {
            dataPackageView = Clipboard.GetContent();
        }
        catch (System.Exception ex)
        {
            return (false, ex.Message);
        }

        if (dataPackageView is null)
        {
            return (false, clipboardText);
        }

        if (dataPackageView.Contains(StandardDataFormats.Text))
        {
            try
            {
                clipboardText = await dataPackageView.GetTextAsync();
            }
            catch (System.Exception ex)
            {
                return (false, $"error with dataPackageView.GetTextAsync(). Exception Message: {ex.Message}");
            }

            return (true, clipboardText);
        }

        return (false, clipboardText);
    }

    public static async Task<(bool, BitmapImage?)> TryGetImageFromClipboard()
    {
        DataPackageView? dataPackageView = null;

        try
        {
            dataPackageView = Clipboard.GetContent();
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"error with Clipboard.GetContent(). Exception Message: {ex.Message}");
        }

        if (dataPackageView is null)
        {
            return (false, null);
        }

        if (dataPackageView.Contains(StandardDataFormats.Bitmap))
        {
            try
            {
                RandomAccessStreamReference streamReference = await dataPackageView.GetBitmapAsync();
                using IRandomAccessStream stream = await streamReference.OpenReadAsync();
                BitmapImage bmp = ImageMethods.GetBitmapImageFromIRandomAccessStream(stream);
                return (true, bmp);
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"error with dataPackageView.GetBitmapAsync(). Exception Message: {ex.Message}");
                return (false, null);
            }
        }

        return (false, null);
    }
}