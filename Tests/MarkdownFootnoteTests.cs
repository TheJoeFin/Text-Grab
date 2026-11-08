using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using Text_Grab.Utilities;
using WpfList = System.Windows.Documents.List;

namespace Tests;

public class MarkdownFootnoteTests
{
    [WpfFact]
    public void ReferencesAreSuperscriptAndNotesFollowFirstUseOrder()
    {
        const string markdown = "Before[^second] then **bold[^first]** and again[^second].\n\n" +
            "[^first]: The first definition.\n\n[^second]: The second definition.\n\nEnding.";
        FlowDocument document = CreateDocument(markdown);
        Hyperlink[] references = GetReferences(document);
        Assert.Equal(new[] { "1", "2", "1" }, references.Select(GetText));
        Assert.All(references, reference =>
        {
            Assert.Null(reference.NavigateUri);
            Assert.Equal(12, reference.FontSize);
        });
        WpfList notes = Assert.IsType<WpfList>(document.Blocks.LastBlock);
        Assert.Equal(TextMarkerStyle.Decimal, notes.MarkerStyle);
        Assert.Equal(2, notes.ListItems.Count);
        Assert.StartsWith("1.\tThe second definition.", GetText(notes.ListItems.FirstListItem));
        Assert.StartsWith("2.\tThe first definition.", GetText(notes.ListItems.LastListItem));
        string text = MarkdownFlowDocumentUtilities.GetDocumentPlainText(document);
        Assert.DoesNotContain("[^", text);
        Assert.Equal(1, text.Split("Ending.", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("\u21a9", MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfFact]
    public void ReferencesAndReturnLinksNavigateLocallyFromQuotesListsAndTables()
    {
        const string markdown = "> Quote[^note].\n\n- List[^note].\n\n" +
            "| Header |\n| --- |\n| Table[^note] |\n\n[^note]: Note body.";
        FlowDocument document = CreateDocument(markdown);
        Hyperlink[] references = GetReferences(document);
        Assert.Equal(3, references.Length);
        ListItem note = Assert.Single(Assert.IsType<WpfList>(document.Blocks.LastBlock).ListItems);
        Hyperlink[] returns = Assert.IsType<Paragraph>(note.Blocks.LastBlock).Inlines.OfType<Hyperlink>().ToArray();
        Assert.Equal(new[] { " \u21a91", " \u21a92", " \u21a93" }, returns.Select(GetText));
        DependencyObject? target = null;
        int externalNavigationCount = 0;
        document.AddHandler(FrameworkElement.RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler((_, args) =>
        {
            target = args.TargetObject;
            args.Handled = true;
        }));
        document.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler((_, _) => externalNavigationCount++));

        for (int index = 0; index < references.Length; index++)
        {
            RoutedEventArgs click = new(Hyperlink.ClickEvent);
            references[index].RaiseEvent(click);
            Assert.True(click.Handled);
            Assert.Same(note, target);
            target = null;
            returns[index].RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
            Assert.Same(references[index], target);
            target = null;
            Assert.Null(returns[index].NavigateUri);
        }
        Assert.Equal(0, externalNavigationCount);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormattedDefinitionsPreserveEditsAndRoundTrip(bool preserveLiteralMarkdown)
    {
        const string markdown = "Body[^details].\n\n" +
            "[^details]: A **strong** note with `code` and [a link](https://example.com).\n\n" +
            "    Another paragraph.\n\n    - first item\n    - second **item**\n\n" +
            "    > Quoted text.\n\n    ```csharp\n    int value = 1;\n    ```\n\nEnding.";
        FlowDocument document = CreateDocument(markdown);
        ListItem note = Assert.Single(Assert.IsType<WpfList>(document.Blocks.LastBlock).ListItems);
        Paragraph first = Assert.IsType<Paragraph>(note.Blocks.FirstBlock);
        Bold bold = Assert.Single(first.Inlines.OfType<Bold>());
        Assert.IsType<Run>(Assert.Single(bold.Inlines)).Text = "updated";
        WpfList nested = Assert.Single(note.Blocks.OfType<WpfList>());
        Paragraph item = Assert.IsType<Paragraph>(nested.ListItems.FirstListItem.Blocks.FirstBlock);
        Assert.IsType<Run>(item.Inlines.FirstInline).Text = "edited item";
        Paragraph code = note.Blocks.OfType<Paragraph>().Single(paragraph => GetText(paragraph).Contains("int value = 1;", StringComparison.Ordinal));
        Assert.IsType<Run>(code.Inlines.FirstInline).Text = "int value = 2;";

        string saved = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document, preserveLiteralMarkdown);
        Assert.Contains("[^details]: A **updated** note with `code` and [a link](https://example.com).", saved);
        Assert.Contains("    Another paragraph.", saved);
        Assert.Contains("    - edited item", saved);
        Assert.Contains("    > Quoted text.", saved);
        Assert.Contains("    ```csharp", saved);
        Assert.Contains("    int value = 2;", saved);
        Assert.DoesNotContain("\u21a9", saved);
        FlowDocument reloaded = CreateDocument(saved);
        Assert.Equal(saved, MarkdownFlowDocumentUtilities.SerializeToMarkdown(reloaded, preserveLiteralMarkdown));
        Assert.Equal(MarkdownFlowDocumentUtilities.GetDocumentPlainText(document), MarkdownFlowDocumentUtilities.GetDocumentPlainText(reloaded));
    }

    [WpfTheory]
    [InlineData("- first\n    - second")]
    [InlineData("```text\n    code body\n    ```")]
    [InlineData("> quoted body")]
    [InlineData("| A | B |\n    | --- | --- |\n    | 1 | 2 |")]
    public void DefinitionsCanStartWithBlockContent(string content)
    {
        string markdown = "Body[^block].\n\n[^block]:\n    " + content;
        FlowDocument document = CreateDocument(markdown);
        WpfList notes = Assert.IsType<WpfList>(document.Blocks.LastBlock);
        Assert.Single(notes.ListItems);
        string saved = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);
        Assert.Contains("[^block]:", saved);
        Assert.DoesNotContain("\u21a9", saved);
        FlowDocument reloaded = CreateDocument(saved);
        Assert.Equal(saved, MarkdownFlowDocumentUtilities.SerializeToMarkdown(reloaded));
        Assert.Equal(MarkdownFlowDocumentUtilities.GetDocumentPlainText(document), MarkdownFlowDocumentUtilities.GetDocumentPlainText(reloaded));
    }

    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ThemeChangesKeepReferencesLegibleAndDoNotChangeMarkdown(bool isLightTheme)
    {
        FlowDocument document = CreateDocument("Body[^note].\n\n[^note]: A **formatted** footnote.");
        string saved = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);
        Border resources = new();
        resources.Resources["TextFillColorPrimaryBrush"] = isLightTheme ? Brushes.Black : Brushes.White;
        resources.Resources["ControlStrokeColorDefaultBrush"] = Brushes.Gray;
        resources.Resources["Teal"] = Brushes.Teal;
        document.FontSize = 24;
        MarkdownFlowDocumentUtilities.ApplyTheme(document, resources, isLightTheme);
        Hyperlink reference = Assert.Single(GetReferences(document));
        Assert.Equal(18, reference.FontSize);
        Assert.Same(Brushes.Teal, reference.Foreground);
        Assert.Equal(BaselineAlignment.Superscript, reference.BaselineAlignment);
        WpfList notes = Assert.IsType<WpfList>(document.Blocks.LastBlock);
        Assert.Same(Brushes.Gray, notes.BorderBrush);
        Paragraph body = Assert.IsType<Paragraph>(notes.ListItems.FirstListItem.Blocks.FirstBlock);
        Assert.Equal(24, body.FontSize);
        Assert.Same(resources.Resources["TextFillColorPrimaryBrush"], body.Foreground);
        Assert.Same(Brushes.Teal, Assert.Single(body.Inlines.OfType<Hyperlink>()).Foreground);
        Assert.Equal(saved, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfFact]
    public void UndefinedReferencesAndUnusedDefinitionsRemainLiteral()
    {
        const string markdown = "An unresolved[^missing].\n\n[^unused]: A **retained** definition.";
        FlowDocument document = CreateDocument(markdown);
        Assert.Empty(GetReferences(document));
        Assert.Empty(document.Blocks.OfType<WpfList>());
        Assert.Contains("An unresolved[^missing].", MarkdownFlowDocumentUtilities.GetDocumentPlainText(document));
        Assert.Equal(markdown.Replace("\n", Environment.NewLine, StringComparison.Ordinal),
            MarkdownFlowDocumentUtilities.SerializeToMarkdown(document, preserveLiteralMarkdown: true));
    }

    private static FlowDocument CreateDocument(string markdown) =>
        MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

    private static string GetText(TextElement element) => new TextRange(element.ContentStart, element.ContentEnd).Text;

    private static Hyperlink[] GetReferences(FlowDocument document)
    {
        List<Hyperlink> references = [];
        for (TextPointer? position = document.ContentStart; position is not null;
            position = position.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart &&
                position.GetAdjacentElement(LogicalDirection.Forward) is Hyperlink { BaselineAlignment: BaselineAlignment.Superscript } link)
                references.Add(link);
        }
        return references.ToArray();
    }
}
