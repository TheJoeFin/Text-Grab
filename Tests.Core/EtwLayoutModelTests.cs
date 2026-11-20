using System.Text.Json;
using System.Text.Json.Nodes;
using Text_Grab.Core.Layouts;
using Text_Grab.Models;

namespace Text_Grab.Tests.Core;

/// <summary>The layout model (v5 E4a): JSON round trips, what a file may omit, and what it may not.</summary>
public class EtwLayoutModelTests
{
    private const string Classic = """
        {
          "schemaVersion": "1.0",
          "id": "classic",
          "name": "Classic",
          "root": {
            "type": "split",
            "orientation": "horizontal",
            "children": [
              { "node": { "type": "pane", "paneId": "textgrab.pane.editor" }, "size": 1, "unit": "star" },
              { "node": { "type": "pane", "paneId": "textgrab.pane.calc" }, "size": 200, "unit": "pixel", "collapsed": true }
            ]
          }
        }
        """;

    // ---- Round trips ----

    [Fact]
    public void Read_ClassicShape_GivesTheTree()
    {
        LayoutReadResult result = EtwLayoutSerializer.Read(Classic);

        Assert.True(result.Succeeded, result.Error);
        EtwLayoutDefinition layout = result.Layout!;
        Assert.Equal("classic", layout.Id);
        SplitNode root = Assert.IsType<SplitNode>(layout.Root);
        Assert.Equal(DockOrientation.Horizontal, root.Orientation);
        Assert.Equal(2, root.Children.Count);
        Assert.Equal(PaneIds.Editor, Assert.IsType<PaneNode>(root.Children[0].Node).PaneId);
        Assert.Equal(DockSizeUnit.Star, root.Children[0].Unit);
        Assert.False(root.Children[0].Collapsed);
        Assert.Equal(PaneIds.Calc, Assert.IsType<PaneNode>(root.Children[1].Node).PaneId);
        Assert.Equal(200, root.Children[1].Size);
        Assert.Equal(DockSizeUnit.Pixel, root.Children[1].Unit);
        Assert.True(root.Children[1].Collapsed);
    }

    [Fact]
    public void Serialize_ThenRead_GivesTheSameJson()
    {
        EtwLayoutDefinition layout = new(
            EtwLayoutSerializer.CurrentSchemaVersion,
            "everything",
            "Everything",
            new SplitNode(DockOrientation.Vertical,
            [
                new DockChild(new SplitNode(DockOrientation.Horizontal,
                [
                    new DockChild(new PaneNode(PaneIds.Editor), 2, DockSizeUnit.Star),
                    new DockChild(new PaneNode(PaneIds.Calc, JsonDocument.Parse("""{"precision":3}""").RootElement), 240, DockSizeUnit.Pixel, Collapsed: true),
                ]), 1, DockSizeUnit.Star),
                new DockChild(new TabGroupNode([new PaneNode("textgrab.pane.recent"), new PaneNode("textgrab.pane.mermaid")], SelectedIndex: 1), 180, DockSizeUnit.Pixel),
            ]),
            DefaultMode: EtwEditorMode.Code,
            DefaultCodeLanguage: "C#",
            Window: new WindowOptions(AlwaysOnTop: true, RestorePosition: false, ShowBottomBar: true),
            BottomBarActionIds: ["textgrab.copy-close"],
            StatusItemIds: ["textgrab.status.words"]);

        string json = EtwLayoutSerializer.Serialize(layout);
        LayoutReadResult read = EtwLayoutSerializer.Read(json);

        Assert.True(read.Succeeded, read.Error);
        AssertSameJson(json, EtwLayoutSerializer.Serialize(read.Layout!));
    }

    [Fact]
    public void Serialize_WritesEnumsByNameAndOmitsWhatIsUnset()
    {
        string json = EtwLayoutSerializer.Serialize(EtwLayoutSerializer.Read(Classic).Layout!);

        Assert.Contains("\"orientation\": \"horizontal\"", json);
        Assert.Contains("\"unit\": \"pixel\"", json);
        Assert.Contains("\"type\": \"split\"", json);
        // Collapsed is written only when true, and null members not at all.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"collapsed\""));
        Assert.DoesNotContain("defaultMode", json);
        Assert.DoesNotContain("chrome", json);
        Assert.DoesNotContain("null", json);
    }

    [Fact]
    public void Read_TheTypeNeedNotComeFirst()
    {
        // System.Text.Json wants the discriminator first by default; people editing a file do not.
        string json = Classic.Replace("""{ "type": "pane", "paneId": "textgrab.pane.calc" }""", """{ "paneId": "textgrab.pane.calc", "type": "pane" }""");

        Assert.True(EtwLayoutSerializer.Read(json).Succeeded);
    }

