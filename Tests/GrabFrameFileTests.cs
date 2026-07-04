using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

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
                BorderRect = new Rect(1, 2, 30, 12),
                LineNumber = 0,
            },
            new()
            {
                Word = "World",
                BorderRect = new Rect(35, 2, 32, 12),
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
            PositionRect = new Rect(100, 120, 400, 300),
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
            Assert.Equal(new Rect(100, 120, 400, 300), loaded.PositionRect);

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
    public async Task LoadGrabFrameFileAsync_ReturnsNull_ForMissingFile()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tggf");

        HistoryInfo? loaded = await GrabFrameFileUtilities.LoadGrabFrameFileAsync(missingPath);

        Assert.Null(loaded);
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
