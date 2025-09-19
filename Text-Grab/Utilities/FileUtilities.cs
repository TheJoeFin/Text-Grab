using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
        Dictionary<string, string> imageFilters = [];
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

        if (!string.IsNullOrEmpty(imageExtensions))
        {
            result += $"{separator}Image files|{imageExtensions}";
        }
        return result;
    }

    public static string GetPathToLocalFile(string imageRelativePath)
    {
        string? executableDirectory = Path.GetDirectoryName(GetExePath());

        if (executableDirectory is null)
            throw new NullReferenceException($"{nameof(executableDirectory)} cannot be null");

        return Path.Combine(executableDirectory, imageRelativePath);
    }

    public static async Task<string> GetPathToHistory()
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

    private static async Task<Bitmap?> GetImageFilePackaged(string fileName, FileStorageKind storageKind)
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
    private static async Task<string> GetTextFilePackaged(string fileName, FileStorageKind storageKind)
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
        string defaultFallback = "c:\\Text-Grab";

        string? executableDirectory = Path.GetDirectoryName(GetExePath());

        if (string.IsNullOrEmpty(executableDirectory))
            return defaultFallback;

        string historyDirectory = Path.Combine(executableDirectory, "history");

        switch (storageKind)
        {
            case FileStorageKind.Absolute:
                return filename;
            case FileStorageKind.WithExe:
                return executableDirectory;
            case FileStorageKind.WithHistory:
                return historyDirectory;
            default:
                break;
        }

        return defaultFallback;
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

    public static async void TryDeleteHistoryDirectory()
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

    public static string GetExePath()
    {
        if (!string.IsNullOrEmpty(Environment.ProcessPath))
            return Environment.ProcessPath;

        // For single-file self-contained apps, use the original executable location
        if (IsExtractedSingleFile())
        {
            // Try to get the original path from command line args or process info
            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            {
                string? processDir = Path.GetDirectoryName(processPath);
                if (!string.IsNullOrEmpty(processDir))
                {
                    return processDir;
                }
            }
        }
        
        // For framework-dependent apps, use the base directory approach
        string baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            // Remove trailing slash/backslash to ensure consistency
            return baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        
        // Fallback to process directory
        string? fallbackProcessPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(fallbackProcessPath))
        {
            string? processDir = Path.GetDirectoryName(fallbackProcessPath);
            if (!string.IsNullOrEmpty(processDir))
            {
                return processDir;
            }
        }
        
        return "";
    }

    private static bool IsExtractedSingleFile()
    {
        string baseDir = AppContext.BaseDirectory;

        if (baseDir.Contains(@"AppData\Local\Temp\Text-Grab"))
            return true;

        return false;

        // return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR"));
    }
    #endregion Private Methods
}