    // ---- Chrome members (E4b), which E4a carries but does not render ----

    [Fact]
    public void Read_AFileWithNoChromeMembers_LeavesThemNull()
    {
        EtwLayoutDefinition layout = EtwLayoutSerializer.Read(Classic).Layout!;

        Assert.Null(layout.Chrome);
        Assert.Null(layout.BottomBarActionIds);
        Assert.Null(layout.StatusItemIds);
        Assert.Null(layout.DefaultMode);
        Assert.Null(layout.Window);
    }

    [Fact]
    public void ChromeMembers_SurviveALoadAndSaveUntouched()
    {
        string json = """
            {
              "schemaVersion": "1.0",
              "id": "with-chrome",
              "name": "With chrome",
              "root": { "type": "pane", "paneId": "textgrab.pane.editor" },
              "chrome": {
                "kind": "menuBar",
                "groups": [
                  {
                    "title": "File",
                    "accessKey": "F",
                    "items": [
                      { "kind": "action", "actionId": "textgrab.file.open" },
                      { "kind": "separator" },
                      { "kind": "submenu", "title": "Recent", "children": [ { "kind": "dynamicList", "dynamicSourceId": "textgrab.recent" } ] }
                    ],
                    "laterGroupOption": 4
                  }
                ]
              },
              "bottomBarActionIds": [ "textgrab.copy-close", "textgrab.single-line" ],
              "statusItemIds": [ "textgrab.status.words" ]
            }
            """;

        LayoutReadResult read = EtwLayoutSerializer.Read(json);

        Assert.True(read.Succeeded, read.Error);
        Assert.Equal(ChromeKind.MenuBar, read.Layout!.Chrome!.Kind);
        AssertSameJson(json, EtwLayoutSerializer.Serialize(read.Layout));
    }

    [Fact]
    public void UnknownMembers_SurviveALoadAndSave()
    {
        // A layout written by a later Text Grab, opened and saved by this one.
        string json = """
            {
              "schemaVersion": "1.3",
              "id": "later",
              "name": "Later",
              "ribbonAccent": "teal",
              "root": {
                "type": "split",
                "orientation": "horizontal",
                "splitterStyle": "thin",
                "children": [ { "node": { "type": "pane", "paneId": "textgrab.pane.editor", "pinned": true }, "size": 1, "unit": "star" } ]
              }
            }
            """;

        LayoutReadResult read = EtwLayoutSerializer.Read(json);

        Assert.True(read.Succeeded, read.Error);
        AssertSameJson(json, EtwLayoutSerializer.Serialize(read.Layout!));
    }

    // ---- Tab groups ----

    [Fact]
    public void TabGroup_UnselectedTabsSurviveASave()
    {
        string json = """
            {
              "schemaVersion": "1.0",
              "id": "tabs",
              "name": "Tabs",
              "root": {
                "type": "split",
                "orientation": "horizontal",
                "children": [
                  { "node": { "type": "pane", "paneId": "textgrab.pane.editor" }, "size": 1, "unit": "star" },
                  {
                    "node": { "type": "tabs", "selectedIndex": 2, "tabs": [
                      { "type": "pane", "paneId": "textgrab.pane.recent" },
                      { "type": "pane", "paneId": "textgrab.pane.folder-tree", "config": { "root": "C:\\Notes" } },
                      { "type": "pane", "paneId": "textgrab.pane.calc" }
                    ] },
                    "size": 220, "unit": "pixel"
                  }
                ]
              }
            }
            """;

        EtwLayoutDefinition layout = EtwLayoutSerializer.Read(json).Layout!;
        TabGroupNode tabs = Assert.IsType<TabGroupNode>(((SplitNode)layout.Root).Children[1].Node);

        Assert.Equal(2, tabs.SelectedIndex);
        Assert.Equal(["textgrab.pane.recent", "textgrab.pane.folder-tree", "textgrab.pane.calc"], tabs.Tabs.Select(tab => tab.PaneId));
        AssertSameJson(json, EtwLayoutSerializer.Serialize(layout));
    }

    // ---- What a file may not do ----

    [Theory]
    [InlineData("2.0")]
    [InlineData("10")]
    public void Read_ANewerMajorVersion_IsRefused(string version)
    {
        LayoutReadResult result = EtwLayoutSerializer.Read(Classic.Replace("\"1.0\"", $"\"{version}\""));

        Assert.False(result.Succeeded);
        Assert.Contains("newer", result.Error);
    }

