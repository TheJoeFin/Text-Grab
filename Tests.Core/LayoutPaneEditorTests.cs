using Text_Grab.Core.Layouts;

namespace Text_Grab.Tests.Core;

/// <summary>Adding and removing panes from a layout's dock tree for the Panes menu (v5 E6 <c>picker</c>).</summary>
public class LayoutPaneEditorTests
{
    private static string[] Ids(EtwLayoutDefinition layout) => [.. EtwLayoutValidator.PaneIdsIn(layout.Root)];

    [Fact]
    public void AddingOnTheLeft_PutsThePaneFirstInTheHorizontalSplit()
    {
        EtwLayoutDefinition result = LayoutPaneEditor.AddPane(BuiltInLayouts.Classic, PaneIds.Recent, PaneSide.Left, 260);

        Assert.Equal([PaneIds.Recent, PaneIds.Editor, PaneIds.Calc], Ids(result));
        DockChild added = ((SplitNode)result.Root).Children[0];
        Assert.Equal((260d, DockSizeUnit.Pixel, false), (added.Size, added.Unit, added.Collapsed));
    }

    [Fact]
    public void AddingOnTheRight_PutsThePaneLast()
        => Assert.Equal([PaneIds.Editor, PaneIds.Calc, PaneIds.Mermaid], Ids(LayoutPaneEditor.AddPane(BuiltInLayouts.Classic, PaneIds.Mermaid, PaneSide.Right, 300)));

    [Theory]
    [InlineData(PaneSide.Top, true)]
    [InlineData(PaneSide.Bottom, false)]
    public void AddingAboveOrBelow_WrapsTheHorizontalRootInAVerticalSplit(PaneSide side, bool paneFirst)
    {
        EtwLayoutDefinition result = LayoutPaneEditor.AddPane(BuiltInLayouts.Classic, PaneIds.Mermaid, side, 150);

        SplitNode root = Assert.IsType<SplitNode>(result.Root);
        Assert.Equal(DockOrientation.Vertical, root.Orientation);
        Assert.Equal(2, root.Children.Count);
        Assert.Equal(paneFirst ? PaneIds.Mermaid : null, (root.Children[0].Node as PaneNode)?.PaneId);
        Assert.Equal(DockSizeUnit.Star, root.Children[paneFirst ? 1 : 0].Unit);
        string[] expected = paneFirst
            ? [PaneIds.Mermaid, PaneIds.Editor, PaneIds.Calc]
            : [PaneIds.Editor, PaneIds.Calc, PaneIds.Mermaid];
        Assert.Equal(expected, Ids(result));
    }

    [Fact]
    public void AddingAPaneTheLayoutHas_ChangesNothing()
        => Assert.Same(BuiltInLayouts.Classic, LayoutPaneEditor.AddPane(BuiltInLayouts.Classic, PaneIds.Calc, PaneSide.Left, 200));

    [Fact]
    public void AddingWithAnUnusableSize_UsesAFallback()
    {
        EtwLayoutDefinition result = LayoutPaneEditor.AddPane(BuiltInLayouts.Classic, PaneIds.Recent, PaneSide.Left, 0);

        Assert.True(((SplitNode)result.Root).Children[0].Size > 0);
    }

    [Fact]
    public void Removing_TakesThePaneOutAndKeepsTheRest()
    {
        EtwLayoutDefinition result = LayoutPaneEditor.RemovePane(BuiltInLayouts.Classic, PaneIds.Calc);

        Assert.Equal([PaneIds.Editor], Ids(result));
    }

    [Fact]
    public void RemovingTheEditorOrAPaneThatIsNotThere_ChangesNothing()
    {
        Assert.Same(BuiltInLayouts.Classic, LayoutPaneEditor.RemovePane(BuiltInLayouts.Classic, PaneIds.Editor));
        Assert.Equal(Ids(BuiltInLayouts.Classic), Ids(LayoutPaneEditor.RemovePane(BuiltInLayouts.Classic, PaneIds.Recent)));
    }

    [Fact]
    public void AddingThenRemoving_GivesBackTheSamePanes()
    {
        EtwLayoutDefinition wrapped = LayoutPaneEditor.AddPane(BuiltInLayouts.Classic, PaneIds.Recent, PaneSide.Top, 100);
        EtwLayoutDefinition back = LayoutPaneEditor.RemovePane(wrapped, PaneIds.Recent);

        Assert.Equal(Ids(BuiltInLayouts.Classic), Ids(back));
        Assert.IsType<SplitNode>(back.Root);
        Assert.Equal(DockOrientation.Horizontal, ((SplitNode)back.Root).Orientation);
    }

    [Fact]
    public void PaneChanges_IgnoreSizesAndTheCalcPanesShownState()
    {
        SplitNode root = (SplitNode)BuiltInLayouts.Classic.Root;
        EtwLayoutDefinition resized = BuiltInLayouts.Classic with { Root = root with { Children = [root.Children[0], root.Children[1] with { Size = 500, Collapsed = false }] } };

        Assert.False(LayoutPaneEditor.HasPaneChanges(resized, BuiltInLayouts.Classic));
    }

    [Fact]
    public void PaneChanges_SeeAnAddedPaneARemovedPaneAndACollapsedOne()
    {
        Assert.True(LayoutPaneEditor.HasPaneChanges(LayoutPaneEditor.AddPane(BuiltInLayouts.Classic, PaneIds.Recent, PaneSide.Left, 260), BuiltInLayouts.Classic));
        Assert.True(LayoutPaneEditor.HasPaneChanges(LayoutPaneEditor.RemovePane(BuiltInLayouts.Classic, PaneIds.Calc), BuiltInLayouts.Classic));

        Assert.True(BuiltInLayouts.TryGet(LayoutIds.Explorer, out EtwLayoutDefinition explorer));
        SplitNode root = (SplitNode)explorer.Root;
        EtwLayoutDefinition collapsed = explorer with { Root = root with { Children = [root.Children[0] with { Collapsed = true }, .. root.Children.Skip(1)] } };
        Assert.True(LayoutPaneEditor.HasPaneChanges(collapsed, explorer));
    }
}
