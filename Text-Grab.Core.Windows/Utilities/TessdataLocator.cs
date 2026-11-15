using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage;

namespace Text_Grab.Utilities;

public enum TessdataSource
{
    /// <summary>Ships inside the app folder (read-only).</summary>
    Bundled,

    /// <summary>Lives in the user-writable tessdata folder (downloaded by the user).</summary>
    Downloaded,
}

public sealed record TessdataLanguageFile(string Tag, string Path, TessdataSource Source, long SizeBytes);

/// <summary>
/// Finds the tessdata folders used by the bundled engine, using the same location rules as the
/// settings storage: the package's local folder (MSIX), next to the exe (unpackaged) or the
/// automation profile directory (UI tests).
/// </summary>
public static class TessdataLocator
{
    public const string FolderName = "tessdata";
    public const string BundledFolderName = "tessdata-bundled";
    public const string FileExtension = ".traineddata";

    /// <summary>Pure resolver so the three install cases can be unit tested.</summary>
    public static string ResolveWritableFolder(string? automationProfileRoot, bool isPackaged, string? packagedLocalFolder, string? exeDirectory)
    {
        if (!string.IsNullOrEmpty(automationProfileRoot))
            return Path.Combine(automationProfileRoot, FolderName);

        if (isPackaged && !string.IsNullOrEmpty(packagedLocalFolder))
            return Path.Combine(packagedLocalFolder, FolderName);

        return Path.Combine(exeDirectory ?? "c:\\Text-Grab", FolderName);
    }

    /// <summary>The user-writable folder. Not created here; see <see cref="EnsureWritableFolder"/>.</summary>
    public static string GetWritableFolder()
    {
        bool packaged = AutomationProfile.Current is null && PackageIdentity.IsPackaged();
        return ResolveWritableFolder(
            AutomationProfile.Current?.RootPath,
            packaged,
            packaged ? ApplicationData.Current.LocalFolder.Path : null,
            Path.GetDirectoryName(FileUtilities.GetExePath()));
    }

    public static string EnsureWritableFolder()
    {
        string folder = GetWritableFolder();
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// The read-only folder that ships with the app (contains the bundled English model). It has its
    /// own name because the unpackaged writable folder is <c>&lt;exe folder&gt;\tessdata</c>, and the two
    /// must stay distinct for the Bundled/Downloaded source and override rules to work.
    /// </summary>
    public static string GetBundledFolder()
    {
        string baseFolder = Path.Combine(AppContext.BaseDirectory, BundledFolderName);
        if (Directory.Exists(baseFolder))
            return baseFolder;

        // Single-file builds can report an extraction folder as the base directory.
        string? exeDirectory = Path.GetDirectoryName(FileUtilities.GetExePath());
        if (exeDirectory is not null)
        {
            string besideExe = Path.Combine(exeDirectory, BundledFolderName);
            if (Directory.Exists(besideExe))
                return besideExe;
        }

        return baseFolder;
    }

    /// <summary>
    /// Every language with a model file. A downloaded file wins over a bundled file of the same
    /// tag, so a larger downloaded <c>eng</c> overrides the bundled fast one.
    /// </summary>
    public static IReadOnlyList<TessdataLanguageFile> GetInstalledLanguages() =>
        Merge(
            Enumerate(GetBundledFolder(), TessdataSource.Bundled),
            Enumerate(GetWritableFolder(), TessdataSource.Downloaded));

    public static IReadOnlyList<TessdataLanguageFile> Merge(
        IEnumerable<TessdataLanguageFile> bundled,
        IEnumerable<TessdataLanguageFile> downloaded)
    {
        Dictionary<string, TessdataLanguageFile> byTag = new(StringComparer.OrdinalIgnoreCase);
        foreach (TessdataLanguageFile file in bundled)
            byTag[file.Tag] = file;
        foreach (TessdataLanguageFile file in downloaded)
            byTag[file.Tag] = file;

        return [.. byTag.Values.OrderBy(file => file.Tag, StringComparer.OrdinalIgnoreCase)];
    }

    public static IEnumerable<TessdataLanguageFile> Enumerate(string folder, TessdataSource source)
    {
        if (!Directory.Exists(folder))
            yield break;

        foreach (string path in Directory.EnumerateFiles(folder, $"*{FileExtension}"))
        {
            string tag = Path.GetFileNameWithoutExtension(path);
            // osd is orientation/script detection data, not a recognition language.
            if (string.Equals(tag, "osd", StringComparison.OrdinalIgnoreCase))
                continue;

            long size = 0;
            try { size = new FileInfo(path).Length; }
            catch (IOException) { }

            yield return new TessdataLanguageFile(tag, path, source, size);
        }
    }

    /// <summary>
    /// Returns the folder an engine should be created with for a language spec such as
    /// <c>eng</c> or <c>eng+deu</c>, or null when a part of the spec has no model file. One engine
    /// reads all its languages from one folder, so when a spec mixes bundled and downloaded files
    /// the bundled ones are copied into the writable folder first.
    /// </summary>
    public static string? ResolveFolderForSpec(string languageSpec, IReadOnlyList<TessdataLanguageFile> installed)
    {
        string[] parts = languageSpec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return null;

        List<TessdataLanguageFile> files = [];
        foreach (string part in parts)
        {
            TessdataLanguageFile? file = installed.FirstOrDefault(f => string.Equals(f.Tag, part, StringComparison.OrdinalIgnoreCase));
            if (file is null)
                return null;
            files.Add(file);
        }

        if (files.All(f => f.Source == TessdataSource.Bundled))
            return Path.GetDirectoryName(files[0].Path);

        string writable = EnsureWritableFolder();
        foreach (TessdataLanguageFile file in files.Where(f => f.Source == TessdataSource.Bundled))
        {
            string target = Path.Combine(writable, Path.GetFileName(file.Path));
            if (!File.Exists(target))
                File.Copy(file.Path, target);
        }

        return writable;
    }
}
