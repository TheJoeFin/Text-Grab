using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

// Moved wholesale in 7b: GrabFrameFileUtilities followed HistoryInfo to Core.Windows once the
// HistoryInfo blocker cleared (a8591aa), and every assertion here is against that headless pair
// (GrabFrameFileUtilities, HistoryInfo/WordBorderInfo) with no WPF type in sight - the file had
// an unused `using System.Windows;` from before that move, since dropped.
public class GrabFrameFileTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsMetadataWordBordersAndImage()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tggf");

        List<WordBorderInfo> wordBorders =
        [
            new()
            {
                Word = "Hello",
                BorderRect = new RectangleF(1, 2, 30, 12),
                LineNumber = 0,
            },
            new()
            {
                Word = "World",
                BorderRect = new RectangleF(35, 2, 32, 12),
                LineNumber = 0,
            },
        ];

        HistoryInfo info = new()
        {
            ID = "round-trip-id",
            TextContent = "Hello World",
            SourceMode = TextGrabMode.GrabFrame,
            IsTable = true,
            LanguageTag = "en-US",
            LanguageKind = LanguageKind.Global,
            PositionRect = new RectangleF(100, 120, 400, 300),
            WordBorderInfoJson = JsonSerializer.Serialize(wordBorders),
            ImageContent = new Bitmap(64, 48),
        };

        try
        {
            bool saved = await GrabFrameFileUtilities.SaveGrabFrameFileAsync(info, tempPath);
            Assert.True(saved);
            Assert.True(File.Exists(tempPath));

            HistoryInfo? loaded = await GrabFrameFileUtilities.LoadGrabFrameFileAsync(tempPath);

            Assert.NotNull(loaded);
            Assert.Equal("round-trip-id", loaded!.ID);
            Assert.Equal("Hello World", loaded.TextContent);
            Assert.Equal(TextGrabMode.GrabFrame, loaded.SourceMode);
            Assert.True(loaded.IsTable);
            Assert.Equal("en-US", loaded.LanguageTag);
            Assert.Equal(LanguageKind.Global, loaded.LanguageKind);
            Assert.Equal(new RectangleF(100, 120, 400, 300), loaded.PositionRect);

            Assert.NotNull(loaded.ImageContent);
            Assert.Equal(64, loaded.ImageContent!.Width);
            Assert.Equal(48, loaded.ImageContent.Height);

            Assert.False(string.IsNullOrWhiteSpace(loaded.WordBorderInfoJson));
            List<WordBorderInfo>? loadedBorders =
                JsonSerializer.Deserialize<List<WordBorderInfo>>(loaded.WordBorderInfoJson!);
            Assert.NotNull(loadedBorders);
            Assert.Equal(2, loadedBorders!.Count);
            Assert.Equal("Hello", loadedBorders[0].Word);
            Assert.Equal("World", loadedBorders[1].Word);

            loaded.ImageContent?.Dispose();
        }
        finally
        {
            info.ImageContent?.Dispose();
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task SaveGrabFrameFileAsync_DoesNotMutateSuppliedInfo()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tggf");

        string originalWordBordersJson = JsonSerializer.Serialize(new List<WordBorderInfo>
        {
            new() { Word = "Hello", BorderRect = new RectangleF(1, 2, 30, 12), LineNumber = 0 },
        });
        Bitmap originalImage = new(64, 48);

        HistoryInfo info = new()
        {
            ID = "no-mutate-id",
            TextContent = "Hello World",
            SourceMode = TextGrabMode.GrabFrame,
            ImagePath = "original-image-path.png",
            WordBorderInfoJson = originalWordBordersJson,
            WordBorderInfoFileName = "original-borders.json",
            ImageContent = originalImage,
        };

        try
        {
            bool saved = await GrabFrameFileUtilities.SaveGrabFrameFileAsync(info, tempPath);
            Assert.True(saved);

            // The save packs these fields into the archive from a copy; the caller's instance
            // must be left exactly as it was passed in.
            Assert.Equal(originalWordBordersJson, info.WordBorderInfoJson);
            Assert.Equal("original-borders.json", info.WordBorderInfoFileName);
            Assert.Equal("original-image-path.png", info.ImagePath);
            Assert.Same(originalImage, info.ImageContent);
        }
        finally
        {
            info.ImageContent?.Dispose();
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task SaveGrabFrameFileAsync_PreservesExistingFile_WhenAtomicReplaceFails()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tggf");
        byte[] originalContent = "existing grab frame content"u8.ToArray();
        await File.WriteAllBytesAsync(tempPath, originalContent, TestContext.Current.CancellationToken);

        HistoryInfo info = new()
        {
            ID = "replacement",
            TextContent = "replacement content",
            SourceMode = TextGrabMode.GrabFrame,
        };

        bool saved;
        try
        {
            using (FileStream lockedFile = new(tempPath, FileMode.Open, FileAccess.Read, FileShare.None))
                saved = await GrabFrameFileUtilities.SaveGrabFrameFileAsync(info, tempPath);

            Assert.False(saved);
            Assert.Equal(
                originalContent,
                await File.ReadAllBytesAsync(tempPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task SaveGrabFrameFileAsync_AtomicallyReplacesExistingFile()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tggf");
        await File.WriteAllTextAsync(tempPath, "old content", TestContext.Current.CancellationToken);

        HistoryInfo info = new()
        {
            ID = "replacement",
            TextContent = "new content",
            SourceMode = TextGrabMode.GrabFrame,
        };

        try
        {
            Assert.True(await GrabFrameFileUtilities.SaveGrabFrameFileAsync(info, tempPath));

            HistoryInfo? loaded = await GrabFrameFileUtilities.LoadGrabFrameFileAsync(tempPath);
            Assert.NotNull(loaded);
            Assert.Equal("new content", loaded!.TextContent);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task LoadGrabFrameFileAsync_ReturnsNull_ForMissingFile()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tggf");

        HistoryInfo? loaded = await GrabFrameFileUtilities.LoadGrabFrameFileAsync(missingPath);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadGrabFrameFileAsync_ReturnsNull_ForOversizedMetadata()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tggf");

        try
        {
            using (FileStream zipStream = new(tempPath, FileMode.Create, FileAccess.Write))
            using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create))
            using (StreamWriter writer = new(archive.CreateEntry("metadata.json").Open()))
                writer.Write(new string('a', checked((int)GrabFrameFileUtilities.MaxMetadataBytes + 1)));

            HistoryInfo? loaded = await GrabFrameFileUtilities.LoadGrabFrameFileAsync(tempPath);

            Assert.Null(loaded);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Theory]
    [InlineData(8_000, 5_000, true)]
    [InlineData(8_001, 5_000, false)]
    [InlineData(16_385, 1, false)]
    public void AreImageDimensionsAllowed_EnforcesDimensionAndPixelLimits(int width, int height, bool expected)
    {
        Assert.Equal(expected, GrabFrameFileUtilities.AreImageDimensionsAllowed(width, height));
    }

    [Theory]
    [InlineData("frame.tggf", true)]
    [InlineData("frame.TGGF", true)]
    [InlineData("image.png", false)]
    [InlineData("", false)]
    public void IsGrabFrameFile_MatchesExtension(string path, bool expected)
    {
        Assert.Equal(expected, GrabFrameFileUtilities.IsGrabFrameFile(path));
    }
}
