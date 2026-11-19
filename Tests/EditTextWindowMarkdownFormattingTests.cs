using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using Text_Grab;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Utilities;
using static Tests.EditTextWindowTestHost;

namespace Tests;

public class EditTextWindowMarkdownFormattingTests
{
    [WpfFact]
    public void ToolbarIsVisibleOnlyInMarkdownMode()
    {
        EditTextWindow window = CreateWindow("Text");
        MarkdownFormattingBar toolbar = Toolbar(window);
        Assert.Equal(Visibility.Collapsed, toolbar.Visibility);

        SwitchMode(window, EtwEditorMode.Markdown);
        Assert.Equal(Visibility.Visible, toolbar.Visibility);

        foreach (EtwEditorMode mode in new[] { EtwEditorMode.Text, EtwEditorMode.Spreadsheet, EtwEditorMode.Code })
        {
            SwitchMode(window, mode);
            Assert.Equal(Visibility.Collapsed, toolbar.Visibility);
        }
    }

    [WpfFact]
    public void EmphasisFormatsOnlyTheSelectionAndCanBeToggledOff()
    {
        EditTextWindow window = OpenMarkdown("before middle after");
        RichTextBox editor = MarkdownEditor(window);
        Run run = Assert.IsType<Run>(Assert.IsType<Paragraph>(editor.Document.Blocks.FirstBlock).Inlines.FirstInline);
        editor.Selection.Select(run.ContentStart.GetPositionAtOffset(7), run.ContentStart.GetPositionAtOffset(13));

        ClickButton(window, "BoldButton");
        Assert.Equal("before **middle** after", RawText(window));
        ClickButton(window, "ItalicButton");
        Assert.Equal("before ***middle*** after", RawText(window));
        Assert.Equal("middle", editor.Selection.Text);

        ClickButton(window, "BoldButton");
        Assert.Equal("before *middle* after", RawText(window));
        editor.Undo();
        Assert.Equal("before ***middle*** after", RawText(window));
    }

    [WpfFact]
    public void EmphasisAcrossExistingFormattingWritesOneOuterSpan()
    {
        EditTextWindow window = OpenMarkdown("one *two* three");
        MarkdownEditor(window).SelectAll();

        ClickButton(window, "BoldButton");

        Assert.Equal("**one *two* three**", RawText(window));
    }

    [WpfFact]
    public void EmphasisCannotStripAnExtendedInline()
    {
        EditTextWindow window = OpenMarkdown("one ~~two~~ three");
        RichTextBox editor = MarkdownEditor(window);
        editor.SelectAll();
        Assert.False(((Button)Toolbar(window).FindName("BoldButton")).IsEnabled);

        Run run = Assert.IsType<Run>(Assert.IsType<Paragraph>(editor.Document.Blocks.FirstBlock).Inlines.FirstInline);
        editor.Selection.Select(run.ContentStart, run.ContentStart.GetPositionAtOffset(3));
        ClickButton(window, "BoldButton");
        Assert.Equal("**one** ~~two~~ three", RawText(window));
    }

    [WpfTheory]
    [InlineData("StrikethroughButton", "~~", BaselineAlignment.Baseline)]
    [InlineData("SuperscriptButton", "^", BaselineAlignment.Superscript)]
    [InlineData("SubscriptButton", "~", BaselineAlignment.Subscript)]
    public void ExtendedEmphasisFormatsOnlyTheSelectionAndCanBeToggledOff(string buttonName, string delimiter, BaselineAlignment alignment)
    {
        EditTextWindow window = OpenMarkdown("before middle after");
        RichTextBox editor = MarkdownEditor(window);
        Paragraph paragraph = Assert.IsType<Paragraph>(editor.Document.Blocks.FirstBlock);
        Run run = Assert.IsType<Run>(paragraph.Inlines.FirstInline);
        editor.Selection.Select(run.ContentStart.GetPositionAtOffset(7), run.ContentStart.GetPositionAtOffset(13));

        ClickButton(window, buttonName);
        string formatted = $"before {delimiter}middle{delimiter} after";
        Assert.Equal(formatted, RawText(window));
        Assert.Equal("middle", editor.Selection.Text);
        Span span = Assert.Single(paragraph.Inlines.OfType<Span>());
        if (alignment == BaselineAlignment.Baseline)
            Assert.Equal(TextDecorationLocation.Strikethrough, Assert.Single(span.TextDecorations).Location);
        else
        {
            Assert.Equal(alignment, span.BaselineAlignment);
            Assert.True(span.FontSize < paragraph.FontSize);
        }

        editor.Undo();
        Assert.Equal("before middle after", RawText(window));
        editor.Redo();
        Assert.Equal(formatted, RawText(window));

        ClickButton(window, buttonName);
        Assert.Equal("before middle after", RawText(window));
        editor.Undo();
        Assert.Equal(formatted, RawText(window));
    }

