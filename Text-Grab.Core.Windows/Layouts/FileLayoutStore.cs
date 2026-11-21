using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>The store's metadata file: the global default layout today, and E6 <c>defaults</c>' per-file-kind entries later.</summary>
    public const string MetadataFileName = ".store.json";

    private const string GlobalDefaultMember = "globalDefault";
    private const int MaxMoveAttempts = 6;

    private static readonly JsonDocumentOptions MetadataDocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim writeGate = new(1, 1);

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
        if (CheckBeforeReading(id) is LayoutReadResult early)
            return early;

        string json;
        try
        {
            json = await File.ReadAllTextAsync(PathFor(id), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return LayoutReadResult.Failed($"The layout \"{id}\" could not be read: {ex.Message}");
        }

        return Parse(id, json);
    }

    public LayoutReadResult Load(string id)
    {
        if (CheckBeforeReading(id) is LayoutReadResult early)
            return early;

        string json;
        try
        {
            json = File.ReadAllText(PathFor(id));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return LayoutReadResult.Failed($"The layout \"{id}\" could not be read: {ex.Message}");
        }

        return Parse(id, json);
    }

    // The built-in layout, or a reason there is nothing to read; null when the file is there to read.
    private LayoutReadResult? CheckBeforeReading(string id)
    {
        if (BuiltInLayouts.TryGet(id, out EtwLayoutDefinition builtIn))
            return new LayoutReadResult(builtIn, null);

        if (!LayoutIds.IsValid(id))
            return LayoutReadResult.Failed($"\"{id}\" can't be a layout ID.");

        return File.Exists(PathFor(id)) ? null : LayoutReadResult.Failed($"There is no layout \"{id}\".");
    }

    private static LayoutReadResult Parse(string id, string json)
    {
        LayoutReadResult read = EtwLayoutSerializer.Read(json);
        if (read.Layout is null)
            return LayoutReadResult.Failed($"The layout \"{id}\" could not be used. {read.Error}");

        // The file name is the ID: a copied file that still carries the old ID is still this layout.
        return read.Layout.Id == id ? read : new LayoutReadResult(read.Layout with { Id = id }, null);
    }

    public LayoutStoreDefaults ReadDefaults()
    {
        string path = MetadataPath;
        if (!File.Exists(path))
            return new LayoutStoreDefaults(null);

        try
        {
            return ParseDefaults(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new LayoutStoreDefaults(null, $"The layout settings could not be read: {ex.Message}");
        }
    }

    public async Task<LayoutStoreDefaults> ReadDefaultsAsync(CancellationToken cancellationToken = default)
    {
        string path = MetadataPath;
        if (!File.Exists(path))
            return new LayoutStoreDefaults(null);

        try
        {
            return ParseDefaults(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new LayoutStoreDefaults(null, $"The layout settings could not be read: {ex.Message}");
        }
    }

    public async Task SetGlobalDefaultAsync(string? id, CancellationToken cancellationToken = default)
    {
        if (id is not null && !LayoutIds.IsValid(id))
            throw new ArgumentException($"\"{id}\" can't be a layout ID.", nameof(id));

        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Every other member of the file is kept: E6 `defaults` adds its own.
            JsonObject metadata = [];
            if (File.Exists(MetadataPath))
            {
                try
                {
                    if (JsonNode.Parse(await File.ReadAllTextAsync(MetadataPath, cancellationToken).ConfigureAwait(false), documentOptions: MetadataDocumentOptions) is JsonObject existing)
                        metadata = existing;
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                {
                    // A file that cannot be read is replaced.
                }
            }

            if (id is null)
                metadata.Remove(GlobalDefaultMember);
            else
                metadata[GlobalDefaultMember] = id;

            await WriteAtomicallyCoreAsync(MetadataPath, metadata.ToJsonString(IndentedJsonOptions), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeGate.Release();
        }
    }

    private static LayoutStoreDefaults ParseDefaults(string json)
    {
        try
        {
            if (JsonNode.Parse(json, documentOptions: MetadataDocumentOptions) is not JsonObject metadata)
                return new LayoutStoreDefaults(null, "The layout settings file is not valid.");

            if (metadata[GlobalDefaultMember] is not JsonNode node)
                return new LayoutStoreDefaults(null);

            string? id = node is JsonValue value && value.TryGetValue(out string? text) ? text : null;
            return LayoutIds.IsValid(id)
                ? new LayoutStoreDefaults(id)
                : new LayoutStoreDefaults(null, "The default layout in the layout settings is not a layout ID.");
        }
        catch (JsonException ex)
        {
            return new LayoutStoreDefaults(null, $"The layout settings file is not valid: {ex.Message}");
        }
    }

    public async Task SaveAsync(EtwLayoutDefinition layout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (!LayoutIds.IsValid(layout.Id))
            throw new ArgumentException($"\"{layout.Id}\" can't be a layout ID.", nameof(layout));

        if (BuiltInLayouts.IsBuiltIn(layout.Id))
            throw new InvalidOperationException($"\"{layout.Id}\" is a built-in layout and can't be replaced. Save a copy under another ID.");

        await WriteAtomicallyAsync(PathFor(layout.Id), EtwLayoutSerializer.Serialize(layout), cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteAtomicallyAsync(string path, string content, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteAtomicallyCoreAsync(path, content, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeGate.Release();
        }
    }

    private async Task WriteAtomicallyCoreAsync(string path, string content, CancellationToken cancellationToken)
    {
        System.IO.Directory.CreateDirectory(Directory);
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, cancellationToken).ConfigureAwait(false);
            await MoveWithRetryAsync(temporaryPath, path, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static async Task MoveWithRetryAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        int delayMs = 10;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < MaxMoveAttempts && ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                delayMs *= 2;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
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

    // A leading '.' can never start a layout ID, so no layout can have this name and the listing skips it.
    private string MetadataPath => Path.Combine(Directory, MetadataFileName);
}
