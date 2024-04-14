using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Streaming.Adaptive;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Text_Grab.Utilities;

public class FileUtilities
{
    #region Public Methods

    public static Task<Bitmap?> GetImageFileAsync(string fileName, FileStorageKind storageKind)
    {
        if (AppUtilities.IsPackaged())
            return GetImageFilePackaged(fileName, storageKind);

        return GetImageFileUnpackaged(fileName, storageKind);
    }

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

    public async static Task<string> GetPathToHistory()
    {
        if (AppUtilities.IsPackaged())
        {
            StorageFolder historyFolder = await GetStorageFolderPackaged("", FileStorageKind.WithHistory);
            return historyFolder.Path;
        }

        return GetFolderPathUnpackaged("", FileStorageKind.WithHistory);
    }

    public static Task<string> GetTextFileAsync(string fileName, FileStorageKind storageKind)
    {
        if (AppUtilities.IsPackaged())
            return GetTextFilePackaged(fileName, storageKind);

        return GetTextFileUnpackaged(fileName, storageKind);
    }

    public static Task<bool> SaveImageFile(Bitmap image, string filename, FileStorageKind storageKind)
    {
        if (AppUtilities.IsPackaged())
            return SaveImagePackaged(image, filename, storageKind);

        return SaveImageFileUnpackaged(image, filename, storageKind);
    }

    public static Task<bool> SaveTextFile(string textContent, string filename, FileStorageKind storageKind)
    {
        if (AppUtilities.IsPackaged())
            return SaveTextFilePackaged(textContent, filename, storageKind);

        return SaveTextFileUnpackaged(textContent, filename, storageKind);
    }

    private async static Task<Bitmap?> GetImageFilePackaged(string fileName, FileStorageKind storageKind)
    {
        StorageFolder folder = await GetStorageFolderPackaged(fileName, storageKind);

        try
        {
            StorageFile file = await folder.GetFileAsync(fileName);
            return new Bitmap(file.Path);
        }
        catch
        {
            return null;
        }
    }
    
#pragma warning disable CS1998
    private static async Task<Bitmap?> GetImageFileUnpackaged(string fileName, FileStorageKind storageKind)
    {
        string folderPath = GetFolderPathUnpackaged(fileName, storageKind);
        string filePath = Path.Combine(folderPath, fileName);

        if (!File.Exists(filePath))
            return null;

        return new Bitmap(filePath);
    }
    private async static Task<string> GetTextFilePackaged(string fileName, FileStorageKind storageKind)
    {
        try
        {
            StorageFolder folder = await GetStorageFolderPackaged(fileName, storageKind);

            if (storageKind == FileStorageKind.Absolute)
                fileName = Path.GetFileName(fileName);

            StorageFile file = await folder.GetFileAsync(fileName);
            using Stream stream = await file.OpenStreamForReadAsync();
            StreamReader streamReader = new(stream);
            return streamReader.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> GetTextFileUnpackaged(string fileName, FileStorageKind storageKind)
    {
        string folderPath = GetFolderPathUnpackaged(fileName, storageKind);
        string filePath = Path.Combine(folderPath, fileName);

        if (!File.Exists(filePath))
            return string.Empty;

        return await File.ReadAllTextAsync(filePath);
    }
    #endregion Public Methods

    #region Private Methods

    private static void AddText(FileStream fs, string value)
    {
        byte[] info = new UTF8Encoding(true).GetBytes(value);
        fs.Write(info, 0, info.Length);
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

    private static async Task<StorageFolder> GetStorageFolderPackaged(string fileName, FileStorageKind storageKind)
    {
        switch (storageKind)
        {
            case FileStorageKind.Absolute:
                string? dirPath = Path.GetDirectoryName(fileName);
                StorageFolder absoluteFolder = await StorageFolder.GetFolderFromPathAsync(dirPath);
                return absoluteFolder;
            case FileStorageKind.WithExe:
                return ApplicationData.Current.LocalFolder;
            case FileStorageKind.WithHistory:
                ApplicationData currentAppData = ApplicationData.Current;
                StorageFolder storageFolder = await currentAppData.LocalFolder.CreateFolderAsync("history", CreationCollisionOption.OpenIfExists);
                return storageFolder;
            default:
                break;
        }

        return ApplicationData.Current.LocalCacheFolder;
    }

    private static async Task<bool> SaveImageFileUnpackaged(Bitmap image, string filename, FileStorageKind storageKind)
    {
        string folderPath = GetFolderPathUnpackaged(filename, storageKind);
        string filePath = Path.Combine(folderPath, filename);

        if (string.IsNullOrEmpty(folderPath))
            return false;

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        if (File.Exists(filePath))
            File.Delete(filePath);

        image.Save(filePath);
        return true;
    }

    private static async Task<bool> SaveImagePackaged(Bitmap image, string filename, FileStorageKind storageKind)
    {
        try
        {
            StorageFolder historyFolder = await GetStorageFolderPackaged(filename, storageKind);
            StorageFile imageFile = await historyFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
            image.Save(imageFile.Path, ImageFormat.Bmp);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SaveTextFilePackaged(string textContent, string filename, FileStorageKind storageKind)
    {
        try
        {
            StorageFolder storageFolder = await GetStorageFolderPackaged(filename, storageKind);

            if (storageKind == FileStorageKind.Absolute)
                filename = Path.GetFileName(filename);

            StorageFile textFile = await storageFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
            using IRandomAccessStream randomAccessStream = await textFile.OpenAsync(FileAccessMode.ReadWrite);
            DataWriter dataWriter = new(randomAccessStream);
            dataWriter.WriteString(textContent);
            await dataWriter.StoreAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SaveTextFileUnpackaged(string textContent, string filename, FileStorageKind storageKind)
    {
        string folderPath = GetFolderPathUnpackaged(filename, storageKind);
        string filePath = Path.Combine(folderPath, filename);

        if (string.IsNullOrEmpty(folderPath))
            return false;

        if (!Directory.Exists(folderPath) && storageKind != FileStorageKind.Absolute)
            Directory.CreateDirectory(folderPath);

        if (File.Exists(filePath))
            File.Delete(filePath);

        using FileStream fs = File.Create(filePath);
        AddText(fs, textContent);
        return true;
    }
#pragma warning restore CS1998
    
    public async static void TryDeleteHistoryDirectory()
    {
        FileStorageKind historyFolderKind = FileStorageKind.WithHistory;
        if (AppUtilities.IsPackaged())
        {
            StorageFolder historyFolder = await GetStorageFolderPackaged("", historyFolderKind);

            try
            {
                await historyFolder.DeleteAsync();
            }
            catch { }
            return;
        }

        string historyDirectory = GetFolderPathUnpackaged("", historyFolderKind);

        try
        {
            Directory.Delete(historyDirectory, true);
        }
        catch { }
    }
    #endregion Private Methods
}