    [WpfFact]
    public void StrikethroughPreservesExistingEmphasisAcrossParagraphs()
    {
        string source = $"first{Environment.NewLine}{Environment.NewLine}**second** sentence";
        EditTextWindow window = OpenMarkdown(source);
        RichTextBox editor = MarkdownEditor(window);
        editor.SelectAll();

        ClickButton(window, "StrikethroughButton");

        Assert.Equal($"~~first~~{Environment.NewLine}{Environment.NewLine}~~**second** sentence~~", RawText(window));
    }

    [WpfFact]
    public void StrikethroughAcrossAListItemsHardBreakPreservesTheSelection()
    {
        EditTextWindow window = OpenMarkdown("- **before middle**  \n  another part");
        RichTextBox editor = MarkdownEditor(window);
        Paragraph paragraph = Assert.IsType<Paragraph>(
            Assert.IsType<List>(editor.Document.Blocks.FirstBlock).ListItems.FirstListItem!.Blocks.FirstBlock);
        Run first = Assert.IsType<Run>(Assert.IsType<Bold>(paragraph.Inlines.FirstInline).Inlines.FirstInline);
        Run last = Assert.IsType<Run>(paragraph.Inlines.LastInline);
        editor.Selection.Select(first.ContentStart.GetPositionAtOffset(7), last.ContentStart.GetPositionAtOffset(7));
        string selectedText = editor.Selection.Text;

        ClickButton(window, "StrikethroughButton");

        Assert.Equal($"- **before ~~middle~~**  {Environment.NewLine}  ~~another~~ part", RawText(window));
        Assert.Equal(selectedText, editor.Selection.Text);
        FlowDocument reloaded = MarkdownFlowDocumentUtilities.CreateFlowDocument(RawText(window), editor.FontFamily, editor.FontSize);
        Assert.Equal(MarkdownFlowDocumentUtilities.GetDocumentPlainText(editor.Document), MarkdownFlowDocumentUtilities.GetDocumentPlainText(reloaded));
    }

    [WpfFact]
    public void ChangingPartOfAScriptPreservesTheUnselectedEnds()
    {
        EditTextWindow window = OpenMarkdown("x^123^");
        RichTextBox editor = MarkdownEditor(window);
        Paragraph paragraph = Assert.IsType<Paragraph>(editor.Document.Blocks.FirstBlock);
        Span script = Assert.Single(paragraph.Inlines.OfType<Span>());
        Run run = Assert.IsType<Run>(script.Inlines.FirstInline);
        editor.Selection.Select(run.ContentStart.GetPositionAtOffset(1), run.ContentStart.GetPositionAtOffset(2));

        ClickButton(window, "SubscriptButton");

        Assert.Equal("x^1^~2~^3^", RawText(window));
        Assert.Equal("2", editor.Selection.Text);
        FlowDocument reloaded = MarkdownFlowDocumentUtilities.CreateFlowDocument(RawText(window), editor.FontFamily, editor.FontSize);
        Assert.Equal("x123", MarkdownFlowDocumentUtilities.GetDocumentPlainText(reloaded));
        Assert.Equal(new[] { BaselineAlignment.Superscript, BaselineAlignment.Subscript, BaselineAlignment.Superscript },
            Assert.IsType<Paragraph>(reloaded.Blocks.FirstBlock).Inlines.OfType<Span>().Select(span => span.BaselineAlignment));

        editor.Undo();
        Assert.Equal("x^123^", RawText(window));
        editor.Redo();
        Assert.Equal("x^1^~2~^3^", RawText(window));
    }

    [WpfFact]
    public void AdjacentSubscriptEditsSaveAsOneDelimiterPair()
    {
        EditTextWindow window = OpenMarkdown("H~1~2O");
        RichTextBox editor = MarkdownEditor(window);
        Run run = Assert.IsType<Run>(Assert.IsType<Paragraph>(editor.Document.Blocks.FirstBlock).Inlines.LastInline);
        editor.Selection.Select(run.ContentStart, run.ContentStart.GetPositionAtOffset(1));

        ClickButton(window, "SubscriptButton");

        Assert.Equal("H~12~O", RawText(window));
        FlowDocument reloaded = MarkdownFlowDocumentUtilities.CreateFlowDocument(RawText(window), editor.FontFamily, editor.FontSize);
        Span script = Assert.Single(Assert.IsType<Paragraph>(reloaded.Blocks.FirstBlock).Inlines.OfType<Span>());
        Assert.Equal(BaselineAlignment.Subscript, script.BaselineAlignment);
        Assert.Equal("12", new TextRange(script.ContentStart, script.ContentEnd).Text);
        Assert.Equal("2", editor.Selection.Text);
    }

    [WpfFact]
    public void HeadingAndNormalTextChangeOnlyTheCurrentParagraphAndAreUndoable()
    {
        EditTextWindow window = OpenMarkdown($"Title{Environment.NewLine}{Environment.NewLine}Body");
        RichTextBox editor = MarkdownEditor(window);
        Paragraph title = Assert.IsType<Paragraph>(editor.Document.Blocks.FirstBlock);
        editor.CaretPosition = title.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        Button heading = (Button)Toolbar(window).FindName("HeadingButton");
        MenuItem[] styles = heading.ContextMenu.Items.OfType<MenuItem>().ToArray();

        Click(styles.Single(item => Equals(item.Header, "Heading 3")));
        Assert.Equal($"### Title{Environment.NewLine}{Environment.NewLine}Body", RawText(window));
        Assert.True(title.FontSize > editor.Document.FontSize);

        Click(styles.Single(item => Equals(item.Header, "Normal text")));
        Assert.Equal($"Title{Environment.NewLine}{Environment.NewLine}Body", RawText(window));
        Assert.Equal(editor.Document.FontSize, title.FontSize);
        editor.Undo();
        Assert.StartsWith("### Title", RawText(window));
    }

