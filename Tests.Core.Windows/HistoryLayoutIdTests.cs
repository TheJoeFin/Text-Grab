using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// The layout a history item was saved with (v5 E4a, <see cref="HistoryInfo.LayoutId"/>): an optional
/// string that older history files do not have and older builds ignore. Written to a temporary
/// automation profile's history folder, never the real one.
/// </summary>
[Collection(HistoryFilesCollectionDefinition.Name)]
public sealed class HistoryLayoutIdTests : IDisposable
{
    private const string HistoryName = "HistoryTextOnly";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tg-history-layout-{Guid.NewGuid():N}");
    private readonly IDisposable _profileScope;

    public HistoryLayoutIdTests()
    {
        AutomationProfile profile = AutomationProfile.TryCreate(
            ["Text-Grab.exe"],
            name => name == AutomationProfile.ProfileEnvironmentVariable ? _root : null)
            ?? throw new InvalidOperationException("Failed to create automation profile.");
        _profileScope = AutomationProfile.OverrideCurrentForTests(profile);
    }

    private string HistoryFile => Path.Combine(_root, "history", $"{HistoryName}.json");

    public void Dispose()
    {
        _profileScope.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task AnOlderHistoryFile_LoadsWithNoLayout()
    {
        // As 4.17.0-beta1 wrote it: no LayoutId member at all.
        Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile)!);
        await File.WriteAllTextAsync(HistoryFile, """
            [
              {
                "CaptureDateTime": "2026-11-01T10:00:00+00:00",
                "ID": "older",
                "SourceMode": "EditText",
                "TextContent": "1 + 1",
                "HasCalcPaneOpen": true,
                "CalcPaneWidth": 260,
                "EditorMode": "Text"
              }
            ]
            """, TestContext.Current.CancellationToken);

        (List<HistoryInfo> loaded, bool needsRewrite) = await HistoryFileUtilities.LoadHistoryAsync(HistoryName);

        HistoryInfo item = Assert.Single(loaded);
        Assert.Null(item.LayoutId);
        Assert.Equal(("older", true, 260), (item.ID, item.HasCalcPaneOpen, item.CalcPaneWidth));
        Assert.False(needsRewrite);
    }

    [Fact]
    public async Task AnItemWithNoLayout_IsWrittenWithoutTheMember()
    {
        HistoryFileUtilities.WriteHistoryFiles([new HistoryInfo { ID = "classic", TextContent = "x", SourceMode = TextGrabMode.EditText }], HistoryName, HistoryFileUtilities.MaxHistoryTextOnly);

        string json = await File.ReadAllTextAsync(HistoryFile, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("LayoutId", json);
    }

    [Fact]
    public async Task AnItemsLayout_IsWrittenAsAStringAndReadBack()
    {
        HistoryFileUtilities.WriteHistoryFiles([new HistoryInfo { ID = "laid-out", TextContent = "x", SourceMode = TextGrabMode.EditText, LayoutId = "wide-calc" }], HistoryName, HistoryFileUtilities.MaxHistoryTextOnly);

        string json = await File.ReadAllTextAsync(HistoryFile, TestContext.Current.CancellationToken);
        (List<HistoryInfo> loaded, _) = await HistoryFileUtilities.LoadHistoryAsync(HistoryName);

        Assert.Contains("\"LayoutId\": \"wide-calc\"", json);
        Assert.Equal("wide-calc", Assert.Single(loaded).LayoutId);
    }

    [Fact]
    public async Task ALayoutThisBuildDoesNotKnow_StillLoadsTheItem()
    {
        // A history file from a later build, naming a layout this one has never heard of.
        Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile)!);
        await File.WriteAllTextAsync(HistoryFile, """
            [ { "ID": "later", "SourceMode": "EditText", "TextContent": "x", "EditorMode": "Text", "LayoutId": "future-layout" } ]
            """, TestContext.Current.CancellationToken);

        (List<HistoryInfo> loaded, _) = await HistoryFileUtilities.LoadHistoryAsync(HistoryName);

        Assert.Equal("future-layout", Assert.Single(loaded).LayoutId);
    }
}
