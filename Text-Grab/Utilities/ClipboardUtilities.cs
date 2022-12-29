using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
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
            dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
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

    public static (bool, ImageSource?) TryGetImageFromClipboard()
    {
        if (!System.Windows.Clipboard.ContainsImage())
            return (false, null);

        IDataObject clipboardData = System.Windows.Clipboard.GetDataObject();
        if (clipboardData is null
            || !clipboardData.GetDataPresent(System.Windows.Forms.DataFormats.Bitmap))
            return(false, null);

        ImageSource imageSource = System.Windows.Clipboard.GetImage();

        if (imageSource is null)
            return (false, null);

        return (true, imageSource);
    }
}