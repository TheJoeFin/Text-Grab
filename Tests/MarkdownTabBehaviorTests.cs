using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab;
using Text_Grab.Editing;
using Text_Grab.Utilities;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>Tab and Shift+Tab in the rendered Markdown editor, and that what they make survives a save and reload.</summary>
public class MarkdownTabBehaviorTests
{
    private const string Em = MarkdownTabBehavior.Indent;

    private static string Unix(string text) => text.Replace("\r\n", "\n");

    /// <summary>A focused RichTextBox in a shown window: the editing commands do nothing otherwise.</summary>
    internal sealed class Editor : IDisposable
    {
        private readonly Window window;

        public Editor(string markdown)
        {
            Box = new RichTextBox
            {
                Document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16),
                AcceptsTab = true,
                IsDocumentEnabled = true,
                Width = 600,
                Height = 400,
            };
            window = new Window { Content = Box, Width = 700, Height = 500, ShowActivated = true, Left = 40, Top = 40 };
            window.Show();
            window.UpdateLayout();
            DoEvents();
            Keyboard.Focus(Box);
        }

        public RichTextBox Box { get; }

        public string Markdown => Unix(MarkdownFlowDocumentUtilities.SerializeToMarkdown(Box.Document));

        public void CaretAfter(string text) => Box.CaretPosition = After(text);

        public void Select(string from, string to) => Box.Selection.Select(After(from).GetPositionAtOffset(-from.Length)!, After(to));

        public bool Tab(bool outdent = false)
        {
            bool handled = MarkdownTabBehavior.TryHandle(Box, outdent);
            DoEvents();
            return handled;
        }

        public string Reloaded() => Unix(MarkdownFlowDocumentUtilities.SerializeToMarkdown(
            MarkdownFlowDocumentUtilities.CreateFlowDocument(Markdown, new FontFamily("Segoe UI"), 16)));

        private TextPointer After(string text)
        {
            for (TextPointer? p = Box.Document.ContentStart; p is not null; p = p.GetNextContextPosition(LogicalDirection.Forward))
            {
                if (p.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
                    continue;

                int index = p.GetTextInRun(LogicalDirection.Forward).IndexOf(text, StringComparison.Ordinal);
                if (index >= 0)
                    return p.GetPositionAtOffset(index + text.Length)!;
            }

            throw new InvalidOperationException($"'{text}' is not in the document.");
        }

        public void Dispose() => window.Close();
    }

    [WpfFact]
    public void OnAnEmptyLine_TabIndentsWithAnEmSpace_AndShiftTabTakesItBack()
    {
        using Editor editor = new("First\n");
        Paragraph empty = new();
        editor.Box.Document.Blocks.Add(empty);
        editor.Box.CaretPosition = empty.ContentStart;

        Assert.True(editor.Tab());
        Assert.True(editor.Tab());
        Assert.Equal($"{Em}{Em}", new TextRange(empty.ContentStart, empty.ContentEnd).Text.TrimEnd('\r', '\n'));

        Assert.True(editor.Tab(outdent: true));
        Assert.Equal(Em, new TextRange(empty.ContentStart, empty.ContentEnd).Text.TrimEnd('\r', '\n'));
    }

    [WpfFact]
    public void AtTheStartOfAParagraph_TabIndentsWithAnEmSpace_ThatSurvivesAReload_UnlikeATabOrSpaces()
    {
        using Editor editor = new("Hello world\n");
        editor.Box.CaretPosition = editor.Box.Document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);

        Assert.True(editor.Tab());

