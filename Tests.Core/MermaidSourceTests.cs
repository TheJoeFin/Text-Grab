using Text_Grab.Core.Editing;
using Text_Grab.Models;

namespace Text_Grab.Tests.Core;

/// <summary>What the Mermaid pane draws for a document (v5 E5 <c>mermaid</c>).</summary>
public class MermaidSourceTests
{
    private const string Document = "# Title\n\n```mermaid\nflowchart LR\nA --> B\n```\n\nText between.\n\n~~~MERMAID\nsequenceDiagram\nAlice->>Bob: Hi\n~~~\n\nThe end";

    // ---- Every mode but Markdown draws the whole document ----

    [Theory]
    [InlineData(EtwEditorMode.Text)]
    [InlineData(EtwEditorMode.Code)]
    [InlineData(EtwEditorMode.Spreadsheet)]
    public void OutsideMarkdown_TheWholeDocumentIsTheDiagram(EtwEditorMode mode)
        => Assert.Equal("flowchart LR\nA --> B", MermaidSource.ForDocument("flowchart LR\nA --> B", mode, caretOffset: 3));

    [Fact]
    public void OutsideMarkdown_AnEmptyDocumentIsEmpty()
        => Assert.Equal(string.Empty, MermaidSource.ForDocument(string.Empty, EtwEditorMode.Code, 0));

    // ---- Markdown draws the block under the caret ----

    [Theory]
    [InlineData("```mermaid", "flowchart LR\nA --> B")]
    [InlineData("flowchart LR", "flowchart LR\nA --> B")]
    [InlineData("A --> B\n```", "flowchart LR\nA --> B")]
    [InlineData("Alice->>Bob", "sequenceDiagram\nAlice->>Bob: Hi")]
    [InlineData("~~~MERMAID", "sequenceDiagram\nAlice->>Bob: Hi")]
    public void InMarkdown_TheBlockContainingTheCaretIsTheDiagram(string caretAt, string expected)
    {
        int caret = Document.IndexOf(caretAt, StringComparison.Ordinal) + 1;
        Assert.Equal(expected, MermaidSource.ForDocument(Document, EtwEditorMode.Markdown, caret));
    }

    [Theory]
    [InlineData("# Title")]
    [InlineData("Text between.")]
    [InlineData("The end")]
    public void InMarkdown_ACaretOutsideEveryBlockHasNoDiagram(string caretAt)
    {
        int caret = Document.IndexOf(caretAt, StringComparison.Ordinal) + 2;
        Assert.Null(MermaidSource.ForDocument(Document, EtwEditorMode.Markdown, caret));
    }

    [Fact]
    public void TheBlocksSpanCoversItsFencesAndNothingElse()
    {
        MermaidBlock? block = MermaidSource.FindBlockAt(Document, Document.IndexOf("A --> B", StringComparison.Ordinal));

        Assert.NotNull(block);
        Assert.Equal("```mermaid\nflowchart LR\nA --> B\n```", Document.Substring(block.Block.Start, block.Block.Length));
    }

    [Fact]
    public void ACaretAtTheStartAndAtTheEndOfTheBlockIsInIt()
    {
        MermaidBlock block = MermaidSource.FindBlockAt(Document, Document.IndexOf("```mermaid", StringComparison.Ordinal))!;

        Assert.NotNull(MermaidSource.FindBlockAt(Document, block.Block.Start));
        Assert.NotNull(MermaidSource.FindBlockAt(Document, block.Block.End));
        Assert.Null(MermaidSource.FindBlockAt(Document, block.Block.End + 1));
    }

    // ---- What counts as a Mermaid fence ----

    [Theory]
    [InlineData("```csharp\nA --> B\n```")]
    [InlineData("```mermaid-example\nA --> B\n```")]
    [InlineData("``mermaid\nA --> B\n``")]
    [InlineData("    plain text")]
    public void OnlyAMermaidFenceOfThreeOrMoreMarkersIsABlock(string markdown)
        => Assert.Null(MermaidSource.FindBlockAt(markdown, 5));

    [Theory]
    [InlineData("> ```mermaid\n> graph TD\n> A --> B\n> ```", "graph TD\nA --> B", "graph TD")]
    [InlineData("- ```mermaid\n  classDiagram\n  Animal <|-- Duck\n  ```", "classDiagram\nAnimal <|-- Duck", "Animal")]
    [InlineData("```Mermaid {title=\"x\"}\ngraph TD\nA --> B\n```", "graph TD\nA --> B", "graph TD")]
    public void FencesInQuotesAndListsAndWithAnInfoStringAreBlocks(string markdown, string expected, string caretAt)
        => Assert.Equal(expected, MermaidSource.ForDocument(markdown, EtwEditorMode.Markdown, markdown.IndexOf(caretAt, StringComparison.Ordinal) + 1));

    [Fact]
    public void ALongerFenceIsClosedOnlyByAtLeastAsManyMarkers()
    {
        const string markdown = "````mermaid\nA --> B\n```\nC --> D\n````\nafter";

        Assert.Equal("A --> B\n```\nC --> D", MermaidSource.ForDocument(markdown, EtwEditorMode.Markdown, 14));
    }

    [Fact]
    public void ABlockNeverClosedRunsToTheEndOfTheText()
    {
        const string markdown = "intro\n```mermaid\nA --> B\nB --> C";

        Assert.Equal("A --> B\nB --> C", MermaidSource.ForDocument(markdown, EtwEditorMode.Markdown, markdown.Length));
        Assert.Null(MermaidSource.ForDocument(markdown, EtwEditorMode.Markdown, 2));
    }

    [Fact]
    public void WindowsLineEndingsAreReadAndTheSourceIsJoinedWithNewlines()
    {
        const string markdown = "```mermaid\r\nA --> B\r\n\r\nB --> C\r\n```\r\nafter";

        Assert.Equal("A --> B\n\nB --> C", MermaidSource.ForDocument(markdown, EtwEditorMode.Markdown, 14));
    }

    [Fact]
    public void FindBlocks_ListsEveryBlockInOrder_AndFindFirstBlockTakesTheFirst()
    {
        IReadOnlyList<MermaidBlock> blocks = MermaidSource.FindBlocks(Document);

        Assert.Equal(["flowchart LR\nA --> B", "sequenceDiagram\nAlice->>Bob: Hi"], blocks.Select(block => block.Source));
        Assert.Equal(blocks[0], MermaidSource.FindFirstBlock(Document));
        Assert.Empty(MermaidSource.FindBlocks("no fences at all"));
        Assert.Null(MermaidSource.FindFirstBlock("no fences at all"));
        Assert.Null(MermaidSource.FindFirstBlock(string.Empty));
    }

    [Fact]
    public void AnEmptyOrCaretlessDocumentHasNoBlock()
    {
        Assert.Null(MermaidSource.FindBlockAt(string.Empty, 0));
        Assert.Null(MermaidSource.FindBlockAt("no fences at all", -5));
        Assert.Null(MermaidSource.FindBlockAt("no fences at all", 500));
    }
}
