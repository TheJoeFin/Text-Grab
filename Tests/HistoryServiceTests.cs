using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Reflection;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Properties;
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

    [Fact]
    public void ImageHistory_SeparatesPdfDocumentsFromRecentGrabs()
    {
        HistoryInfo olderGrab = new()
        {
            ID = "grab",
            CaptureDateTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
            ImagePath = "grab.bmp",
            SourceContentKind = OpenContentKind.Image,
        };
        HistoryInfo newerPdf = new()
        {
            ID = "pdf",
            CaptureDateTime = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero),
            ImagePath = "pdf-page.bmp",
            SourceContentKind = OpenContentKind.PdfDocument,
            SourcePath = @"C:\documents\sample.pdf",
            SourcePageIndex = 4,
        };

        HistoryService historyService = new();
        SetPrivateField(historyService, "HistoryWithImage", new List<HistoryInfo> { olderGrab, newerPdf });
        SetPrivateField(historyService, "_imageHistoryLoaded", true);

        Assert.Same(olderGrab, Assert.Single(historyService.GetRecentGrabs()));
        Assert.Same(newerPdf, Assert.Single(historyService.GetRecentPdfDocuments()));
        Assert.Equal(4, newerPdf.SourcePageIndex);
        Assert.True(historyService.HasAnyRecentGrabs());
        Assert.Same(olderGrab, HistoryFileUtilities.GetMostRecentGrab([olderGrab, newerPdf]));
    }

    [WpfFact]
    public void RecentFiles_PersistsDeduplicatesAndIncludesLegacyPdfs()
    {
        string root = Path.Combine(Path.GetTempPath(), $"text-grab-recent-files-{Guid.NewGuid():N}");
        AutomationProfile profile = AutomationProfile.TryCreate(
            ["Text-Grab.exe"],
            name => name == AutomationProfile.ProfileEnvironmentVariable ? root : null)
            ?? throw new InvalidOperationException("Failed to create automation profile.");
        bool originalUseHistory = AppUtilities.TextGrabSettings.UseHistory;
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(profile);
        AppUtilities.TextGrabSettings.UseHistory = true;

        try
        {
            using HistoryService service = new();
            SetPrivateField(service, "HistoryWithImage", new List<HistoryInfo>
            {
                new()
                {
                    ID = "legacy-pdf",
                    SourcePath = @"C:\documents\legacy.pdf",
                    SourceContentKind = OpenContentKind.PdfDocument,
                    CaptureDateTime = DateTimeOffset.UtcNow.AddDays(-1),
                }
            });
            SetPrivateField(service, "_imageHistoryLoaded", true);

            service.RecordRecentFile(@"C:\documents\notes.txt", OpenContentKind.TextFile);
            service.RecordRecentFile(@"C:\documents\readme.md", OpenContentKind.TextFile);
            service.RecordRecentFile(@"C:\DOCUMENTS\NOTES.TXT", OpenContentKind.TextFile);
            SetPrivateField(service, "_hasPendingWrite", true);
            service.WriteHistory();

            List<HistoryInfo> recent = service.GetRecentFiles();
            Assert.Equal(3, recent.Count);
            Assert.Equal(@"C:\DOCUMENTS\NOTES.TXT", recent[0].SourcePath);
            Assert.Equal(@"C:\documents\readme.md", recent[1].SourcePath);
            Assert.Equal("legacy-pdf", recent[2].ID);

            service.ReleaseLoadedHistories();
            List<HistoryInfo> reloaded = service.GetRecentFiles();
            Assert.Equal(3, reloaded.Count);
            Assert.Equal(@"C:\DOCUMENTS\NOTES.TXT", reloaded[0].SourcePath);
            Assert.Equal(@"C:\documents\readme.md", reloaded[1].SourcePath);
            Assert.Equal("legacy-pdf", reloaded[2].ID);
        }
        finally
        {
            AppUtilities.TextGrabSettings.UseHistory = originalUseHistory;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [WpfFact]
    public void RecentFiles_RespectsHistorySettingAndRetainsTenMostRecent()
    {
        string root = Path.Combine(Path.GetTempPath(), $"text-grab-recent-files-{Guid.NewGuid():N}");
        AutomationProfile profile = AutomationProfile.TryCreate(
            ["Text-Grab.exe"],
            name => name == AutomationProfile.ProfileEnvironmentVariable ? root : null)
            ?? throw new InvalidOperationException("Failed to create automation profile.");
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(profile);
        bool originalUseHistory = AppUtilities.TextGrabSettings.UseHistory;
        using HistoryService service = new();
        SetPrivateField(service, "HistoryWithImage", new List<HistoryInfo>());
        SetPrivateField(service, "_imageHistoryLoaded", true);

        try
        {
            AppUtilities.TextGrabSettings.UseHistory = false;
            service.RecordRecentFile(@"C:\documents\ignored.txt", OpenContentKind.TextFile);
            Assert.Empty(service.GetRecentFiles());

            AppUtilities.TextGrabSettings.UseHistory = true;
            for (int i = 0; i < 12; i++)
                service.RecordRecentFile($@"C:\documents\file{i}.txt", OpenContentKind.TextFile);

            List<HistoryInfo> recent = service.GetRecentFiles();
            Assert.Equal(10, recent.Count);
            Assert.Equal(@"C:\documents\file11.txt", recent[0].SourcePath);
            Assert.DoesNotContain(recent, item => item.SourcePath.EndsWith("file0.txt", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(recent, item => item.SourcePath.EndsWith("file1.txt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            AppUtilities.TextGrabSettings.UseHistory = originalUseHistory;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [WpfFact]
    public void RecentFiles_UpdatesAcrossHistoryServiceInstances()
    {
        string root = Path.Combine(Path.GetTempPath(), $"text-grab-recent-files-{Guid.NewGuid():N}");
        AutomationProfile profile = AutomationProfile.TryCreate(
            ["Text-Grab.exe"],
            name => name == AutomationProfile.ProfileEnvironmentVariable ? root : null)
            ?? throw new InvalidOperationException("Failed to create automation profile.");
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(profile);
        bool originalUseHistory = AppUtilities.TextGrabSettings.UseHistory;
        AppUtilities.TextGrabSettings.UseHistory = true;
        try
        {
            using HistoryService first = new();
            using HistoryService second = new();
            Assert.Empty(first.GetRecentFiles());
            second.RecordRecentFile(@"C:\documents\opened.pdf", OpenContentKind.PdfDocument);
            Assert.Equal(@"C:\documents\opened.pdf", Assert.Single(first.GetRecentFiles()).SourcePath);
            first.RecordRecentFile(@"C:\documents\notes.md", OpenContentKind.TextFile);
            Assert.Equal(2, second.GetRecentFiles().Count);
        }
        finally
        {
            AppUtilities.TextGrabSettings.UseHistory = originalUseHistory;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetMostRecentGrab_ReturnsNull_WhenHistoryOnlyContainsPdfs()
    {
        HistoryInfo pdf = new()
        {
            CaptureDateTime = DateTimeOffset.UtcNow,
            SourceContentKind = OpenContentKind.PdfDocument,
        };

        Assert.Null(HistoryFileUtilities.GetMostRecentGrab([pdf]));
    }

    [Fact]
    public void VisualHistoryRetention_LimitsGrabsAndPdfsIndependently()
    {
        List<HistoryInfo> historyItems = [];
        for (int index = 0; index < 12; index++)
        {
            historyItems.Add(new HistoryInfo
            {
                ID = $"grab-{index}",
                CaptureDateTime = new DateTimeOffset(2024, 1, 1, 0, index, 0, TimeSpan.Zero),
                SourceContentKind = OpenContentKind.Image,
            });
            historyItems.Add(new HistoryInfo
            {
                ID = $"pdf-{index}",
                CaptureDateTime = new DateTimeOffset(2024, 1, 2, 0, index, 0, TimeSpan.Zero),
                SourceContentKind = OpenContentKind.PdfDocument,
            });
        }

        List<HistoryInfo> itemsToRemove = HistoryFileUtilities.GetExcessVisualHistoryItems(historyItems);

        Assert.Equal(4, itemsToRemove.Count);
        Assert.Contains(itemsToRemove, history => history.ID == "grab-0");
        Assert.Contains(itemsToRemove, history => history.ID == "grab-1");
        Assert.Contains(itemsToRemove, history => history.ID == "pdf-0");
        Assert.Contains(itemsToRemove, history => history.ID == "pdf-1");
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImageHistory_WritesWordBorderSidecar_AndKeepsInlineJsonOnlyInClassicMode(bool fileBackedSettings)
    {
        const string historyFileName = "HistoryWithImage.json";
        const string sidecarFileName = "image-with-borders.wordborders.json";
        string inlineWordBorderJson = JsonSerializer.Serialize(
            new List<WordBorderInfo>
            {
                new()
                {
                    Word = "hello",
                    DisplayText = $"hello{Environment.NewLine}world",
                    BorderRect = new RectangleF(1, 2, 30, 40),
                    DisplayLineHeight = 18,
                    KeepSingleLineOutput = true,
                    LineNumber = 1,
                    ResultColumnID = 2,
                    ResultRowID = 3
                }
            },
            HistoryJsonOptions);

        string root = Path.Combine(Path.GetTempPath(), $"text-grab-history-borders-{Guid.NewGuid():N}");
        AutomationProfile profile = AutomationProfile.TryCreate(
            ["Text-Grab.exe"],
            name => name == AutomationProfile.ProfileEnvironmentVariable ? root : null)
            ?? throw new InvalidOperationException("Failed to create automation profile.");
        Settings settings = new() { EnableFileBackedManagedSettings = fileBackedSettings };
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(profile);
        try
        {
            SettingsAccess.SetResolver(() => settings);

            Assert.True(await SaveHistoryFileAsync(
                historyFileName,
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
                ]));

            using HistoryService historyService = new();
            HistoryInfo historyItem = Assert.Single(historyService.GetRecentGrabs());
            Assert.Equal(fileBackedSettings ? null : inlineWordBorderJson, historyItem.WordBorderInfoJson);
            Assert.Equal(sidecarFileName, historyItem.WordBorderInfoFileName);

            historyService.ReleaseLoadedHistories();

            string savedHistoryJson = await FileUtilities.GetTextFileAsync(historyFileName, FileStorageKind.WithHistory);
            using JsonDocument savedHistory = JsonDocument.Parse(savedHistoryJson);
            JsonElement savedItem = Assert.Single(savedHistory.RootElement.EnumerateArray());
            Assert.Equal(sidecarFileName, savedItem.GetProperty(nameof(HistoryInfo.WordBorderInfoFileName)).GetString());
            bool hasInlineJson = savedItem.TryGetProperty(nameof(HistoryInfo.WordBorderInfoJson), out JsonElement savedInlineJson);
            Assert.Equal(!fileBackedSettings, hasInlineJson);
            if (!fileBackedSettings)
                Assert.Equal(inlineWordBorderJson, savedInlineJson.GetString());

            string savedWordBorderJson = await FileUtilities.GetTextFileAsync(sidecarFileName, FileStorageKind.WithHistory);
            Assert.Equal(inlineWordBorderJson, savedWordBorderJson);

            HistoryInfo reloadedItem = Assert.Single(historyService.GetRecentGrabs());
            Assert.Equal([44d], reloadedItem.ManualTableColumnSeparators);
            Assert.Equal([18d], reloadedItem.ManualTableRowSeparators);

            WordBorderInfo wordBorderInfo = Assert.Single(await historyService.GetWordBorderInfosAsync(reloadedItem));
            Assert.Equal("hello", wordBorderInfo.Word);
            Assert.Equal($"hello{Environment.NewLine}world", wordBorderInfo.DisplayText);
            Assert.Equal(30d, wordBorderInfo.BorderRect.Width);
            Assert.Equal(40d, wordBorderInfo.BorderRect.Height);
            Assert.Equal(18d, wordBorderInfo.DisplayLineHeight);
            Assert.True(wordBorderInfo.KeepSingleLineOutput);
        }
        finally
        {
            SettingsAccess.SetResolver(static () => AppUtilities.TextGrabSettings);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
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
    public void TextHistory_UpdatesOpenedEditWindowInsteadOfAddingANewItem()
    {
        string root = Path.Combine(Path.GetTempPath(), $"text-grab-history-update-{Guid.NewGuid():N}");
        AutomationProfile profile = AutomationProfile.TryCreate(
            ["Text-Grab.exe"],
            name => name == AutomationProfile.ProfileEnvironmentVariable ? root : null)
            ?? throw new InvalidOperationException("Failed to create automation profile.");
        bool originalUseHistory = AppUtilities.TextGrabSettings.UseHistory;
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(profile);
        AppUtilities.TextGrabSettings.UseHistory = true;

        try
        {
            HistoryInfo original = new()
            {
                ID = "edit-history-item",
                CaptureDateTime = new DateTimeOffset(2024, 1, 5, 12, 0, 0, TimeSpan.Zero),
                TextContent = "original text",
                SourceMode = TextGrabMode.EditText,
            };
            using HistoryService historyService = new();
            SetPrivateField(historyService, "HistoryTextOnly", new List<HistoryInfo> { original });
            SetPrivateField(historyService, "_textHistoryLoaded", true);

            EditTextWindow reopenedWindow = new(original);
            reopenedWindow.Session.Text = "edited text";
            historyService.SaveToHistory(reopenedWindow);

            HistoryInfo updated = Assert.Single(historyService.GetEditWindows());
            Assert.Equal(original.ID, updated.ID);
            Assert.Equal("edited text", updated.TextContent);
        }
        finally
        {
            AppUtilities.TextGrabSettings.UseHistory = originalUseHistory;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
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
