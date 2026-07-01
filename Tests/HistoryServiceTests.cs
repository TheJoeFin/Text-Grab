using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Reflection;
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
    public async Task ImageHistory_KeepsInlineWordBorderJsonWhileMirroringSidecarStorage()
    {
        string inlineWordBorderJson = JsonSerializer.Serialize(
            new List<WordBorderInfo>
            {
                new()
                {
                    Word = "hello",
                    DisplayText = $"hello{Environment.NewLine}world",
                    BorderRect = new Rect(1, 2, 30, 40),
                    DisplayLineHeight = 18,
                    KeepSingleLineOutput = true,
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
                    WordBorderInfoJson = inlineWordBorderJson,
                    ManualTableColumnSeparators = [44],
                    ManualTableRowSeparators = [18]
                }
            ]);

        HistoryService historyService = new();
        HistoryInfo historyItem = Assert.Single(historyService.GetRecentGrabs());

        Assert.Equal(inlineWordBorderJson, historyItem.WordBorderInfoJson);
        Assert.Equal("image-with-borders.wordborders.json", historyItem.WordBorderInfoFileName);
        Assert.Equal([44d], historyItem.ManualTableColumnSeparators);
        Assert.Equal([18d], historyItem.ManualTableRowSeparators);

        List<WordBorderInfo> wordBorderInfos = await historyService.GetWordBorderInfosAsync(historyItem);
        WordBorderInfo wordBorderInfo = Assert.Single(wordBorderInfos);
        Assert.Equal("hello", wordBorderInfo.Word);
        Assert.Equal($"hello{Environment.NewLine}world", wordBorderInfo.DisplayText);
        Assert.Equal(30d, wordBorderInfo.BorderRect.Width);
        Assert.Equal(40d, wordBorderInfo.BorderRect.Height);
        Assert.Equal(18d, wordBorderInfo.DisplayLineHeight);
        Assert.True(wordBorderInfo.KeepSingleLineOutput);

        historyService.ReleaseLoadedHistories();

        string savedHistoryJson = await FileUtilities.GetTextFileAsync("HistoryWithImage.json", FileStorageKind.WithHistory);
        Assert.Contains("\"WordBorderInfoJson\"", savedHistoryJson);
        Assert.Contains("\"WordBorderInfoFileName\"", savedHistoryJson);
        Assert.Contains("\"ManualTableColumnSeparators\"", savedHistoryJson);
        Assert.Contains("\"ManualTableRowSeparators\"", savedHistoryJson);

        string savedWordBorderJson = await FileUtilities.GetTextFileAsync(historyItem.WordBorderInfoFileName!, FileStorageKind.WithHistory);
        Assert.Contains("hello", savedWordBorderJson);
    }

    [WpfFact]
    public async Task ImageHistory_NormalizesPreviewUiAutomationEntriesToRollbackSafeValues()
    {
        await SaveHistoryFileAsync(
            "HistoryWithImage.json",
            [
                new HistoryInfo
                {
                    ID = "uia-preview",
                    CaptureDateTime = new DateTimeOffset(2024, 1, 4, 12, 0, 0, TimeSpan.Zero),
                    TextContent = "direct text history",
                    ImagePath = "uia.bmp",
                    SourceMode = TextGrabMode.Fullscreen,
                    LanguageTag = UiAutomationLang.Tag,
                    LanguageKind = LanguageKind.UiAutomation,
                }
            ]);

        HistoryService historyService = new();
        HistoryInfo historyItem = Assert.Single(historyService.GetRecentGrabs());

        Assert.True(historyItem.UsedUiAutomation);
        Assert.Equal(LanguageKind.Global, historyItem.LanguageKind);
        Assert.NotEqual(UiAutomationLang.Tag, historyItem.LanguageTag);
        Assert.IsNotType<UiAutomationLang>(historyItem.OcrLanguage);

        historyService.WriteHistory();
        historyService.ReleaseLoadedHistories();

        string savedHistoryJson = await FileUtilities.GetTextFileAsync("HistoryWithImage.json", FileStorageKind.WithHistory);
        Assert.DoesNotContain("\"LanguageKind\": \"UiAutomation\"", savedHistoryJson);
        Assert.DoesNotContain($"\"LanguageTag\": \"{UiAutomationLang.Tag}\"", savedHistoryJson);
        Assert.Contains("\"UsedUiAutomation\": true", savedHistoryJson);
    }

    [WpfFact]
    public async Task TextHistory_PreservesMarkdownEditorModeAndSource()
    {
        await SaveHistoryFileAsync(
            "HistoryTextOnly.json",
            [
                new HistoryInfo
                {
                    ID = "markdown-history",
                    CaptureDateTime = new DateTimeOffset(2024, 1, 5, 12, 0, 0, TimeSpan.Zero),
                    TextContent = "# Heading\r\n\r\n**bold**",
                    SourceMode = TextGrabMode.EditText,
                    EditorMode = EtwEditorMode.Markdown
                }
            ]);

        HistoryService historyService = new();
        HistoryInfo historyItem = Assert.Single(historyService.GetEditWindows());

        Assert.Equal(EtwEditorMode.Markdown, historyItem.EditorMode);
        Assert.Equal("# Heading\r\n\r\n**bold**", historyItem.TextContent);
    }

    [WpfFact]
    public async Task TextHistory_LoadsUnknownLanguageKindsUsingGlobalFallback()
    {
        const string rawHistoryJson =
            """
            [
              {
                "ID": "legacy-language-kind",
                "CaptureDateTime": "2024-01-06T12:00:00+00:00",
                "TextContent": "legacy text history",
                "SourceMode": "EditText",
                "LanguageTag": "en-US",
                "LanguageKind": "LegacyPreview"
              }
            ]
            """;

        await FileUtilities.SaveTextFile(rawHistoryJson, "HistoryTextOnly.json", FileStorageKind.WithHistory);

        HistoryService historyService = new();
        HistoryInfo historyItem = Assert.Single(historyService.GetEditWindows());

        Assert.Equal("legacy text history", historyItem.TextContent);
        Assert.Equal(LanguageKind.Global, historyItem.LanguageKind);

        historyService.WriteHistory();
        historyService.ReleaseLoadedHistories();

        string savedHistoryJson = await FileUtilities.GetTextFileAsync("HistoryTextOnly.json", FileStorageKind.WithHistory);
        Assert.DoesNotContain("LegacyPreview", savedHistoryJson);
        Assert.Contains("\"LanguageKind\": \"Global\"", savedHistoryJson);
    }

    [WpfFact]
    public async Task TextHistory_RecoversValidEntriesWhenOneEntryIsMalformed()
    {
        const string rawHistoryJson =
            """
            [
              {
                "ID": "valid-entry",
                "CaptureDateTime": "2024-01-07T12:00:00+00:00",
                "TextContent": "valid text history",
                "SourceMode": "EditText"
              },
              {
                "ID": "bad-entry",
                "CaptureDateTime": "2024-01-08T12:00:00+00:00",
                "TextContent": "bad text history",
                "SourceMode": { "unexpected": true }
              }
            ]
            """;

        await FileUtilities.SaveTextFile(rawHistoryJson, "HistoryTextOnly.json", FileStorageKind.WithHistory);

        HistoryService historyService = new();
        HistoryInfo historyItem = Assert.Single(historyService.GetEditWindows());

        Assert.Equal("valid-entry", historyItem.ID);
        Assert.Equal("valid text history", historyItem.TextContent);
    }

    [WpfFact]
    public void TextHistory_WriteHistory_PersistsSavedEditWindowText()
    {
        bool originalUseHistory = AppUtilities.TextGrabSettings.UseHistory;
        AppUtilities.TextGrabSettings.UseHistory = true;

        try
        {
            HistoryService historyService = new();
            historyService.DeleteHistory();
            SetPrivateField(historyService, "HistoryTextOnly", new List<HistoryInfo>
            {
                new()
                {
                    ID = "saved-edit-window",
                    CaptureDateTime = new DateTimeOffset(2024, 1, 6, 12, 0, 0, TimeSpan.Zero),
                    TextContent = "history text from close action",
                    SourceMode = TextGrabMode.EditText
                }
            });
            SetPrivateField(historyService, "_textHistoryLoaded", true);
            SetPrivateField(historyService, "_hasPendingWrite", true);

            historyService.WriteHistory();
            historyService.ReleaseLoadedHistories();

            HistoryInfo historyItem = Assert.Single(historyService.GetEditWindows());
            Assert.Equal("history text from close action", historyItem.TextContent);
        }
        finally
        {
            AppUtilities.TextGrabSettings.UseHistory = originalUseHistory;
        }
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        fieldInfo.SetValue(target, value);
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
