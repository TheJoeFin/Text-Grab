using System.Windows;
using Text_Grab;
using Text_Grab.Core.Editing;
using Text_Grab.Models;
using static Tests.EditTextWindowTestHost;

namespace Tests;

public class EditTextWindowProtocolPasteTests
{
    [WpfFact]
    public void EnterMarkdownMode_BeforeShowingWindow_RendersLoadedText()
    {
        const string markdown = "# Heading\r\n\r\nSome **bold** text.";
        EditTextWindow window = new();
        window.AddThisText(markdown);

        window.EnterMarkdownMode();
        LayOut(window);

        Assert.Equal(EtwEditorMode.Markdown, window.Session.Mode);
        Assert.Equal(markdown, window.Session.Text);
        Assert.Equal(Visibility.Visible, MarkdownEditor(window).Visibility);
        Assert.Equal(Visibility.Collapsed, RawTextBox(window).Visibility);
        Assert.Contains("Heading", RenderedMarkdown(window));
        Assert.Contains("Some bold text.", RenderedMarkdown(window));
        Assert.DoesNotContain("**", RenderedMarkdown(window));
    }

    [WpfFact]
    public void EnterCodeMode_BeforeShowingWindow_PreservesTextAndDetectsLanguage()
    {
        const string code = "using System;\r\n\r\nnamespace Demo;\r\n\r\npublic class Greeter\r\n{\r\n\tpublic string Name { get; set; }\r\n}\r\n";
        EditTextWindow window = new();
        window.AddThisText(code);

        window.EnterCodeMode();
        LayOut(window);

        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal(code, window.Session.Text);
        Assert.Equal(code, CodeEditor(window).Text);
        Assert.Equal(Visibility.Visible, CodeEditor(window).Visibility);
        Assert.Equal(Visibility.Collapsed, RawTextBox(window).Visibility);
        Assert.Equal(CodeLanguages.CSharp, window.CodeLanguageName);
    }
}
