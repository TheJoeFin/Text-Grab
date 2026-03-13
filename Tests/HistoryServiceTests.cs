using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Tests;

[Collection("History service")]
public class HistoryServiceTests
{
    private static readonly JsonSerializerOptions HistoryJsonOptions = new()
    {
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [WpfFact]
    public async Task TextHistory_LazyLoadsAgainAfterRelease()
    {
        await SaveHistoryFileAsync(
            "HistoryTextOnly.json",
            [
                new HistoryInfo
                {
                    ID = "text-1",
                    CaptureDateTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
                    TextContent = "first text history",
                    SourceMode = TextGrabMode.EditText
                }
            ]);

        HistoryService historyService = new();

        Assert.Equal("first text history", historyService.GetLastTextHistory());

        historyService.ReleaseLoadedHistories();

        await SaveHistoryFileAsync(
            "HistoryTextOnly.json",
            [
                new HistoryInfo
                {
                    ID = "text-2",
                    CaptureDateTime = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero),
                    TextContent = "second text history",
                    SourceMode = TextGrabMode.EditText
                }
            ]);

        Assert.Equal("second text history", historyService.GetLastTextHistory());
    }

    [WpfFact]
    public async Task ImageHistory_LazyLoadsAgainAfterRelease()
    {
        await SaveHistoryFileAsync(
            "HistoryWithImage.json",
            [
                new HistoryInfo
                {
                    ID = "image-1",
                    CaptureDateTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
                    TextContent = "first image history",
                    ImagePath = "one.bmp",
                    SourceMode = TextGrabMode.GrabFrame
                }
            ]);

        HistoryService historyService = new();

        Assert.Equal("one.bmp", Assert.Single(historyService.GetRecentGrabs()).ImagePath);

        historyService.ReleaseLoadedHistories();

        await SaveHistoryFileAsync(
            "HistoryWithImage.json",
            [
                new HistoryInfo
                {
                    ID = "image-2",
                    CaptureDateTime = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero),
                    TextContent = "second image history",
                    ImagePath = "two.bmp",
                    SourceMode = TextGrabMode.Fullscreen
                }
            ]);

        Assert.Equal("two.bmp", Assert.Single(historyService.GetRecentGrabs()).ImagePath);
        Assert.Equal("image-2", historyService.GetLastFullScreenGrabInfo()?.ID);
    }

    [WpfFact]
    public async Task ImageHistory_MigratesInlineWordBorderJsonToSidecarStorage()
    {
        string inlineWordBorderJson = JsonSerializer.Serialize(
            new List<WordBorderInfo>
            {
                new()
                {
                    Word = "hello",
                    BorderRect = new Rect(1, 2, 30, 40),
                    LineNumber = 1,
                    ResultColumnID = 2,
                    ResultRowID = 3
                }
            },
            HistoryJsonOptions);

        await SaveHistoryFileAsync(
            "HistoryWithImage.json",
            [
                new HistoryInfo
                {
                    ID = "image-with-borders",
                    CaptureDateTime = new DateTimeOffset(2024, 1, 3, 12, 0, 0, TimeSpan.Zero),
                    TextContent = "history with borders",
                    ImagePath = "borders.bmp",
                    SourceMode = TextGrabMode.GrabFrame,
                    WordBorderInfoJson = inlineWordBorderJson
                }
            ]);

        HistoryService historyService = new();
        HistoryInfo historyItem = Assert.Single(historyService.GetRecentGrabs());

        Assert.Null(historyItem.WordBorderInfoJson);
        Assert.Equal("image-with-borders.wordborders.json", historyItem.WordBorderInfoFileName);

        List<WordBorderInfo> wordBorderInfos = await historyService.GetWordBorderInfosAsync(historyItem);
        WordBorderInfo wordBorderInfo = Assert.Single(wordBorderInfos);
        Assert.Equal("hello", wordBorderInfo.Word);
        Assert.Equal(30d, wordBorderInfo.BorderRect.Width);
        Assert.Equal(40d, wordBorderInfo.BorderRect.Height);

        historyService.ReleaseLoadedHistories();

        string savedHistoryJson = await FileUtilities.GetTextFileAsync("HistoryWithImage.json", FileStorageKind.WithHistory);
        Assert.DoesNotContain("\"WordBorderInfoJson\"", savedHistoryJson);
        Assert.Contains("\"WordBorderInfoFileName\"", savedHistoryJson);

        string savedWordBorderJson = await FileUtilities.GetTextFileAsync(historyItem.WordBorderInfoFileName!, FileStorageKind.WithHistory);
        Assert.Contains("hello", savedWordBorderJson);
    }

    private static Task<bool> SaveHistoryFileAsync(string fileName, List<HistoryInfo> historyItems)
    {
        string historyJson = JsonSerializer.Serialize(historyItems, HistoryJsonOptions);
        return FileUtilities.SaveTextFile(historyJson, fileName, FileStorageKind.WithHistory);
    }
}

[CollectionDefinition("History service", DisableParallelization = true)]
public class HistoryServiceCollectionDefinition
{
}
