using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Editing;
using Text_Grab.Utilities;
using static Tests.EditTextWindowTestHost;
using Editor = Tests.MarkdownTabBehaviorTests.Editor;

namespace Tests;

/// <summary>The marker, indent and spacing each list level gets (bullets •◦▪ and 1. a. i.), which is display only.</summary>
public class MarkdownListStylingTests
{
    private static FlowDocument Create(string markdown) =>
        MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

    /// <summary>The marker style of each list from the outside in, following the first item of each.</summary>
    private static List<TextMarkerStyle> StylesByDepth(FlowDocument document)
    {
        List<TextMarkerStyle> styles = [];
        for (List? list = document.Blocks.OfType<List>().First(); list is not null;
            list = list.ListItems.First().Blocks.OfType<List>().FirstOrDefault())
            styles.Add(list.MarkerStyle);
        return styles;
    }

    [WpfFact]
    public void Bullets_CycleDotRingFilledSquare_ThenStartOver_NeverTheHollowBoxThatLooksLikeACheckbox()
    {
        FlowDocument document = Create("- a\n  - b\n    - c\n      - d\n        - e\n");

        Assert.Equal(
            [TextMarkerStyle.Disc, TextMarkerStyle.Circle, TextMarkerStyle.Box, TextMarkerStyle.Disc, TextMarkerStyle.Circle],
            StylesByDepth(document));
    }

    [WpfFact]
    public void NumberedLists_CycleNumberLetterRoman_ThenStartOver()
    {
        FlowDocument document = Create("1. a\n   1. b\n      1. c\n         1. d\n            1. e\n");

        Assert.Equal(
            [TextMarkerStyle.Decimal, TextMarkerStyle.LowerLatin, TextMarkerStyle.LowerRoman, TextMarkerStyle.Decimal, TextMarkerStyle.LowerLatin],
            StylesByDepth(document));
    }

    [WpfFact]
    public void MixedLists_FollowTheirDepthAcrossBothKinds()
    {
        FlowDocument document = Create("1. a\n   - b\n     1. c\n        - d\n");

        Assert.Equal(
            [TextMarkerStyle.Decimal, TextMarkerStyle.Circle, TextMarkerStyle.LowerRoman, TextMarkerStyle.Disc],
            StylesByDepth(document));
    }

    [WpfFact]
    public void EveryLevelStepsInTheSameAmount_AndItsItemsAreSpacedTightly()
    {
        FlowDocument document = Create("- a\n  - b\n    1. c\n");

        List top = document.Blocks.OfType<List>().Single();
        List middle = top.ListItems.First().Blocks.OfType<List>().Single();
        List inner = middle.ListItems.First().Blocks.OfType<List>().Single();
        foreach (List list in new[] { top, middle, inner })
        {
            Assert.Equal(MarkdownFlowDocumentUtilities.ListLevelIndent, list.Padding.Left);
            Assert.Equal(MarkdownFlowDocumentUtilities.ListMarkerGap, list.MarkerOffset);
        }

        Paragraph text = (Paragraph)top.ListItems.First().Blocks.First();
        Assert.Equal(MarkdownFlowDocumentUtilities.ListItemSpacing, text.Margin.Top);
        Assert.Equal(MarkdownFlowDocumentUtilities.ListItemSpacing, text.Margin.Bottom);
    }

    [WpfFact]
    public void ALettersListIsStillSavedAsNumbers()
    {
        string markdown = "1. a\n   1. b\n      1. c";

        Assert.Equal(markdown, SerializeUnix(Create(markdown)));
    }

    [WpfFact]
    public void ATopLevelToDoList_KeepsItsNoMarkerLook_ButItsNestedListsStillCycle()
    {
        FlowDocument document = Create("- [ ] a\n- [ ] b\n  - c\n");

        List top = document.Blocks.OfType<List>().Single();
        Assert.Equal(TextMarkerStyle.None, top.MarkerStyle);
        Assert.Equal(0, top.Padding.Left);
        Assert.Equal(TextMarkerStyle.Circle, top.ListItems.Last().Blocks.OfType<List>().Single().MarkerStyle);
    }

    [WpfFact]
    public void TheFootnoteList_KeepsItsOwnNumbering()
    {
        FlowDocument document = Create("Text[^1]\n\n[^1]: The note.\n");

        List notes = document.Blocks.OfType<List>().Single();
        Assert.Equal(TextMarkerStyle.Decimal, notes.MarkerStyle);
        Assert.NotEqual(MarkdownFlowDocumentUtilities.ListLevelIndent, notes.Padding.Left);
    }

    [WpfFact]
    public void Tab_RestylesTheNestedListAtItsNewDepth_AndShiftTabRestylesItBack()
    {
        using Editor bullets = new("- a\n- b\n");
        bullets.CaretAfter("b");
        Assert.True(bullets.Tab());
        Assert.Equal([TextMarkerStyle.Disc, TextMarkerStyle.Circle], StylesByDepth(bullets.Box.Document));
        Assert.True(bullets.Tab(outdent: true));
        Assert.Equal(TextMarkerStyle.Disc, StylesByDepth(bullets.Box.Document)[0]);
        Assert.Single(StylesByDepth(bullets.Box.Document));

        using Editor numbers = new("1. a\n2. b\n3. c\n");
        numbers.CaretAfter("b");
        Assert.True(numbers.Tab());
        numbers.CaretAfter("c");
        Assert.True(numbers.Tab());
        Assert.True(numbers.Tab());
        Assert.Equal([TextMarkerStyle.Decimal, TextMarkerStyle.LowerLatin, TextMarkerStyle.LowerRoman], StylesByDepth(numbers.Box.Document));
        Assert.Equal("1. a\n   1. b\n      1. c", numbers.Markdown);
    }

    [WpfFact]
    public void ATabAndItsRestyling_AreOneUndoStep()
    {
        using Editor editor = new("1. a\n2. b\n");
        editor.CaretAfter("b");
        Assert.True(editor.Tab());
        Assert.Equal(TextMarkerStyle.LowerLatin, StylesByDepth(editor.Box.Document)[1]);

        ApplicationCommands.Undo.Execute(null, editor.Box);
        DoEvents();

        Assert.Equal("1. a\n2. b", editor.Markdown);
        Assert.Equal([TextMarkerStyle.Decimal], StylesByDepth(editor.Box.Document));
    }

    [WpfFact]
    public void TheToolbarsBulletsAndNumbering_GetTheSameStylingAsAParsedList()
    {
        using Editor editor = new("One\n\nTwo\n");
        editor.Select("One", "One");
        MarkdownFlowDocumentUtilities.ApplyFormatting(editor.Box, MarkdownFormattingAction.Numbering, 0, editor.Box, isLightTheme: true);
        DoEvents();

        List list = editor.Box.Document.Blocks.OfType<List>().Single();
        Assert.Equal(TextMarkerStyle.Decimal, list.MarkerStyle);
        Assert.Equal(MarkdownFlowDocumentUtilities.ListLevelIndent, list.Padding.Left);
    }

    private static string SerializeUnix(FlowDocument document) =>
        MarkdownFlowDocumentUtilities.SerializeToMarkdown(document).Replace("\r\n", "\n");
}
