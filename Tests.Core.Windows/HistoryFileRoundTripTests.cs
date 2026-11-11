using System.Drawing;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// The automation-profile override that redirects the history folder is process-global, so these
/// never run alongside other tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class HistoryFilesCollectionDefinition
{
    public const string Name = "History files";
}

/// <summary>
/// Characterization of the on-disk history pipeline, <see cref="HistoryFileUtilities"/> (v5 F6,
/// step 1): what survives a write and reload, what each retention cap drops, and what the loader
/// does with damaged files. Pinned before anything is built on top of it. Every test writes under
/// its own temporary automation-profile root, never the real history folder.
/// </summary>
[Collection(HistoryFilesCollectionDefinition.Name)]
public sealed class HistoryFileRoundTripTests : IDisposable
{
    private const string TextHistory = "HistoryTextOnly";
    private const string ImageHistory = "HistoryWithImage";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tg-history-files-{Guid.NewGuid():N}");
    private readonly IDisposable _profileScope;

    public HistoryFileRoundTripTests()
    {
        AutomationProfile profile = AutomationProfile.TryCreate(
            ["Text-Grab.exe"],
            name => name == AutomationProfile.ProfileEnvironmentVariable ? _root : null)
            ?? throw new InvalidOperationException("Failed to create automation profile.");
        _profileScope = AutomationProfile.OverrideCurrentForTests(profile);
    }

    private string HistoryFolder => Path.Combine(_root, "history");

