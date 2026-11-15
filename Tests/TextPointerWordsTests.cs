using System.Windows.Documents;
using Text_Grab.Utilities;

namespace Tests;

public class TextPointerWordsTests
{
    [WpfFact]
    public void FindsTheWordAroundAPosition()
    {
        Run run = InParagraph(new Run("hello brave world"));

        Assert.Equal("brave", WordAt(run, 8));
    }

    [WpfFact]
    public void APositionAtEitherEdgeOfAWordFindsThatWord()
    {
        Run run = InParagraph(new Run("hello brave world"));

        Assert.Equal("brave", WordAt(run, 6));
        Assert.Equal("brave", WordAt(run, 11));
        Assert.Equal("hello", WordAt(run, 0));
        Assert.Equal("world", WordAt(run, 17));
    }

    [WpfFact]
    public void WhitespaceAndPunctuationAreNotWords()
    {
        Run run = InParagraph(new Run("one  -- two"));

        Assert.Null(WordAt(run, 4));
        Assert.Null(WordAt(run, 6));
    }

    [WpfFact]
    public void AWordCarriesOnThroughFormatting()
    {
        Run plain = new("ba");
        Bold bold = new(new Run("nana"));
        InParagraph(plain, bold, new Run(" split"));

        Assert.Equal("banana", WordAt(plain, 1));
        Assert.Equal("banana", Text(TextPointerWords.GetWordRange(((Run)bold.Inlines.FirstInline).ContentStart.GetPositionAtOffset(3)!)));
    }

    [WpfFact]
    public void ApostrophesInsideAWordBelongToIt()
    {
        Run run = InParagraph(new Run("don't stop the dogs' toys"));

        Assert.Equal("don't", WordAt(run, 1));
        Assert.Equal("don't", WordAt(run, 4));
        Assert.Equal("dogs", WordAt(run, 16));
    }

    [WpfFact]
    public void TypographicApostrophesCountToo()
    {
        Run run = InParagraph(new Run("it’s"));

        Assert.Equal("it’s", WordAt(run, 1));
    }

    [WpfFact]
    public void ALineBreakEndsAWord()
    {
        Run first = new("one");
        Run second = new("two");
        InParagraph(first, new LineBreak(), second);

        Assert.Equal("one", WordAt(first, 3));
        Assert.Equal("two", WordAt(second, 0));
    }

    [WpfFact]
    public void AParagraphEndsAWord()
    {
        Run first = new("end");
        Run second = new("start");
        FlowDocument document = new();
        document.Blocks.Add(new Paragraph(first));
        document.Blocks.Add(new Paragraph(second));

        Assert.Equal("end", WordAt(first, 3));
        Assert.Equal("start", WordAt(second, 0));
    }

    [WpfFact]
    public void DigitsAndUnderscoresArePartOfAWord()
    {
        Run run = InParagraph(new Run("see banana1 and snake_case."));

        Assert.Equal("banana1", WordAt(run, 6));
        Assert.Equal("snake_case", WordAt(run, 20));
    }

    [WpfTheory]
    [InlineData("A\U00010400B")]
    [InlineData("\U00020BB7\u91ce\u5bb6")]
    [InlineData("x\U0001D165y")]
    [InlineData("item\U0001D7CE")]
    public void SupplementaryLettersMarksAndDigitsBelongToTheWord(string word)
    {
        Run run = InParagraph(new Run(word));

        Assert.Equal(word, WordAt(run, 0));
        Assert.Equal(word, WordAt(run, word.Length));
    }

    [WpfFact]
    public void SupplementaryLettersJoinApostrophesAndSpanFormatting()
    {
        Run first = new("A\uD801");
        Run second = new("\uDC00'B");
        InParagraph(first, new Bold(second));

        Assert.Equal("A\U00010400'B", WordAt(first, 1));
        Assert.Equal("A\U00010400'B", WordAt(second, 3));
    }

    [WpfFact]
    public void SupplementarySymbolsAndUnpairedSurrogatesDoNotJoinWords()
    {
        Run run = InParagraph(new Run("one\U0001F600two \uD801 \uDC00"));

        Assert.Equal("one", WordAt(run, 1));
        Assert.Equal("two", WordAt(run, 6));
        Assert.Null(WordAt(run, 10));
        Assert.Null(WordAt(run, 12));
    }

    private static Run InParagraph(params Inline[] inlines)
    {
        Paragraph paragraph = new();
        paragraph.Inlines.AddRange(inlines);
        new FlowDocument().Blocks.Add(paragraph);
        return inlines.OfType<Run>().First();
    }

    private static string? WordAt(Run run, int offset) =>
        Text(TextPointerWords.GetWordRange(run.ContentStart.GetPositionAtOffset(offset)!));

    private static string? Text((TextPointer Start, TextPointer End)? range) =>
        range is (TextPointer start, TextPointer end) ? new TextRange(start, end).Text : null;
}