        Assert.Equal($"{Em}Hello world", editor.Markdown);
        Assert.Equal(editor.Markdown, editor.Reloaded());
    }

    [WpfFact]
    public void InTheMiddleOfText_TabIsLeftToTheRichTextBox()
    {
        using Editor editor = new("Hello world\n");
        editor.CaretAfter("Hello");

        Assert.False(editor.Tab());
        Assert.Equal("Hello world", editor.Markdown);
    }

    [WpfFact]
    public void InAListItem_TabNestsIt_AndShiftTabBringsItBack_AtEveryDepth()
    {
        using Editor editor = new("- a\n- b\n- c\n- d\n");

        editor.CaretAfter("b");
        Assert.True(editor.Tab());
        editor.CaretAfter("c");
        Assert.True(editor.Tab());
        Assert.True(editor.Tab());

        Assert.Equal("- a\n  - b\n    - c\n- d", editor.Markdown);
        Assert.Equal(editor.Markdown, editor.Reloaded());

        Assert.True(editor.Tab(outdent: true));
        Assert.Equal("- a\n  - b\n  - c\n- d", editor.Markdown);
        Assert.Equal(editor.Markdown, editor.Reloaded());
    }

    [WpfFact]
    public void InAListItem_TabAnywhereInTheItemNestsIt_NotJustAtItsStart()
    {
        using Editor editor = new("- first\n- second item\n");
        editor.CaretAfter("second");

        Assert.True(editor.Tab());

        Assert.Equal("- first\n  - second item", editor.Markdown);
        Assert.DoesNotContain('\t', editor.Markdown);
    }

    [WpfFact]
    public void InAListItem_TheFirstItemCannotNest_AndATopLevelItemCannotOutdent()
    {
        using Editor editor = new("- a\n- b\n");
        string before = editor.Markdown;

        editor.CaretAfter("a");
        Assert.True(editor.Tab());
        Assert.True(editor.Tab(outdent: true));
        editor.CaretAfter("b");
        Assert.True(editor.Tab(outdent: true));

        Assert.Equal(before, editor.Markdown);
    }

    [WpfFact]
    public void InAnOrderedAndATaskList_TabNestsAndKeepsTheMarkers()
    {
        using Editor ordered = new("1. a\n2. b\n");
        ordered.CaretAfter("b");
        Assert.True(ordered.Tab());
        Assert.Equal("1. a\n   1. b", ordered.Markdown);
        Assert.Equal(ordered.Markdown, ordered.Reloaded());

        using Editor tasks = new("- [ ] a\n- [x] b\n");
        tasks.CaretAfter("b");
        Assert.True(tasks.Tab());
        Assert.Equal("- [ ] a\n  - [x] b", tasks.Markdown);
        Assert.Equal(tasks.Markdown, tasks.Reloaded());
    }

    [WpfFact]
    public void InATable_TabIsLeftToTheRichTextBox_WhichMovesBetweenCells()
    {
        using Editor editor = new("| A | B |\n|---|---|\n| 1 | 2 |\n");
        editor.CaretAfter("1");

        Assert.False(editor.Tab());
    }

    [WpfFact]
    public void InACodeBlock_TabInsertsATab_AndShiftTabRemovesTheOneBeforeTheCaret()
    {
        using Editor editor = new("```\ncode\n```\n");
        editor.CaretAfter("code");

        Assert.False(editor.Tab());
        editor.Box.CaretPosition.InsertTextInRun("\t");
        editor.CaretAfter("code\t");
        Assert.True(editor.Tab(outdent: true));

        Assert.DoesNotContain('\t', editor.Markdown);
    }

    [WpfFact]
    public void AcrossSeveralParagraphs_TabIndentsEachOne_WithoutReplacingTheSelectedText()
    {
        using Editor editor = new("One\n\nTwo\n\nThree\n");
        editor.Select("One", "Three");

        Assert.True(editor.Tab());
        Assert.Equal($"{Em}One\n\n{Em}Two\n\n{Em}Three", editor.Markdown);

        Assert.True(editor.Tab(outdent: true));
        Assert.Equal("One\n\nTwo\n\nThree", editor.Markdown);
    }

    [WpfTheory]
    [InlineData("- a\n  - b\n    - c\n- d")]
    [InlineData("- a\n  - b\n    - c\n      - d")]
    [InlineData("1. a\n   1. b\n      1. c\n2. e")]
    [InlineData("- [ ] a\n  - [x] b\n    - [ ] c")]
    [InlineData("- a\n  - b\n  - c\n- d")]
    public void NestedLists_SerializeWithTheirOwnIndentOnly_SoTheyReloadAsTheSameStructure(string markdown)
    {
        // Each level was indented twice over (4, then 10 spaces), so the third level came back as a code block.
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

        Assert.Equal(markdown, Unix(MarkdownFlowDocumentUtilities.SerializeToMarkdown(document)));
    }

    [WpfFact]
    public void TheMarkdownEditor_AcceptsTab_SoTabNeverMovesFocusToALink()
    {
        EditTextWindow window = CreateWindow("[a link](https://example.com)\n");

        Assert.True(MarkdownEditor(window).AcceptsTab);
    }
}
