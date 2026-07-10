using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

/// <summary>
/// Reads and writes Text Grab Grab Frame files (<c>.tggf</c>). A Grab Frame file bundles
/// the word borders, frame settings, and the source image together in a single document so
/// a Grab Frame session can be saved to disk and reopened later.
///
/// The format is a ZIP container with three entries:
/// <list type="bullet">
///   <item><c>metadata.json</c> — the <see cref="HistoryInfo"/> describing the frame
///   (language, table state, position, source mode, and the OCR text).</item>
///   <item><c>wordborders.json</c> — the serialized <see cref="System.Collections.Generic.List{T}"/>
///   of <see cref="WordBorderInfo"/>.</item>
///   <item><c>image.png</c> — the frame's source image.</item>
/// </list>
/// Reusing <see cref="HistoryInfo"/> and <see cref="WordBorderInfo"/> lets a loaded file flow
/// through the same GrabFrame code path as the History feature.
/// </summary>
public static class GrabFrameFileUtilities
{
    public const string GrabFrameFileExtension = ".tggf";

    private const string MetadataEntryName = "metadata.json";
    private const string WordBordersEntryName = "wordborders.json";
    private const string ImageEntryName = "image.png";

    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static bool IsGrabFrameFileExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension)
            && string.Equals(extension, GrabFrameFileExtension, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGrabFrameFile(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && IsGrabFrameFileExtension(Path.GetExtension(path));
    }

    /// <summary>
    /// The FileDialog filter string for Grab Frame files.
    /// </summary>
    public static string GetGrabFrameFileFilter()
    {
        return $"Text Grab Frame (*{GrabFrameFileExtension})|*{GrabFrameFileExtension}";
    }

    /// <summary>
    /// Writes a Grab Frame file to <paramref name="destinationPath"/>. The supplied
    /// <paramref name="info"/> is expected to come from <c>GrabFrame.AsHistoryItem()</c>;
    /// its <see cref="HistoryInfo.ImageContent"/> and <see cref="HistoryInfo.WordBorderInfoJson"/>
    /// are packed into the archive. The serialized metadata is taken from a copy with those
    /// transient fields cleared, so the caller's <paramref name="info"/> is left untouched.
    /// </summary>
    public static async Task<bool> SaveGrabFrameFileAsync(HistoryInfo info, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            return false;

        // Pull out the payloads that live in their own archive entries.
        string? wordBordersJson = info.WordBorderInfoJson;
        Bitmap? image = info.ImageContent;

        // Serialize a copy with the pointer fields blanked so the archive metadata does not
        // duplicate or dangle — mutating the caller's live instance here would corrupt it.
        HistoryInfo metadata = info.ShallowCopy();
        metadata.WordBorderInfoJson = null;
        metadata.WordBorderInfoFileName = null;
        metadata.ImagePath = ImageEntryName;

        string metadataJson;
        try
        {
            metadataJson = JsonSerializer.Serialize(metadata, MetadataJsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to serialize Grab Frame metadata: {ex}");
            return false;
        }

        string? directory = Path.GetDirectoryName(destinationPath);
        string tempPath = Path.Combine(
            string.IsNullOrEmpty(directory) ? Path.GetTempPath() : directory,
            $"{Guid.NewGuid():N}.tggf.tmp");

        try
        {
            await Task.Run(() =>
            {
                using (FileStream zipStream = new(tempPath, FileMode.Create, FileAccess.Write))
                using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create))
                {
                    WriteTextEntry(archive, MetadataEntryName, metadataJson);

                    if (!string.IsNullOrWhiteSpace(wordBordersJson))
                        WriteTextEntry(archive, WordBordersEntryName, wordBordersJson);

                    if (image is not null)
                        WriteImageEntry(archive, ImageEntryName, image);
                }
            });

            // Move into place only after a fully written archive so an in-progress or failed
            // save never clobbers an existing file at the destination.
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            File.Move(tempPath, destinationPath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save Grab Frame file '{destinationPath}': {ex}");
            TryDeleteFile(tempPath);
            return false;
        }
    }

    /// <summary>
    /// Reads a Grab Frame file and returns a <see cref="HistoryInfo"/> with the image loaded into
    /// <see cref="HistoryInfo.ImageContent"/> and the word borders placed inline in
    /// <see cref="HistoryInfo.WordBorderInfoJson"/>, ready to hand to <c>new GrabFrame(historyInfo)</c>.
    /// Returns <c>null</c> when the file is missing, unreadable, or malformed.
    /// </summary>
    public static async Task<HistoryInfo?> LoadGrabFrameFileAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return null;

        try
        {
            return await Task.Run(() =>
            {
                using FileStream zipStream = File.OpenRead(sourcePath);
                using ZipArchive archive = new(zipStream, ZipArchiveMode.Read);

                ZipArchiveEntry? metadataEntry = archive.GetEntry(MetadataEntryName);
                if (metadataEntry is null)
                    return null;

                HistoryInfo? info = JsonSerializer.Deserialize<HistoryInfo>(
                    ReadEntryText(metadataEntry), MetadataJsonOptions);

                if (info is null)
                    return null;

                ZipArchiveEntry? wordBordersEntry = archive.GetEntry(WordBordersEntryName);
                info.WordBorderInfoJson = wordBordersEntry is null ? null : ReadEntryText(wordBordersEntry);
                info.WordBorderInfoFileName = null;

                ZipArchiveEntry? imageEntry = archive.GetEntry(ImageEntryName);
                if (imageEntry is not null)
                    info.ImageContent = ReadImageEntry(imageEntry);

                if (string.IsNullOrWhiteSpace(info.ID))
                    info.ID = Guid.NewGuid().ToString();

                return info;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load Grab Frame file '{sourcePath}': {ex}");
            return null;
        }
    }

    private static void WriteTextEntry(ZipArchive archive, string entryName, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream entryStream = entry.Open();
        using StreamWriter writer = new(entryStream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void WriteImageEntry(ZipArchive archive, string entryName, Bitmap image)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

        // GDI+ requires a seekable stream to encode a PNG; the zip entry stream is not
        // seekable, so encode into memory first and copy the bytes across.
        using MemoryStream imageStream = new();
        image.Save(imageStream, ImageFormat.Png);
        imageStream.Position = 0;

        using Stream entryStream = entry.Open();
        imageStream.CopyTo(entryStream);
    }

    private static Bitmap ReadImageEntry(ZipArchiveEntry entry)
    {
        // Copy into memory and build a self-contained Bitmap. A Bitmap constructed directly
        // from a stream keeps the stream alive for its lifetime, so clone into an owned bitmap.
        using Stream entryStream = entry.Open();
        using MemoryStream imageStream = new();
        entryStream.CopyTo(imageStream);
        imageStream.Position = 0;

        using Bitmap decoded = new(imageStream);
        return new Bitmap(decoded);
    }

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using Stream entryStream = entry.Open();
        using StreamReader reader = new(entryStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to delete temporary Grab Frame file '{path}': {ex}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Access denied deleting temporary Grab Frame file '{path}': {ex}");
        }
    }
}