    [WpfFact]
    public void ListActionsConvertTheSelectedParagraphsAndSwitchMarkerStyles()
    {
        string source = $"alpha{Environment.NewLine}{Environment.NewLine}beta";
        EditTextWindow window = OpenMarkdown(source);
        MarkdownEditor(window).SelectAll();

        ClickButton(window, "BulletsButton");
        Assert.Equal($"- alpha{Environment.NewLine}- beta", RawText(window));
        ClickButton(window, "NumberingButton");
        Assert.Equal($"1. alpha{Environment.NewLine}2. beta", RawText(window));
        ClickButton(window, "NumberingButton");
        Assert.Equal(source, RawText(window));
    }

    [WpfFact]
    public void CodeBlockCombinesParagraphsWithoutInterpretingTheirText()
    {
        string newline = Environment.NewLine;
        EditTextWindow window = OpenMarkdown($"first{newline}{newline}second");
        RichTextBox editor = MarkdownEditor(window);
        editor.SelectAll();

        ClickButton(window, "CodeBlockButton");
        Assert.Equal($"```{newline}first\nsecond{newline}```", RawText(window));
        Assert.True(MarkdownFlowDocumentUtilities.TryGetCodeBlock(
            (Paragraph)editor.Document.Blocks.FirstBlock!, out _, out _));
        Assert.False(((Button)Toolbar(window).FindName("BoldButton")).IsEnabled);

        editor.Undo();
        Assert.Equal($"first{newline}{newline}second", RawText(window));
        editor.Redo();
        Assert.Equal($"```{newline}first\nsecond{newline}```", RawText(window));
    }

    [WpfFact]
    public void BlockQuoteTogglesWithoutChangingInlineFormatting()
    {
        EditTextWindow window = OpenMarkdown("a **quoted** sentence");
        ClickButton(window, "QuoteButton");
        Assert.Equal("> a **quoted** sentence", RawText(window));

        ClickButton(window, "QuoteButton");
        Assert.Equal("a **quoted** sentence", RawText(window));
    }

    [WpfTheory]
    [InlineData(3, 2)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    public void TablePickerHighlightsTheRectangleAndInsertsThatSizeAtTheCaret(int columns, int rows)
    {
        EditTextWindow window = OpenMarkdown("before after");
        RichTextBox editor = MarkdownEditor(window);
        Paragraph paragraph = Assert.IsType<Paragraph>(editor.Document.Blocks.FirstBlock);
        editor.CaretPosition = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward).GetPositionAtOffset(7);
        UniformGrid picker = (UniformGrid)Toolbar(window).FindName("TableSizeGrid");
        Button picked = (Button)picker.Children[(rows - 1) * picker.Columns + columns - 1];
        picked.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
        Assert.Equal(columns * rows, picker.Children.OfType<Button>()
            .Count(cell => cell.ReadLocalValue(Control.BackgroundProperty) != DependencyProperty.UnsetValue));

        Click(picked);
        Table table = Assert.Single(editor.Document.Blocks.OfType<Table>());
        TableRowGroup group = Assert.Single(table.RowGroups);
        Assert.Equal(rows, group.Rows.Count);
        Assert.All(group.Rows, row => Assert.Equal(columns, row.Cells.Count));
        Assert.Same(group.Rows[0].Cells[0].Blocks.FirstBlock, editor.CaretPosition.Paragraph);
        Assert.StartsWith("before " + Environment.NewLine, RawText(window));
        Assert.EndsWith("after", RawText(window));

        FlowDocument roundTrip = MarkdownFlowDocumentUtilities.CreateFlowDocument(RawText(window), editor.FontFamily, editor.FontSize);
        Assert.Equal(rows, Assert.Single(Assert.Single(roundTrip.Blocks.OfType<Table>()).RowGroups).Rows.Count);

        DoEvents();
        editor.Undo();
        Assert.Equal("before after", RawText(window));
        Assert.Empty(editor.Document.Blocks.OfType<Table>());
    }

    private static EditTextWindow OpenMarkdown(string text)
    {
        EditTextWindow window = CreateWindow(text);
        SwitchMode(window, EtwEditorMode.Markdown);
        return window;
    }

    private static MarkdownFormattingBar Toolbar(EditTextWindow window) => (MarkdownFormattingBar)window.FindName("MarkdownToolbar");

    private static void ClickButton(EditTextWindow window, string name) => Click((Button)Toolbar(window).FindName(name));

    private static void Click(Button button)
    {
        Assert.True(button.IsEnabled);
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
    }

    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
}