    public void Dispose()
    {
        _profileScope.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task WriteThenLoad_RoundTripsEveryPersistedField()
    {
        List<HistoryInfo> written =
        [
            Item("text-1", day: 1, mode: TextGrabMode.EditText, text: "plain note"),
            Item("text-2", day: 2, mode: TextGrabMode.EditText, text: "# Markdown\r\n\r\n**bold**", editor: EtwEditorMode.Markdown),
            Item("text-3", day: 3, mode: TextGrabMode.Fullscreen, text: "a\tb\r\nc\td", isTable: true, rect: new RectangleF(10.5f, 20, 300, 40.25f)),
        ];

        HistoryFileUtilities.WriteHistoryFiles(written, TextHistory, HistoryFileUtilities.MaxHistoryTextOnly);
        (List<HistoryInfo> loaded, bool needsRewrite) = await HistoryFileUtilities.LoadHistoryAsync(TextHistory);

        Assert.False(needsRewrite);
        Assert.Equal(written.Select(Snapshot.Of), loaded.Select(Snapshot.Of));
    }

    [Fact]
    public async Task Write_StoresItemsOldestFirst()
    {
        List<HistoryInfo> written = [Item("c", day: 3), Item("a", day: 1), Item("b", day: 2)];

        HistoryFileUtilities.WriteHistoryFiles(written, TextHistory, HistoryFileUtilities.MaxHistoryTextOnly);
        (List<HistoryInfo> loaded, _) = await HistoryFileUtilities.LoadHistoryAsync(TextHistory);

        Assert.Equal(["a", "b", "c"], loaded.Select(h => h.ID));
    }

    [Fact]
    public async Task TextCap_KeepsTheNewest100()
    {
        List<HistoryInfo> written = [.. Enumerable.Range(0, HistoryFileUtilities.MaxHistoryTextOnly + 5).Select(i => Item($"t{i}", minute: i))];

        HistoryFileUtilities.WriteHistoryFiles(written, TextHistory, HistoryFileUtilities.MaxHistoryTextOnly);
        (List<HistoryInfo> loaded, _) = await HistoryFileUtilities.LoadHistoryAsync(TextHistory);

        Assert.Equal(HistoryFileUtilities.MaxHistoryTextOnly, loaded.Count);
        Assert.Equal("t5", loaded[0].ID);
        Assert.Equal($"t{HistoryFileUtilities.MaxHistoryTextOnly + 4}", loaded[^1].ID);
    }

    [Fact]
    public async Task ImageFileCap_IsTheSumOfTheGrabAndPdfCaps()
    {
        int cap = HistoryFileUtilities.MaxHistoryWithImages + HistoryFileUtilities.MaxHistoryPdfDocuments;
        List<HistoryInfo> written = [.. Enumerable.Range(0, cap + 3).Select(i => Item($"i{i}", minute: i, imagePath: $"i{i}.bmp"))];

        HistoryFileUtilities.WriteHistoryFiles(written, ImageHistory, cap);
        (List<HistoryInfo> loaded, _) = await HistoryFileUtilities.LoadHistoryAsync(ImageHistory);

        Assert.Equal(cap, loaded.Count);
        Assert.Equal("i3", loaded[0].ID);
    }

    [Fact]
    public void VisualRetention_DropsOnlyTheOldestBeyondEachCap()
    {
        List<HistoryInfo> history =
        [
            .. Enumerable.Range(0, HistoryFileUtilities.MaxHistoryWithImages + 1).Select(i => Item($"grab{i}", minute: i)),
            .. Enumerable.Range(0, 3).Select(i => Item($"pdf{i}", minute: i, kind: OpenContentKind.PdfDocument)),
        ];

        List<HistoryInfo> excess = HistoryFileUtilities.GetExcessVisualHistoryItems(history);

        Assert.Equal("grab0", Assert.Single(excess).ID);
    }

    [Fact]
    public async Task MissingFile_LoadsEmpty_WithoutARewrite()
    {
        (List<HistoryInfo> loaded, bool needsRewrite) = await HistoryFileUtilities.LoadHistoryAsync(TextHistory);

        Assert.Empty(loaded);
        Assert.False(needsRewrite);
    }

    [Fact]
    public async Task EmptyArray_LoadsEmpty_WithoutARewrite()
    {
        await WriteRawAsync(TextHistory, "[]");

        (List<HistoryInfo> loaded, bool needsRewrite) = await HistoryFileUtilities.LoadHistoryAsync(TextHistory);

        Assert.Empty(loaded);
        Assert.False(needsRewrite);
    }

    [Fact]
    public async Task OneMalformedEntry_IsSkipped_TheRestRecovered_AndARewriteRequested()
    {
        await WriteRawAsync(TextHistory,
            """
            [
              { "ID": "good-1", "CaptureDateTime": "2024-01-01T00:00:00+00:00", "TextContent": "kept", "SourceMode": "EditText" },
              { "ID": "bad", "CaptureDateTime": "2024-01-02T00:00:00+00:00", "SourceMode": { "unexpected": true } },
              { "ID": "good-2", "CaptureDateTime": "2024-01-03T00:00:00+00:00", "TextContent": "also kept", "SourceMode": "EditText" }
            ]
            """);

        (List<HistoryInfo> loaded, bool needsRewrite) = await HistoryFileUtilities.LoadHistoryAsync(TextHistory);

        Assert.Equal(["good-1", "good-2"], loaded.Select(h => h.ID));
        Assert.True(needsRewrite);
    }

    /// <summary>
    /// Pins existing behavior that matters for user data: a file that is not JSON at all loads as
    /// empty and asks for a rewrite, so the next write replaces the unreadable file.
    /// </summary>
    [Theory]
    [InlineData("this is not json")]
    [InlineData("""{ "ID": "an object, not an array" }""")]
    [InlineData("[ { \"ID\": \"truncated\", ")]
    public async Task UnreadableFile_LoadsEmpty_AndRequestsARewrite(string rawText)
    {
        await WriteRawAsync(TextHistory, rawText);

        (List<HistoryInfo> loaded, bool needsRewrite) = await HistoryFileUtilities.LoadHistoryAsync(TextHistory);

        Assert.Empty(loaded);
        Assert.True(needsRewrite);
    }

    [Fact]
    public async Task UnknownLanguageKind_FallsBackToGlobal_AndRequestsARewrite()
    {
        await WriteRawAsync(TextHistory,
            """
            [ { "ID": "legacy", "CaptureDateTime": "2024-01-01T00:00:00+00:00", "SourceMode": "EditText", "LanguageTag": "en-US", "LanguageKind": "LegacyPreview" } ]
            """);

        (List<HistoryInfo> loaded, bool needsRewrite) = await HistoryFileUtilities.LoadHistoryAsync(TextHistory);

        Assert.Equal(LanguageKind.Global, Assert.Single(loaded).LanguageKind);
        Assert.True(needsRewrite);
    }

    [Fact]
    public void NormalizeHistoryIds_ReplacesBlankAndDuplicateIdsOnly()
    {
        List<HistoryInfo> history = [Item("keep", day: 1), Item("", day: 2), Item("keep", day: 3), Item("other", day: 4)];

        bool changed = HistoryFileUtilities.NormalizeHistoryIds(history);

        Assert.True(changed);
        Assert.Equal("keep", history[0].ID);
        Assert.Equal("other", history[3].ID);
        Assert.Equal(4, history.Select(h => h.ID).Distinct().Count());
        Assert.All(history, h => Assert.False(string.IsNullOrWhiteSpace(h.ID)));
        Assert.False(HistoryFileUtilities.NormalizeHistoryIds(history));
    }

    [Fact]
    public async Task WordBorderSidecar_IsWrittenBesideTheHistory_AndReadBack()
    {
        HistoryInfo item = Item("with-borders", day: 1);
        item.WordBorderInfoJson = """[ { "Word": "hello", "LineNumber": 1 } ]""";

        bool persisted = HistoryFileUtilities.PersistWordBorderData(item);
        List<WordBorderInfo> borders = await HistoryFileUtilities.GetWordBorderInfosAsync(item);

        Assert.True(persisted);
        Assert.Equal("with-borders.wordborders.json", item.WordBorderInfoFileName);
        Assert.True(File.Exists(Path.Combine(HistoryFolder, "with-borders.wordborders.json")));
        Assert.Equal("hello", Assert.Single(borders).Word);
        // File-backed managed settings are off in the shipped defaults, so the inline copy stays.
        Assert.NotNull(item.WordBorderInfoJson);
    }

    [Fact]
    public void DeleteUnusedWordBorderFiles_RemovesOnlyOrphans()
    {
        HistoryInfo kept = Item("kept", day: 1);
        kept.WordBorderInfoJson = "[]";
        HistoryInfo orphan = Item("orphan", day: 2);
        orphan.WordBorderInfoJson = "[]";
        HistoryFileUtilities.PersistWordBorderData(kept);
        HistoryFileUtilities.PersistWordBorderData(orphan);

        HistoryFileUtilities.DeleteUnusedWordBorderFiles([kept]);

        Assert.True(File.Exists(Path.Combine(HistoryFolder, "kept.wordborders.json")));
        Assert.False(File.Exists(Path.Combine(HistoryFolder, "orphan.wordborders.json")));
    }

    [Fact]
    public void DeleteHistoryArtifacts_RemovesTheImageAndSidecar()
    {
        HistoryInfo item = Item("doomed", day: 1, imagePath: "doomed.bmp");
        item.WordBorderInfoJson = "[]";
        HistoryFileUtilities.PersistWordBorderData(item);
        File.WriteAllBytes(Path.Combine(HistoryFolder, "doomed.bmp"), [1, 2, 3]);

        HistoryFileUtilities.DeleteHistoryArtifacts(item);

        Assert.False(File.Exists(Path.Combine(HistoryFolder, "doomed.bmp")));
        Assert.False(File.Exists(Path.Combine(HistoryFolder, "doomed.wordborders.json")));
    }

    private Task WriteRawAsync(string historyName, string rawText)
    {
        Directory.CreateDirectory(HistoryFolder);
        return File.WriteAllTextAsync(Path.Combine(HistoryFolder, $"{historyName}.json"), rawText);
    }

    private static HistoryInfo Item(
        string id,
        int day = 1,
        int minute = 0,
        TextGrabMode mode = TextGrabMode.EditText,
        string text = "text",
        EtwEditorMode editor = EtwEditorMode.Text,
        bool isTable = false,
        RectangleF? rect = null,
        string imagePath = "",
        OpenContentKind kind = OpenContentKind.Image)
    {
        HistoryInfo item = new()
        {
            ID = id,
            CaptureDateTime = new DateTimeOffset(2024, 1, day, 0, 0, 0, TimeSpan.Zero).AddMinutes(minute),
            SourceMode = mode,
            TextContent = text,
            EditorMode = editor,
            IsTable = isTable,
            ImagePath = imagePath,
            SourceContentKind = kind,
            LanguageTag = "en-US",
        };

        if (rect is RectangleF value)
            item.PositionRect = value;

        return item;
    }

    /// <summary>The persisted fields, as one comparable value.</summary>
    private sealed record Snapshot(
        string ID, DateTimeOffset CaptureDateTime, TextGrabMode SourceMode, string TextContent, EtwEditorMode EditorMode,
        bool IsTable, string RectAsString, string ImagePath, OpenContentKind SourceContentKind, string SourcePath,
        int SourcePageIndex, string LanguageTag, LanguageKind LanguageKind, double DpiScaleFactor, FsgSelectionStyle SelectionStyle)
    {
        public static Snapshot Of(HistoryInfo h) => new(
            h.ID, h.CaptureDateTime, h.SourceMode, h.TextContent, h.EditorMode, h.IsTable, h.RectAsString, h.ImagePath,
            h.SourceContentKind, h.SourcePath, h.SourcePageIndex, h.LanguageTag, h.LanguageKind, h.DpiScaleFactor, h.SelectionStyle);
    }
}
