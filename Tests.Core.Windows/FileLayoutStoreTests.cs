using Text_Grab.Core.Layouts;

namespace Text_Grab.Tests.Core.Windows;

/// <summary><see cref="FileLayoutStore"/> (v5 E4a) against a folder of its own.</summary>
public sealed class FileLayoutStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "TextGrabLayoutStoreTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task Classic_IsBuiltInAndHasTheCalcPaneCollapsedToTheRightOfTheEditor()
    {
        FileLayoutStore store = new(directory);

        LayoutReadResult read = await store.LoadAsync(LayoutIds.Classic, TestContext.Current.CancellationToken);

        Assert.True(read.Succeeded, read.Error);
        SplitNode root = Assert.IsType<SplitNode>(read.Layout!.Root);
        Assert.Equal(DockOrientation.Horizontal, root.Orientation);
        Assert.Equal(PaneIds.Editor, Assert.IsType<PaneNode>(root.Children[0].Node).PaneId);
        Assert.Equal((1d, DockSizeUnit.Star, false), (root.Children[0].Size, root.Children[0].Unit, root.Children[0].Collapsed));
        Assert.Equal(PaneIds.Calc, Assert.IsType<PaneNode>(root.Children[1].Node).PaneId);
        // Today's window: the calc column is 200 px in XAML and the pane starts closed.
        Assert.Equal((200d, DockSizeUnit.Pixel, true), (root.Children[1].Size, root.Children[1].Unit, root.Children[1].Collapsed));
        Assert.Null(read.Layout.Chrome);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task Save_ThenLoad_GivesTheLayoutBack()
    {
        FileLayoutStore store = new(directory);
        EtwLayoutDefinition layout = BuiltInLayouts.Classic with { Id = "wide-calc", Name = "Wide calc" };

        await store.SaveAsync(layout, TestContext.Current.CancellationToken);
        LayoutReadResult read = await store.LoadAsync("wide-calc", TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(directory, "wide-calc.json")));
        Assert.True(read.Succeeded, read.Error);
        Assert.Equal(EtwLayoutSerializer.Serialize(layout), EtwLayoutSerializer.Serialize(read.Layout!));
    }

    [Fact]
    public async Task Save_OverABuiltInLayout_IsRefused()
    {
        FileLayoutStore store = new(directory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(BuiltInLayouts.Classic, TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("Upper")]
    [InlineData("con")]
    public async Task Save_UnderAnIdThatCannotBeAFileName_IsRefused(string id)
    {
        FileLayoutStore store = new(directory);

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(BuiltInLayouts.Classic with { Id = id }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Load_AnIdThatCannotBeAFileName_FailsWithoutTouchingTheDisk()
    {
        Directory.CreateDirectory(directory);
        string outside = Path.Combine(Path.GetDirectoryName(directory)!, "outside.json");
        await File.WriteAllTextAsync(outside, EtwLayoutSerializer.Serialize(BuiltInLayouts.Classic with { Id = "outside" }), TestContext.Current.CancellationToken);
        FileLayoutStore store = new(directory);

        try
        {
            LayoutReadResult read = await store.LoadAsync("../outside", TestContext.Current.CancellationToken);

            Assert.False(read.Succeeded);
            Assert.Contains("can't be a layout ID", read.Error);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task Load_ALayoutThatIsNotThere_FailsWithAReason()
    {
        LayoutReadResult read = await new FileLayoutStore(directory).LoadAsync("missing", TestContext.Current.CancellationToken);

        Assert.False(read.Succeeded);
        Assert.Contains("no layout \"missing\"", read.Error);
    }

    [Fact]
    public async Task Load_AMalformedFile_FailsWithAReasonRatherThanThrowing()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "broken.json"), "{ \"schemaVersion\": \"1.0\", \"id\": ", TestContext.Current.CancellationToken);

        LayoutReadResult read = await new FileLayoutStore(directory).LoadAsync("broken", TestContext.Current.CancellationToken);

        Assert.False(read.Succeeded);
        Assert.Contains("\"broken\"", read.Error);
    }

    [Fact]
    public async Task Load_TheFileNameIsTheId()
    {
        // A copy of a layout file that still carries the original's ID.
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "copy.json"), EtwLayoutSerializer.Serialize(BuiltInLayouts.Classic with { Id = "original" }), TestContext.Current.CancellationToken);

        LayoutReadResult read = await new FileLayoutStore(directory).LoadAsync("copy", TestContext.Current.CancellationToken);

        Assert.Equal("copy", read.Layout?.Id);
    }

    [Fact]
    public async Task Load_ABuiltInId_IsTheBuiltInLayoutEvenWithAFileOfThatName()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "classic.json"), EtwLayoutSerializer.Serialize(BuiltInLayouts.Classic with { Name = "Impostor" }), TestContext.Current.CancellationToken);

        LayoutReadResult read = await new FileLayoutStore(directory).LoadAsync(LayoutIds.Classic, TestContext.Current.CancellationToken);

        Assert.Equal("Classic", read.Layout?.Name);
    }

    [Fact]
    public async Task List_GivesBuiltInLayoutsFirstThenSavedOnesAndSkipsFilesThatCannotBeLayouts()
    {
        FileLayoutStore store = new(directory);
        await store.SaveAsync(BuiltInLayouts.Classic with { Id = "zeta", Name = "Zeta" }, TestContext.Current.CancellationToken);
        await store.SaveAsync(BuiltInLayouts.Classic with { Id = "alpha", Name = "Alpha" }, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "Not A Layout.json"), "{}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "broken.json"), "{", TestContext.Current.CancellationToken);

        IReadOnlyList<LayoutSummary> layouts = await store.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [("classic", "Classic", true), ("explorer", "Explorer", true), ("mermaid", "Mermaid", true), ("alpha", "Alpha", false), ("broken", "broken", false), ("zeta", "Zeta", false)],
            layouts.Select(layout => (layout.Id, layout.Name, layout.IsBuiltIn)));
    }

    [Fact]
    public async Task Delete_RemovesASavedLayoutButNeverABuiltInOne()
    {
        FileLayoutStore store = new(directory);
        await store.SaveAsync(BuiltInLayouts.Classic with { Id = "mine" }, TestContext.Current.CancellationToken);

        Assert.True(await store.DeleteAsync("mine", TestContext.Current.CancellationToken));
        Assert.False(await store.DeleteAsync("mine", TestContext.Current.CancellationToken));
        Assert.False(await store.DeleteAsync(LayoutIds.Classic, TestContext.Current.CancellationToken));
        Assert.False((await store.LoadAsync("mine", TestContext.Current.CancellationToken)).Succeeded);
    }
}
