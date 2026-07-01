using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

public class RecognizerExecutorTests
{
    private static BuiltInRecognizer Get(string id) =>
        BuiltInRecognizer.GetById(id) ?? throw new InvalidOperationException($"missing recognizer {id}");

    // ── BuiltInRecognizer catalog ─────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsFullCatalog()
    {
        Assert.Equal(14, BuiltInRecognizer.GetAll().Count);
    }

    [Fact]
    public void GetById_And_GetByName_AreCaseInsensitive()
    {
        Assert.NotNull(BuiltInRecognizer.GetById("NUMBER"));
        Assert.NotNull(BuiltInRecognizer.GetByName("date / time"));
        Assert.Null(BuiltInRecognizer.GetById("does-not-exist"));
        Assert.Null(BuiltInRecognizer.GetByName("does-not-exist"));
    }

    // ── GetMatches / HasMatch ─────────────────────────────────────────────────

    [Fact]
    public void GetMatches_Number_FindsAllNumbersWithResolvedValues()
    {
        IReadOnlyList<RecognizerMatch> matches =
            RecognizerExecutor.GetMatches(Get("number"), "I have 25 apples and 3.5 kg");

        Assert.Equal(2, matches.Count);
        Assert.Equal("25", matches[0].Text);
        Assert.Equal("25", matches[0].ResolvedValue);
        Assert.Equal("3.5", matches[1].ResolvedValue);
    }

    [Fact]
    public void GetMatches_ReportsCorrectSpan()
    {
        RecognizerMatch match = RecognizerExecutor.GetMatches(Get("email"), "write to a@b.com please")[0];

        Assert.Equal("a@b.com", match.Text);
        Assert.Equal(9, match.Start);
        Assert.Equal("a@b.com".Length, match.Length);
    }

    [Fact]
    public void HasMatch_TrueWhenEntityPresent_FalseOtherwise()
    {
        Assert.True(RecognizerExecutor.HasMatch(Get("email"), "reach me at a@b.com"));
        Assert.False(RecognizerExecutor.HasMatch(Get("email"), "no address here"));
    }

    [Fact]
    public void GetMatches_EmptyText_ReturnsEmpty()
    {
        Assert.Empty(RecognizerExecutor.GetMatches(Get("number"), string.Empty));
    }

    // ── ApplyRecognizer – modes ───────────────────────────────────────────────

    [Fact]
    public void ApplyRecognizer_All_JoinsWithSeparator()
    {
        string result = RecognizerExecutor.ApplyRecognizer(Get("number"), "25 and 3.5", "all");
        Assert.Equal("25, 3.5", result);
    }

    [Fact]
    public void ApplyRecognizer_First_ReturnsFirst()
    {
        Assert.Equal("25", RecognizerExecutor.ApplyRecognizer(Get("number"), "25 and 3.5", "first"));
    }

    [Fact]
    public void ApplyRecognizer_Last_ReturnsLast()
    {
        Assert.Equal("3.5", RecognizerExecutor.ApplyRecognizer(Get("number"), "25 and 3.5", "last"));
    }

    [Fact]
    public void ApplyRecognizer_NthIndex_ReturnsThatMatch()
    {
        Assert.Equal("3.5", RecognizerExecutor.ApplyRecognizer(Get("number"), "25 and 3.5", "2"));
    }

    [Fact]
    public void ApplyRecognizer_CustomSeparator_IsUsed()
    {
        string result = RecognizerExecutor.ApplyRecognizer(Get("number"), "1 and 2", "all", " | ");
        Assert.Equal("1 | 2", result);
    }

    [Fact]
    public void ApplyRecognizer_NoMatch_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, RecognizerExecutor.ApplyRecognizer(Get("number"), "no numbers here", "all"));
    }

    // ── ApplyRecognizer – output kind ─────────────────────────────────────────

    [Fact]
    public void ApplyRecognizer_ResolvedValue_NormalizesCurrency()
    {
        string result = RecognizerExecutor.ApplyRecognizer(
            Get("currency"), "it costs $5", "first", ", ", RecognizerOutputKind.ResolvedValue);
        Assert.Equal("5 Dollar", result);
    }

    [Fact]
    public void ApplyRecognizer_MatchedText_KeepsOriginalSpan()
    {
        string result = RecognizerExecutor.ApplyRecognizer(
            Get("currency"), "it costs $5", "first", ", ", RecognizerOutputKind.MatchedText);
        Assert.Equal("$5", result);
    }

    // ── GrabTemplateExecutor – recognizer placeholders ────────────────────────

    [Fact]
    public void ApplyRecognizerPlaceholders_AllMatches_Substitutes()
    {
        string result = GrabTemplateExecutor.ApplyRecognizerPlaceholders("Found {r:Number:all}", "1 2 3");
        Assert.Equal("Found 1, 2, 3", result);
    }

    [Fact]
    public void ApplyRecognizerPlaceholders_TextOutput_UsesMatchedText()
    {
        string result = GrabTemplateExecutor.ApplyRecognizerPlaceholders("{r:Currency:first:text}", "it costs $5");
        Assert.Equal("$5", result);
    }

    [Fact]
    public void ApplyRecognizerPlaceholders_UnknownRecognizer_LeavesPlaceholder()
    {
        string result = GrabTemplateExecutor.ApplyRecognizerPlaceholders("{r:Nope:first}", "anything 5");
        Assert.Equal("{r:Nope:first}", result);
    }

    [Fact]
    public void ApplyRecognizerPlaceholders_LeavesPatternPlaceholdersUntouched()
    {
        // Recognizer pass must only resolve {r:...}, never {p:...}
        string result = GrabTemplateExecutor.ApplyRecognizerPlaceholders(
            "{p:Email:first} {r:Number:first}", "value 5");
        Assert.Equal("{p:Email:first} 5", result);
    }

    // ── GrabTemplateExecutor – parsing ────────────────────────────────────────

    [Fact]
    public void ParseRecognizerMatches_ExtractsModeAndOutputKind()
    {
        List<TemplateRecognizerMatch> matches =
            GrabTemplateExecutor.ParseRecognizerMatchesFromOutputTemplate("{r:Number:all:text}");

        TemplateRecognizerMatch match = Assert.Single(matches);
        Assert.Equal("Number", match.RecognizerName);
        Assert.Equal("all", match.MatchMode);
        Assert.Equal(RecognizerOutputKind.MatchedText, match.OutputKind);
        Assert.Equal(Get("number").Id, match.RecognizerId);
    }

    [Fact]
    public void ParseRecognizerMatches_WithSeparator_ParsesValueOutputAndSeparator()
    {
        List<TemplateRecognizerMatch> matches =
            GrabTemplateExecutor.ParseRecognizerMatchesFromOutputTemplate("{r:Number:all:value:; }");

        TemplateRecognizerMatch match = Assert.Single(matches);
        Assert.Equal("all", match.MatchMode);
        Assert.Equal("; ", match.Separator);
        Assert.Equal(RecognizerOutputKind.ResolvedValue, match.OutputKind);
    }

    // ── ApplyTextOnlyTemplate – recognizer-only ───────────────────────────────

    [Fact]
    public void ApplyTextOnlyTemplate_RecognizerPlaceholder_Resolves()
    {
        GrabTemplate template = new("Numbers")
        {
            OutputTemplate = "Numbers: {r:Number:all}"
        };

        string result = GrabTemplateExecutor.ApplyTextOnlyTemplate(template, "got 1 and 2");
        Assert.Equal("Numbers: 1, 2", result);
    }
}
