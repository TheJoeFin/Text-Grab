using System.Drawing;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Tests;

public class TrayFlyoutTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private static HistoryInfo Text(string text, int minutesAgo) => new()
    {
        ID = Guid.NewGuid().ToString(),
        TextContent = text,
        CaptureDateTime = Now.AddMinutes(-minutesAgo),
    };

    private static HistoryInfo File(string path, int minutesAgo) => new()
    {
        ID = Guid.NewGuid().ToString(),
        SourcePath = path,
        CaptureDateTime = Now.AddMinutes(-minutesAgo),
    };

    [Fact]
    public void Merge_SortsNewestFirstAcrossSources()
    {
        List<TrayFlyoutItem> items = TrayFlyoutItemProvider.Merge(
            [Text("edit-old", 30), Text("edit-new", 2)],
            [Text("grab", 10)],
            [File(@"C:\docs\report.txt", 5)],
            TrayFlyoutSources.All);

        Assert.Equal(["edit-new", "report.txt", "grab", "edit-old"], items.Select(i => i.Title));
    }

    [Fact]
    public void Merge_SkipsDisabledSources()
    {
        List<TrayFlyoutItem> items = TrayFlyoutItemProvider.Merge(
            [Text("edit", 1)],
            [Text("grab", 2)],
            [File(@"C:\docs\a.txt", 3)],
            TrayFlyoutSources.GrabFrames | TrayFlyoutSources.Files);

        Assert.DoesNotContain(items, i => i.Kind == TrayFlyoutItemKind.EditText);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Query_CapsToMaxCount()
    {
        List<TrayFlyoutItem> items = TrayFlyoutItemProvider.Merge(
            Enumerable.Range(0, 50).Select(i => Text($"item {i}", i)),
            [],
            [],
            TrayFlyoutSources.All);

        Assert.Equal(20, TrayFlyoutItemProvider.Query(items, null, 20).Count);
    }

    [Fact]
    public void Query_FiltersBeforeCapping_SoSearchReachesPastVisibleCount()
    {
        List<TrayFlyoutItem> items = TrayFlyoutItemProvider.Merge(
            Enumerable.Range(0, 40).Select(i => Text(i == 39 ? "needle in the oldest" : $"hay {i}", i)),
            [],
            [],
            TrayFlyoutSources.All);

        List<TrayFlyoutItem> result = TrayFlyoutItemProvider.Query(items, "needle", 5);

        Assert.Single(result);
        Assert.Equal("needle in the oldest", result[0].Title);
    }

    [Fact]
    public void Query_RequiresAllTermsAndIgnoresCase()
    {
        List<TrayFlyoutItem> items = TrayFlyoutItemProvider.Merge(
            [Text("Invoice from ACME", 1), Text("Invoice from Globex", 2)],
            [],
            [],
            TrayFlyoutSources.All);

        List<TrayFlyoutItem> result = TrayFlyoutItemProvider.Query(items, "invoice acme", 20);

        Assert.Single(result);
        Assert.Contains("ACME", result[0].Title);
    }

    [Fact]
    public void Query_MatchesFilePathNotJustName()
    {
        List<TrayFlyoutItem> items = TrayFlyoutItemProvider.Merge(
            [],
            [],
            [File(@"C:\taxes\2025\return.pdf", 1)],
            TrayFlyoutSources.All);

        Assert.Single(TrayFlyoutItemProvider.Query(items, "taxes", 20));
    }

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(20, 20)]
    [InlineData(500, 100)]
    public void ClampItemCount_StaysInRange(int input, int expected)
        => Assert.Equal(expected, TrayFlyoutItemProvider.ClampItemCount(input));

    [Fact]
    public void Sources_RoundTripThroughSettingsString()
    {
        TrayFlyoutSources original = TrayFlyoutSources.EditText | TrayFlyoutSources.Files;

        string stored = TrayFlyoutItemProvider.FormatSources(original);

        Assert.Equal("EditText,Files", stored);
        Assert.Equal(original, TrayFlyoutItemProvider.ParseSources(stored));
    }

    [Fact]
    public void ParseSources_EmptyOrMissingMeansAll_ButAllDisabledStaysNone()
    {
        Assert.Equal(TrayFlyoutSources.All, TrayFlyoutItemProvider.ParseSources(null));
        Assert.Equal(TrayFlyoutSources.All, TrayFlyoutItemProvider.ParseSources("  "));
        string none = TrayFlyoutItemProvider.FormatSources(TrayFlyoutSources.None);
        Assert.Equal("None", none);
        Assert.Equal(TrayFlyoutSources.None, TrayFlyoutItemProvider.ParseSources(none));
    }

    [Fact]
    public void BuildSnippet_CollapsesWhitespaceAndTruncates()
    {
        Assert.Equal("(empty)", TrayFlyoutItem.BuildSnippet("   "));
        Assert.Equal("a b c", TrayFlyoutItem.BuildSnippet("a\r\n\tb   c"));

        string longText = new('x', 200);
        string snippet = TrayFlyoutItem.BuildSnippet(longText);
        Assert.Equal(70, snippet.Length);
        Assert.EndsWith("…", snippet);
    }

    [Fact]
    public void ReopenGuard_NeverHidden_DoesNotSuppress()
        => Assert.False(TrayFlyoutController.ShouldSuppressReopen(nowTick: 1_000, lastHiddenTick: null));

    [Fact]
    public void ReopenGuard_SuppressesOnlyJustAfterHide()
    {
        Assert.True(TrayFlyoutController.ShouldSuppressReopen(nowTick: 10_100, lastHiddenTick: 10_000));
        Assert.False(TrayFlyoutController.ShouldSuppressReopen(nowTick: 12_000, lastHiddenTick: 10_000));
    }

    // ---- Placement (physical pixels) ----

    private static readonly Rectangle WorkArea = new(0, 0, 1920, 1040); // taskbar is the bottom 40px of a 1080p screen
    private static readonly Size FlyoutSize = new(360, 540);

    [Fact]
    public void Placement_TaskbarClick_SnapsToBottomRightCorner()
    {
        Point result = TrayFlyoutPlacement.Compute(new Point(1700, 1060), WorkArea, FlyoutSize, gap: 8);

        Assert.Equal(1920 - 360 - 8, result.X);
        Assert.Equal(1040 - 540 - 8, result.Y);
    }

    [Fact]
    public void Placement_TaskbarClickOnLeftHalf_SnapsToBottomLeftCorner()
    {
        Point result = TrayFlyoutPlacement.Compute(new Point(100, 1060), WorkArea, FlyoutSize, gap: 8);

        Assert.Equal(new Point(8, 1040 - 540 - 8), result);
    }

    [Fact]
    public void Placement_ClampsToWorkAreaRightEdge()
    {
        Point result = TrayFlyoutPlacement.Compute(new Point(1915, 1060), WorkArea, FlyoutSize, gap: 8);

        Assert.Equal(1920 - 360 - 8, result.X);
    }

    [Fact]
    public void Placement_TaskbarOnRight_SnapsFlushLeftOfTaskbar()
    {
        Rectangle workArea = new(0, 0, 1880, 1080);

        Point result = TrayFlyoutPlacement.Compute(new Point(1900, 500), workArea, FlyoutSize, gap: 8);

        Assert.Equal(1880 - 360 - 8, result.X);
        Assert.Equal(8, result.Y);
    }

    [Fact]
    public void Placement_CursorInsideWorkArea_CentersOnCursorAndStaysOnScreen()
    {
        Point centered = TrayFlyoutPlacement.Compute(new Point(960, 500), WorkArea, FlyoutSize, gap: 8, snapToCorner: false);
        Assert.Equal(new Point(780, 230), centered);

        Point corner = TrayFlyoutPlacement.Compute(new Point(2, 2), WorkArea, FlyoutSize, gap: 8, snapToCorner: false);
        Assert.Equal(new Point(8, 8), corner);
    }

    [Fact]
    public void Placement_WorkAreaSmallerThanFlyout_DoesNotThrow()
    {
        Rectangle tiny = new(0, 0, 200, 200);

        Point result = TrayFlyoutPlacement.Compute(new Point(100, 100), tiny, FlyoutSize, gap: 8);

        Assert.Equal(new Point(8, 8), result);
    }
}
