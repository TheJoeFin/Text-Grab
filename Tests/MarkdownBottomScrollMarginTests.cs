using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Text_Grab;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using Text_Grab.Utilities;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// The blank space under the last Markdown block. A RichTextBox rewrites its document's PagePadding
/// to 5,0,5,0 when its template is first applied, so the margin only holds if it is set after that.
/// </summary>
public class MarkdownBottomScrollMarginTests
{
    private static string LongMarkdown()
    {
        StringBuilder markdown = new();
        for (int i = 0; i < 40; i++)
            markdown.AppendLine($"- [ ] item {i}");
        return markdown.ToString();
    }

    private static ScrollViewer ScrollerOf(RichTextBox box) => (ScrollViewer)box.Template.FindName("PART_ContentHost", box);

    private static void LayOutBox(RichTextBox box)
    {
        box.UpdateLayout();
        DoEvents();
        box.UpdateLayout();
    }

    [WpfFact]
    public void TheMarkdownEditor_ScrollsPastTheLastBlock_WithoutShrinkingItsViewportOrTouchingTheText()
    {
        EditTextWindow window = CreateWindow(LongMarkdown());
        SwitchMode(window, EtwEditorMode.Markdown);
        RichTextBox editor = MarkdownEditor(window);
        ScrollViewer scroller = ScrollerOf(editor);

        Assert.Equal(MarkdownFlowDocumentUtilities.BottomScrollMargin, editor.Document.PagePadding.Bottom);
        double extentWithMargin = scroller.ExtentHeight;
        double viewportWithMargin = scroller.ViewportHeight;
        string textWithMargin = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;

        editor.Document.PagePadding = new Thickness(5, 0, 5, 0);
        LayOutBox(editor);

        Assert.Equal(MarkdownFlowDocumentUtilities.BottomScrollMargin, extentWithMargin - scroller.ExtentHeight, 1);
        Assert.Equal(scroller.ViewportHeight, viewportWithMargin, 1);
        Assert.Equal(textWithMargin, new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
    }

    [WpfFact]
    public void TheMarkdownEditor_KeepsTheMarginWhenItsDocumentIsReloaded()
    {
        EditTextWindow window = CreateWindow(LongMarkdown());
        SwitchMode(window, EtwEditorMode.Markdown);
        SwitchMode(window, EtwEditorMode.Text);
        RawTextBox(window).Text = "# Changed\n\n" + LongMarkdown();
        SwitchMode(window, EtwEditorMode.Markdown);

        Assert.Equal(MarkdownFlowDocumentUtilities.BottomScrollMargin, MarkdownEditor(window).Document.PagePadding.Bottom);
    }

    [WpfFact]
    public async Task TheMarkdownPreview_ScrollsPastTheLastBlock()
    {
        EditTextWindow window = CreateWindow(LongMarkdown());
        MarkdownPreviewPane pane = new();
        pane.Attach(window.Session, config: null);
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!pane.ViewModel.HasDocument)
        {
            Assert.True(DateTime.UtcNow < deadline, "The preview never rendered.");
            await Task.Delay(10);
        }

        pane.Measure(new Size(420, 400));
        pane.Arrange(new Rect(0, 0, 420, 400));
        pane.UpdateLayout();
        RichTextBox preview = pane.PreviewBox;
        ScrollViewer scroller = ScrollerOf(preview);

        Assert.Equal(MarkdownFlowDocumentUtilities.BottomScrollMargin, preview.Document.PagePadding.Bottom);
        double extentWithMargin = scroller.ExtentHeight;

        preview.Document.PagePadding = new Thickness(5, 0, 5, 0);
        LayOutBox(preview);

        Assert.Equal(MarkdownFlowDocumentUtilities.BottomScrollMargin, extentWithMargin - scroller.ExtentHeight, 1);
        pane.Detach();
    }
}
