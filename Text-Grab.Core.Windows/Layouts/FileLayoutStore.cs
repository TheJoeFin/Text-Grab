using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Utilities;
using Windows.Storage;

namespace Text_Grab.Core.Layouts;

/// <summary>
/// Layouts as files, <c>layouts/&lt;id&gt;.json</c>, beside the built-in ones.
/// </summary>
/// <remarks>
/// The folder follows <see cref="FileUtilities"/>' storage (<see cref="GetDefaultDirectory"/>): the
/// automation profile's data folder under UI tests, the package's local folder when packaged, and the
/// executable's folder otherwise. A built-in layout's ID always means the built-in layout, even if a
/// file of that name is in the folder.
/// </remarks>
public sealed class FileLayoutStore : ILayoutStore
{
    public const string FolderName = "layouts";
    private const string Extension = ".json";

    public FileLayoutStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = directory;
    }

    /// <summary>The folder the layouts are saved in. It need not exist until one is saved.</summary>
    public string Directory { get; }

    /// <summary>Where this process saves layouts.</summary>
    public static string GetDefaultDirectory()
    {
        if (AutomationProfile.Current is AutomationProfile profile)
            return Path.Combine(profile.DataDirectory, FolderName);

        if (PackageIdentity.IsPackaged())
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, FolderName);

        string? executableDirectory = Path.GetDirectoryName(FileUtilities.GetExePath());
        return Path.Combine(string.IsNullOrEmpty(executableDirectory) ? "c:\\Text-Grab" : executableDirectory, FolderName);
    }

    public async Task<IReadOnlyList<LayoutSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        List<LayoutSummary> summaries = [.. BuiltInLayouts.All.Select(layout => new LayoutSummary(layout.Id, layout.Name, IsBuiltIn: true))];

        if (!System.IO.Directory.Exists(Directory))
            return summaries;

        List<LayoutSummary> saved = [];
        foreach (string path in System.IO.Directory.EnumerateFiles(Directory, "*" + Extension))
        {
            string id = Path.GetFileNameWithoutExtension(path);
            if (!LayoutIds.IsValid(id) || BuiltInLayouts.IsBuiltIn(id))
                continue;

            LayoutReadResult read = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
            saved.Add(new LayoutSummary(id, read.Layout?.Name is { Length: > 0 } name ? name : id, IsBuiltIn: false));
        }

        summaries.AddRange(saved.OrderBy(summary => summary.Id, StringComparer.Ordinal));
        return summaries;
    }

    public async Task<LayoutReadResult> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        if (BuiltInLayouts.TryGet(id, out EtwLayoutDefinition builtIn))
            return new LayoutReadResult(builtIn, null);

        if (!LayoutIds.IsValid(id))
            return LayoutReadResult.Failed($"\"{id}\" can't be a layout ID.");

        string path = PathFor(id);
        if (!File.Exists(path))
            return LayoutReadResult.Failed($"There is no layout \"{id}\".");

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return LayoutReadResult.Failed($"The layout \"{id}\" could not be read: {ex.Message}");
        }

        LayoutReadResult read = EtwLayoutSerializer.Read(json);
        if (read.Layout is null)
            return LayoutReadResult.Failed($"The layout \"{id}\" could not be used. {read.Error}");

        // The file name is the ID: a copied file that still carries the old ID is still this layout.
        return read.Layout.Id == id ? read : new LayoutReadResult(read.Layout with { Id = id }, null);
    }

    public async Task SaveAsync(EtwLayoutDefinition layout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (!LayoutIds.IsValid(layout.Id))
            throw new ArgumentException($"\"{layout.Id}\" can't be a layout ID.", nameof(layout));

        if (BuiltInLayouts.IsBuiltIn(layout.Id))
            throw new InvalidOperationException($"\"{layout.Id}\" is a built-in layout and can't be replaced. Save a copy under another ID.");

        System.IO.Directory.CreateDirectory(Directory);
        string path = PathFor(layout.Id);
        string temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, EtwLayoutSerializer.Serialize(layout), cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, path, overwrite: true);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LayoutIds.IsValid(id) || BuiltInLayouts.IsBuiltIn(id))
            return Task.FromResult(false);

        string path = PathFor(id);
        if (!File.Exists(path))
            return Task.FromResult(false);

        File.Delete(path);
        return Task.FromResult(true);
    }

    private string PathFor(string id) => Path.Combine(Directory, id + Extension);
}
