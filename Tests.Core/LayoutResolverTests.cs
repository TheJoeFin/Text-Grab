using Text_Grab.Core.Layouts;

namespace Text_Grab.Tests.Core;

/// <summary>
/// The order an Edit Text Window's layout is chosen in (D5, v5 E4a steps 10-11), and the built-in
/// file-type map, which ships empty.
/// </summary>
public class LayoutResolverTests
{
    private static readonly EtwLayoutDefinition Mermaid = BuiltInLayouts.Classic with { Id = "mermaid", Name = "Mermaid" };
    private static readonly EtwLayoutDefinition Saved = BuiltInLayouts.Classic with { Id = "saved", Name = "Saved" };

    private static readonly Dictionary<string, string> TestMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mmd"] = "mermaid",
        [".gone"] = "not-in-the-store",
    };

    private static LayoutResolver Resolver(params EtwLayoutDefinition[] stored)
        => new(new MemoryStore(stored), paneId => paneId == PaneIds.Calc, path => LayoutFileTypeDefaults.LayoutIdFor(path, TestMap));

    // ---- The built-in map ----

    [Fact]
    public void BuiltInMap_ShipsEmpty()
    {
        Assert.Empty(LayoutFileTypeDefaults.Entries);
        Assert.Null(LayoutFileTypeDefaults.LayoutIdFor(@"C:\notes\diagram.mmd"));
        Assert.Null(LayoutFileTypeDefaults.LayoutIdFor(@"C:\notes\readme.md"));
    }

    [Theory]
    [InlineData(@"C:\diagrams\flow.mmd", "mermaid")]
    [InlineData(@"C:\diagrams\FLOW.MMD", "mermaid")]
    [InlineData(@"C:\notes\readme.md", null)]
    [InlineData(@"C:\notes\no-extension", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Map_LooksUpTheExtension(string? path, string? expected)
        => Assert.Equal(expected, LayoutFileTypeDefaults.LayoutIdFor(path, TestMap));

    // ---- The order ----

    [Fact]
    public async Task NothingAsked_GivesClassicWithNoNotice()
    {
        ResolvedLayout resolved = await Resolver().ResolveAsync(new LayoutRequest(), TestContext.Current.CancellationToken);

        Assert.Same(BuiltInLayouts.Classic, resolved.Layout);
        Assert.Null(resolved.Notice);
    }

    [Fact]
    public async Task TheCallersLayout_BeatsTheHistoryItemAndTheFileType()
    {
        ResolvedLayout resolved = await Resolver(Mermaid, Saved).ResolveAsync(
            new LayoutRequest(RequestedId: "saved", HistoryId: "mermaid", FilePath: @"C:\a.mmd"), TestContext.Current.CancellationToken);

        Assert.Equal("saved", resolved.Layout.Id);
    }

    [Fact]
    public async Task AHistoryItemsLayout_BeatsTheFileType()
    {
        ResolvedLayout resolved = await Resolver(Mermaid, Saved).ResolveAsync(
            new LayoutRequest(HistoryId: "saved", FilePath: @"C:\a.mmd"), TestContext.Current.CancellationToken);

        Assert.Equal("saved", resolved.Layout.Id);
        Assert.Null(resolved.Notice);
    }

    [Fact]
    public async Task AMappedFileType_GivesItsLayout()
    {
        ResolvedLayout resolved = await Resolver(Mermaid).ResolveAsync(new LayoutRequest(FilePath: @"C:\a.mmd"), TestContext.Current.CancellationToken);

        Assert.Equal("mermaid", resolved.Layout.Id);
    }

    [Fact]
    public async Task AnExtensionNotInTheMap_GivesClassic()
    {
        ResolvedLayout resolved = await Resolver(Mermaid).ResolveAsync(new LayoutRequest(FilePath: @"C:\notes.txt"), TestContext.Current.CancellationToken);

        Assert.Same(BuiltInLayouts.Classic, resolved.Layout);
        Assert.Null(resolved.Notice);
    }

    [Fact]
    public async Task AMappedLayoutMissingFromTheStore_FallsBackToClassicAndSaysWhy()
    {
        ResolvedLayout resolved = await Resolver().ResolveAsync(new LayoutRequest(FilePath: @"C:\a.gone"), TestContext.Current.CancellationToken);

        Assert.Same(BuiltInLayouts.Classic, resolved.Layout);
        Assert.Contains("not-in-the-store", resolved.Notice);
    }

    [Fact]
    public async Task ARequestedLayoutThatDoesNotLoad_GivesClassicAndSaysWhyRatherThanTheNextChoice()
    {
        // A person who asked for a layout hears that they did not get it.
        ResolvedLayout resolved = await Resolver(Mermaid).ResolveAsync(
            new LayoutRequest(RequestedId: "missing", HistoryId: "mermaid"), TestContext.Current.CancellationToken);

        Assert.Same(BuiltInLayouts.Classic, resolved.Layout);
        Assert.Contains("\"missing\"", resolved.Notice);
        Assert.Contains("Classic", resolved.Notice);
    }

    [Fact]
    public async Task ALayoutWithAnUnknownPane_GivesClassicAndNamesThePane()
    {
        EtwLayoutDefinition unknown = BuiltInLayouts.Classic with
        {
            Id = "telescope",
            Root = new PaneNode("textgrab.pane.telescope"),
        };

        ResolvedLayout resolved = await Resolver(unknown).ResolveAsync(new LayoutRequest(RequestedId: "telescope"), TestContext.Current.CancellationToken);

        Assert.Same(BuiltInLayouts.Classic, resolved.Layout);
        Assert.Contains("textgrab.pane.telescope", resolved.Notice);
    }

    [Fact]
    public async Task AMalformedLayoutFile_GivesClassicAndSaysWhy()
    {
        ResolvedLayout resolved = await new LayoutResolver(new MemoryStore(broken: "broken"), _ => true)
            .ResolveAsync(new LayoutRequest(RequestedId: "broken"), TestContext.Current.CancellationToken);

        Assert.Same(BuiltInLayouts.Classic, resolved.Layout);
        Assert.Contains("not valid", resolved.Notice);
    }

    [Theory]
    [InlineData("classic")]
    [InlineData("  ")]
    public async Task ClassicOrNothing_NeverAsksTheStore(string requested)
    {
        ResolvedLayout resolved = await new LayoutResolver(new MemoryStore(throwOnLoad: true), _ => true)
            .ResolveAsync(new LayoutRequest(RequestedId: requested), TestContext.Current.CancellationToken);

        Assert.Same(BuiltInLayouts.Classic, resolved.Layout);
    }

    /// <summary>A store that holds the given layouts, and optionally one malformed file.</summary>
    private sealed class MemoryStore(EtwLayoutDefinition[]? layouts = null, string? broken = null, bool throwOnLoad = false) : ILayoutStore
    {
        public MemoryStore(params EtwLayoutDefinition[] layouts) : this(layouts, null, false)
        {
        }

        public Task<IReadOnlyList<LayoutSummary>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LayoutSummary>>([]);

        public Task<LayoutReadResult> LoadAsync(string id, CancellationToken cancellationToken = default)
        {
            if (throwOnLoad)
                throw new InvalidOperationException("The store was asked.");

            if (id == broken)
            {
                LayoutReadResult read = EtwLayoutSerializer.Read("{ not json");
                return Task.FromResult(LayoutReadResult.Failed($"The layout \"{id}\" could not be used. {read.Error}"));
            }

            EtwLayoutDefinition? layout = layouts?.FirstOrDefault(candidate => candidate.Id == id);
            return Task.FromResult(layout is null ? LayoutReadResult.Failed($"There is no layout \"{id}\".") : new LayoutReadResult(layout, null));
        }

        public Task SaveAsync(EtwLayoutDefinition layout, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