    [Theory]
    [InlineData("1.9")]
    [InlineData("1")]
    public void Read_ANewerMinorVersion_Loads(string version)
        => Assert.True(EtwLayoutSerializer.Read(Classic.Replace("\"1.0\"", $"\"{version}\"")).Succeeded);

    [Theory]
    [InlineData("")]
    [InlineData("one")]
    public void Read_AnUnreadableVersion_IsRefused(string version)
        => Assert.False(EtwLayoutSerializer.Read(Classic.Replace("\"1.0\"", $"\"{version}\"")).Succeeded);

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{ "schemaVersion": "1.0", "id": "x", "name": "X" }""")]
    [InlineData("""{ "schemaVersion": "1.0", "name": "X", "root": { "type": "pane", "paneId": "textgrab.pane.editor" } }""")]
    [InlineData("""{ "schemaVersion": "1.0", "id": "x", "name": "X", "root": { "type": "floating", "paneId": "textgrab.pane.editor" } }""")]
    [InlineData("""{ "schemaVersion": "1.0", "id": "x", "name": "X", "root": { "paneId": "textgrab.pane.editor" } }""")]
    [InlineData("""{ "schemaVersion": "1.0", "id": "x", "name": "X", "root": { "type": "split", "orientation": "sideways", "children": [] } }""")]
    [InlineData("""{ "schemaVersion": "1.0", "id": "x", "name": "X", "root": { "type": "split", "orientation": "horizontal" } }""")]
    [InlineData("""{ "schemaVersion": "1.0", "id": "x", "name": "X", "root": { "type": "split", "orientation": "horizontal", "children": [ { "size": 1, "unit": "star" } ] } }""")]
    [InlineData("""{ "schemaVersion": "1.0", "id": "x", "name": "X", "root": { "type": "pane" } }""")]
    public void Read_AMalformedFile_FailsWithAReasonRatherThanThrowing(string json)
    {
        LayoutReadResult result = EtwLayoutSerializer.Read(json);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Read_AbsurdNesting_FailsRatherThanThrowing()
    {
        string pane = """{ "type": "pane", "paneId": "textgrab.pane.editor" }""";
        string node = pane;
        for (int i = 0; i < 40; i++)
            node = $$"""{ "type": "split", "orientation": "horizontal", "children": [ { "node": {{node}}, "size": 1, "unit": "star" } ] }""";

        LayoutReadResult result = EtwLayoutSerializer.Read($$"""{ "schemaVersion": "1.0", "id": "deep", "name": "Deep", "root": {{node}} }""");

        Assert.False(result.Succeeded);
    }

    // ---- Validation and repair ----

    [Fact]
    public void Validator_AnUnknownPaneId_IsAProblemThatNamesIt()
    {
        EtwLayoutDefinition layout = EtwLayoutSerializer.Read(Classic.Replace(PaneIds.Calc, "textgrab.pane.telescope")).Layout!;

        string? problem = EtwLayoutValidator.FindProblem(layout, id => id == PaneIds.Calc);

        Assert.NotNull(problem);
        Assert.Contains("textgrab.pane.telescope", problem);
    }

    [Fact]
    public void Validator_Classic_HasNoProblem()
        => Assert.Null(EtwLayoutValidator.FindProblem(EtwLayoutSerializer.Read(Classic).Layout!, id => id == PaneIds.Calc));

    [Fact]
    public void Validator_ThesamePaneTwice_IsAProblem()
    {
        EtwLayoutDefinition layout = new("1.0", "twice", "Twice", new SplitNode(DockOrientation.Horizontal,
        [
            new DockChild(new PaneNode(PaneIds.Editor), 1, DockSizeUnit.Star),
            new DockChild(new PaneNode(PaneIds.Calc), 200, DockSizeUnit.Pixel),
            new DockChild(new TabGroupNode([new PaneNode(PaneIds.Calc)]), 200, DockSizeUnit.Pixel),
        ]));

        Assert.Contains("more than once", EtwLayoutValidator.FindProblem(layout, _ => true));
    }

    [Fact]
    public void Validator_TheEditorInATabGroup_IsAProblem()
    {
        EtwLayoutDefinition layout = new("1.0", "editor-tab", "Editor tab",
            new TabGroupNode([new PaneNode(PaneIds.Editor), new PaneNode(PaneIds.Calc)]));

        Assert.NotNull(EtwLayoutValidator.FindProblem(layout, _ => true));
    }

    [Fact]
    public void Validator_EmptySplitsAndTabGroups_AreProblems()
    {
        EtwLayoutDefinition emptySplit = new("1.0", "a", "A", new SplitNode(DockOrientation.Horizontal, []));
        EtwLayoutDefinition emptyTabs = new("1.0", "b", "B", new SplitNode(DockOrientation.Horizontal,
            [new DockChild(new TabGroupNode([]), 1, DockSizeUnit.Star)]));

        Assert.NotNull(EtwLayoutValidator.FindProblem(emptySplit, _ => true));
        Assert.NotNull(EtwLayoutValidator.FindProblem(emptyTabs, _ => true));
    }

    [Fact]
    public void Normalize_ALayoutWithNoEditor_GetsOneFirst()
    {
        EtwLayoutDefinition layout = new("1.0", "calc-only", "Calc only", new SplitNode(DockOrientation.Horizontal,
            [new DockChild(new PaneNode(PaneIds.Calc), 200, DockSizeUnit.Pixel)]));

        EtwLayoutDefinition normalized = EtwLayoutValidator.Normalize(layout);

        SplitNode root = Assert.IsType<SplitNode>(normalized.Root);
        Assert.Equal([PaneIds.Editor, PaneIds.Calc], EtwLayoutValidator.PaneIdsIn(root));
        Assert.Equal(DockSizeUnit.Star, root.Children[0].Unit);
    }

    [Fact]
    public void Normalize_ALonePane_IsWrappedBesideTheEditor()
    {
        EtwLayoutDefinition normalized = EtwLayoutValidator.Normalize(new("1.0", "x", "X", new PaneNode(PaneIds.Calc)));

        Assert.Equal([PaneIds.Editor, PaneIds.Calc], EtwLayoutValidator.PaneIdsIn(normalized.Root));
    }

    [Fact]
    public void Normalize_ALayoutWithAnEditor_IsUnchanged()
    {
        EtwLayoutDefinition layout = EtwLayoutSerializer.Read(Classic).Layout!;

        Assert.Same(layout, EtwLayoutValidator.Normalize(layout));
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(-5, 200)]
    [InlineData(double.NaN, 200)]
    [InlineData(double.PositiveInfinity, 200)]
    [InlineData(260, 260)]
    public void SanitizeSize_ReplacesUnusableSizes(double size, double expected)
        => Assert.Equal(expected, EtwLayoutValidator.SanitizeSize(size, fallback: 200));

    // ---- IDs ----

    [Theory]
    [InlineData("classic")]
    [InlineData("mermaid")]
    [InlineData("my-layout_2.v1")]
    [InlineData("0")]
    public void LayoutIds_AcceptsPlainIds(string id) => Assert.True(LayoutIds.IsValid(id));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Classic")]
    [InlineData("../classic")]
    [InlineData("..")]
    [InlineData(".hidden")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a b")]
    [InlineData("c:")]
    [InlineData("con")]
    [InlineData("nul.layout")]
    [InlineData("trailing.")]
    [InlineData("é")]
    public void LayoutIds_RefusesWhatCouldNotBeAPlainFileName(string? id) => Assert.False(LayoutIds.IsValid(id));

    // ---- Built-in layouts ----

    [Fact]
    public void Explorer_IsBuiltInWithRecentLeftOfTheEditorAndCalcCollapsedOnTheRight()
    {
        Assert.True(BuiltInLayouts.TryGet(LayoutIds.Explorer, out EtwLayoutDefinition explorer));
        Assert.True(LayoutIds.IsValid(explorer.Id));
        Assert.Null(EtwLayoutValidator.FindProblem(explorer, id => id is PaneIds.Recent or PaneIds.Calc));
        Assert.Equal([PaneIds.Recent, PaneIds.Editor, PaneIds.Calc], EtwLayoutValidator.PaneIdsIn(explorer.Root));

        SplitNode root = Assert.IsType<SplitNode>(explorer.Root);
        Assert.Equal(DockOrientation.Horizontal, root.Orientation);
        Assert.Equal((260d, DockSizeUnit.Pixel, false), (root.Children[0].Size, root.Children[0].Unit, root.Children[0].Collapsed));
        Assert.Equal((1d, DockSizeUnit.Star, false), (root.Children[1].Size, root.Children[1].Unit, root.Children[1].Collapsed));
        Assert.Equal((200d, DockSizeUnit.Pixel, true), (root.Children[2].Size, root.Children[2].Unit, root.Children[2].Collapsed));
    }

    [Fact]
    public void Classic_DoesNotHaveTheRecentPane()
        => Assert.DoesNotContain(PaneIds.Recent, EtwLayoutValidator.PaneIdsIn(BuiltInLayouts.Classic.Root));

    [Fact]
    public void LayoutIds_RefusesOverlongIds() => Assert.False(LayoutIds.IsValid(new string('a', LayoutIds.MaxLength + 1)));

    private static void AssertSameJson(string expected, string actual)
        => Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"Expected:\n{expected}\nActual:\n{actual}");
}
