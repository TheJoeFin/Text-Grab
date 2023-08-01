using System.Collections.Generic;
using System.Drawing.Imaging;

namespace Text_Grab.Utilities;

internal class FileUtilities
{
    /// <summary>
    /// Get the Filter string for all supported image types.
    /// To be used in the FileDialog class Filter Property.
    /// </summary>
    /// <returns></returns>
    /// From StackOverFlow https://stackoverflow.com/a/69318375/7438031
    /// Author https://stackoverflow.com/users/9610801/paul-nakitare
    /// Accessed on 1/6/2023
    /// Modified by Joseph Finney
    public static string GetImageFilter()
    {
        string imageExtensions = string.Empty;
        string separator = "";
        ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
        Dictionary<string, string> imageFilters = new Dictionary<string, string>();
        foreach (ImageCodecInfo codec in codecs)
        {
            if (codec.FilenameExtension is not string extension)
                continue;

            imageExtensions = $"{imageExtensions}{separator}{extension.ToLower()}";
            separator = ";";
            imageFilters.Add($"{codec.FormatDescription} files ({extension.ToLower()})", extension.ToLower());
        }
        string result = string.Empty;
        separator = "";
        //foreach (KeyValuePair<string, string> filter in imageFilters)
        //{
        //    result += $"{separator}{filter.Key}|{filter.Value}";
        //    separator = "|";
        //}
        if (!string.IsNullOrEmpty(imageExtensions))
        {
            result += $"{separator}Image files|{imageExtensions}";
        }
        return result;
    }
}
