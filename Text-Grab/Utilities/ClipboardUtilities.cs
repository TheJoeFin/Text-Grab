using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;

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
        ImageSource? imageSource = null;

        if (!ClipboardContainsBase64Image())
        {
            IDataObject clipboardData = System.Windows.Clipboard.GetDataObject();
            if (clipboardData is null
                || !clipboardData.GetDataPresent(System.Windows.Forms.DataFormats.Bitmap))
                return (false, null);

            imageSource = System.Windows.Clipboard.GetImage();
        }
        else
        {
            imageSource = GetBase64ClipboardContentAsImageSource();
        }

        if (imageSource is null)
            return (false, null);

        return (true, imageSource);
    }

    private static ImageSource? GetBase64ClipboardContentAsImageSource()
    {
        string? trimmedData = null;

        try { trimmedData = System.Windows.Clipboard.GetText().Trim(); } catch { return null; }
        trimmedData = CleanTeamsBase64Image(trimmedData);

        // used some code from https://github.com/veler/DevToys
        string base64 = trimmedData.Substring(trimmedData.IndexOf(',') + 1);
        byte[] bytes = Convert.FromBase64String(base64);

        // cannot dispose of memoryStream or the BitmapImage is empty when the view trys to render
        MemoryStream ms = new(bytes, 0, bytes.Length);
        BitmapImage bmp = new();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.None;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();

        return bmp;
    }

    private static bool ClipboardContainsBase64Image()
    {
        string? trimmedData = null;

        try { trimmedData = System.Windows.Clipboard.GetText().Trim(); } catch { return false; }

        if (string.IsNullOrWhiteSpace(trimmedData))
            return false;
        trimmedData = CleanTeamsBase64Image(trimmedData);
        string fileType = base64ImageExtension(ref trimmedData);

        if (string.IsNullOrWhiteSpace(fileType))
            return false;

        return true;
    }

    private static string CleanTeamsBase64Image(string dirtyTeamsString)
    {
        // TODO: this is a bit hokey, but it works for now.
        // Maybe revist and make more robust.
        const string startingTag = "<img src=\"";
        const string endingTag = "\" alt=\"image\" iscopyblocked=\"false\">";

        if (!dirtyTeamsString.StartsWith(startingTag))
            return dirtyTeamsString;

        StringBuilder sb = new(dirtyTeamsString);
        sb.Replace(startingTag, "");
        sb.Replace(endingTag, "");
        return sb.ToString();
    }

    static string base64ImageExtension(ref string base64String)
    {
        // Copied this portion of the code from https://github.com/veler/DevToys
        if (base64String!.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase))
            return ".png";
        else if (base64String!.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase))
            return ".jpeg";
        else if (base64String!.StartsWith("data:image/bmp;base64,", StringComparison.OrdinalIgnoreCase))
            return ".bmp";
        else if (base64String!.StartsWith("data:image/gif;base64,", StringComparison.OrdinalIgnoreCase))
            return ".gif";
        else if (base64String!.StartsWith("data:image/x-icon;base64,", StringComparison.OrdinalIgnoreCase))
            return ".ico";
        else if (base64String!.StartsWith("data:image/svg+xml;base64,", StringComparison.OrdinalIgnoreCase))
            return ".svg";
        else if (base64String!.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase))
            return ".webp";
        else
            return string.Empty;
    }
}