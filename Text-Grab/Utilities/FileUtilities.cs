using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Text_Grab.Utilities;

public class FileUtilities
{
    #region Public Methods

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

    public static string GetPathToLocalFile(string imageRelativePath)
    {
        Uri codeBaseUrl = new(System.AppDomain.CurrentDomain.BaseDirectory);
        string codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
        string? dirPath = Path.GetDirectoryName(codeBasePath);

        if (dirPath is null)
            dirPath = "";

        return Path.Combine(dirPath, imageRelativePath);
    }

    public static Task<bool> SaveImageFile(Bitmap image, string filename, FileStorageKind storageKind)
    {
        if (ImplementAppOptions.IsPackaged())
            return SaveImagePackaged(image, filename, storageKind);

        return SaveImageFileUnpackaged(image, filename, storageKind);
    }

    public static Task<bool> SaveTextFile(string textContent, string filename, FileStorageKind storageKind)
    {
        if (ImplementAppOptions.IsPackaged())
            return SaveTextFilePackaged(textContent, filename, storageKind);

        return SaveTextFileUnpackaged(textContent, filename, storageKind);
    }

    #endregion Public Methods

    #region Private Methods

    private static async Task<bool> SaveTextFilePackaged(string textContent, string filename, FileStorageKind storageKind)
    {
        StorageFolder storageFolder = await GetStorageFolderPackaged(filename, storageKind);
    }

    private static async Task<bool> SaveTextFileUnpackaged(string textContent, string filename, FileStorageKind storageKind)
    {
        string folderPath = GetFolderPathUnpackaged(filename, storageKind);
        string filePath = Path.Combine(folderPath, filename);

        if (string.IsNullOrEmpty(folderPath))
            return false;

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        if (File.Exists(filePath))
            File.Delete(filePath);

        using FileStream fs = File.Create(filePath);
        AddText(fs, textContent);
        return true;
    }

    public static void AddText(FileStream fs, string value)
    {
        byte[] info = new UTF8Encoding(true).GetBytes(value);
        fs.Write(info, 0, info.Length);
    }

    private static async Task<StorageFolder> GetStorageFolderPackaged(string fileName, FileStorageKind storageKind)
    {
        switch (storageKind)
        {
            case FileStorageKind.Absolute:
                return await StorageFolder.GetFolderFromPathAsync(fileName);
            case FileStorageKind.WithExe:
                return ApplicationData.Current.LocalFolder;
            case FileStorageKind.WithHistory:
                return await ApplicationData.Current.LocalFolder.CreateFolderAsync("history", CreationCollisionOption.OpenIfExists);
            default:
                break;
        }

        return ApplicationData.Current.LocalCacheFolder;
    }


    private static string GetFolderPathUnpackaged(string filename, FileStorageKind storageKind)
    {
        string? exePath = Path.GetDirectoryName(System.AppContext.BaseDirectory);
        string historyDirectory = $"{exePath}\\history";

        switch (storageKind)
        {
            case FileStorageKind.Absolute:
                return filename;
            case FileStorageKind.WithExe:
                return $"{exePath!}";
            case FileStorageKind.WithHistory:
                return $"{historyDirectory}";
            default:
                break;
        }

        return $"c:\\";
    }
    
    private static async Task<bool> SaveImagePackaged(Bitmap image, string filename, FileStorageKind storageKind)
    {
        try
        {
            StorageFolder historyFolder = await GetStorageFolderPackaged(filename, storageKind);
            StorageFile imageFile = await historyFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
            using IRandomAccessStream randomAccessStream = await imageFile.OpenAsync(FileAccessMode.ReadWrite);
            image.Save(randomAccessStream.AsStream(), ImageFormat.Bmp);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool SaveHistoryImageUnpackaged(Bitmap image, string filename)
    {
        string? exePath = Path.GetDirectoryName(System.AppContext.BaseDirectory);
        string historyDirectory = $"{exePath}\\history";

        try
        {
            if (!Directory.Exists(historyDirectory))
                Directory.CreateDirectory(historyDirectory);
            string imgPath = $"{historyDirectory}\\{filename}";
            image.Save(imgPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SaveImageFileUnpackaged(Bitmap image, string filename, FileStorageKind storageKind)
    {
        switch (storageKind)
        {
            case FileStorageKind.Absolute:
                break;
            case FileStorageKind.WithExe:
                break;
            case FileStorageKind.WithHistory:
                return SaveHistoryImageUnpackaged(image, filename);
            default:
                break;
        }

        return false;
    }

    #endregion Private Methods
}
