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
    public void BuiltInMap_HasExactlyTheMermaidEntries()
    {
        // In 4.17 the only entries are .mmd and .mermaid (E5 mermaid). A new entry is a decision, so it is pinned.
        Assert.Equal(
            [(".mermaid", LayoutIds.Mermaid), (".mmd", LayoutIds.Mermaid)],
            LayoutFileTypeDefaults.Entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => (entry.Key, entry.Value)));
    }

    [Theory]
    [InlineData(@"C:\notes\diagram.mmd", "mermaid")]
    [InlineData(@"C:\notes\diagram.MMD", "mermaid")]
    [InlineData(@"C:\notes\diagram.mermaid", "mermaid")]
    [InlineData(@"C:\notes\readme.md", null)]
    [InlineData(@"C:\notes\readme.markdown", null)]
    [InlineData(@"C:\notes\notes.txt", null)]
    [InlineData(@"C:\notes\flow.mmd.txt", null)]
    public void BuiltInMap_MapsMermaidFilesAndNothingElse(string path, string? expected)
        => Assert.Equal(expected, LayoutFileTypeDefaults.LayoutIdFor(path));

    [Fact]
    public void TheMermaidLayoutIsBuiltIn_SoAMermaidFileNeverNeedsTheStore()
    {
        Assert.True(BuiltInLayouts.IsBuiltIn(LayoutFileTypeDefaults.LayoutIdFor(@"C:\a.mmd")));
        Assert.True(BuiltInLayouts.IsBuiltIn(LayoutFileTypeDefaults.LayoutIdFor(@"C:\a.mermaid")));
    }

    [Fact]
    public async Task AnMmdFile_ResolvesToTheBuiltInMermaidLayoutThroughTheRealMap()
    {
        Assert.True(BuiltInLayouts.TryGet(LayoutIds.Mermaid, out EtwLayoutDefinition mermaid));

        ResolvedLayout resolved = await new LayoutResolver(new MemoryStore(mermaid), paneId => paneId is PaneIds.Mermaid)
            .ResolveAsync(new LayoutRequest(FilePath: @"C:\a.mmd"), TestContext.Current.CancellationToken);

        Assert.Same(mermaid, resolved.Layout);
        Assert.Null(resolved.Notice);
    }

    [Fact]
    public async Task AnMmdFileInABuildWithoutTheMermaidPane_FallsBackToClassicAndNamesThePane()
    {
        Assert.True(BuiltInLayouts.TryGet(LayoutIds.Mermaid, out EtwLayoutDefinition mermaid));

        ResolvedLayout resolved = await new LayoutResolver(new MemoryStore(mermaid), paneId => paneId == PaneIds.Calc)
            .ResolveAsync(new LayoutRequest(FilePath: @"C:\a.mmd"), TestContext.Current.CancellationToken);

        Assert.Same(BuiltInLayouts.Classic, resolved.Layout);
        Assert.Contains(PaneIds.Mermaid, resolved.Notice);
    }

    [Fact]
    public void ChooseId_NeedsNoStore_AndFollowsTheSameOrder()
    {
        Assert.Equal("saved", LayoutResolver.ChooseId(new LayoutRequest("saved", "mermaid", @"C:\a.mmd"), null));
        Assert.Equal("mermaid", LayoutResolver.ChooseId(new LayoutRequest(null, "mermaid", @"C:\a.txt"), null));
        Assert.Equal("mermaid", LayoutResolver.ChooseId(new LayoutRequest(FilePath: @"C:\a.mmd"), null));
        Assert.Null(LayoutResolver.ChooseId(new LayoutRequest(FilePath: @"C:\readme.md"), null));
        Assert.Null(LayoutResolver.ChooseId(new LayoutRequest(" ", "", null), null));
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

        public LayoutReadResult Load(string id) => LoadAsync(id).GetAwaiter().GetResult();

        public LayoutStoreDefaults GlobalDefault { get; set; } = new(null);

        public LayoutStoreDefaults ReadDefaults() => GlobalDefault;

        public Task<LayoutStoreDefaults> ReadDefaultsAsync(CancellationToken cancellationToken = default) => Task.FromResult(GlobalDefault);

        public Task SetGlobalDefaultAsync(string? id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // ---- The global default (E6 picker) ----

    [Fact]
    public async Task TheGlobalDefault_IsUsedWhenNothingMoreSpecificIsChosen()
    {
        MemoryStore store = new(Saved) { GlobalDefault = new("saved") };

        ResolvedLayout resolved = await new LayoutResolver(store, paneId => paneId == PaneIds.Calc).ResolveAsync(new LayoutRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("saved", resolved.Layout.Id);
        Assert.Null(resolved.Notice);
    }

    [Fact]
    public async Task TheGlobalDefault_LosesToTheCallerTheHistoryItemAndTheFileType()
    {
        MemoryStore store = new(Saved, Mermaid) { GlobalDefault = new("saved") };
        LayoutResolver resolver = new(store, _ => true, path => LayoutFileTypeDefaults.LayoutIdFor(path, TestMap));

        Assert.Equal("classic", (await resolver.ResolveAsync(new LayoutRequest(RequestedId: "classic"), TestContext.Current.CancellationToken)).Layout.Id);
        Assert.Equal("mermaid", (await resolver.ResolveAsync(new LayoutRequest(HistoryId: "mermaid"), TestContext.Current.CancellationToken)).Layout.Id);
        Assert.Equal("mermaid", (await resolver.ResolveAsync(new LayoutRequest(FilePath: @"C:\a.mmd"), TestContext.Current.CancellationToken)).Layout.Id);
    }

    [Fact]
    public async Task AMissingGlobalDefault_FallsBackToClassicAndSaysWhy()
    {
        MemoryStore store = new() { GlobalDefault = new("gone") };

        ResolvedLayout resolved = await new LayoutResolver(store, _ => true).ResolveAsync(new LayoutRequest(), TestContext.Current.CancellationToken);

        Assert.Same(BuiltInLayouts.Classic, resolved.Layout);
        Assert.Contains("gone", resolved.Notice);
    }

    [Fact]
    public async Task AMalformedMetadataFile_FallsBackToClassicAndSaysWhy()
    {
        MemoryStore store = new() { GlobalDefault = new(null, "The layout settings file is not valid.") };

        ResolvedLayout resolved = await new LayoutResolver(store, _ => true).ResolveAsync(new LayoutRequest(), TestContext.Current.CancellationToken);

        Assert.Same(BuiltInLayouts.Classic, resolved.Layout);
        Assert.Contains("not valid", resolved.Notice);
    }

    [Fact]
    public void TheChoiceWithAGlobalDefault_PutsItLast()
    {
        Assert.Equal("saved", LayoutResolver.ChooseId(new LayoutRequest(GlobalDefaultId: "saved"), null));
        Assert.Equal("explorer", LayoutResolver.ChooseId(new LayoutRequest(RequestedId: "explorer", GlobalDefaultId: "saved"), null));
    }
}
