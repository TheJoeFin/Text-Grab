using Microsoft.Recognizers.Text;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

// Pure half of the original Tests/RecognizerExecutorTests.cs (batch 7a): RecognizerExecutor and
// BuiltInRecognizer are Core-only. The GrabTemplateExecutor-backed tests (recognizer placeholders
// and parsing) moved into the existing Tests/GrabTemplateExecutorTests.cs instead of a new class -
// GrabTemplateExecutor needs System.Windows.Rect and stays app-side, and that file already covers
// it comprehensively.
public class RecognizerExecutorTests
{
    private static BuiltInRecognizer Get(string id) =>
        BuiltInRecognizer.GetById(id) ?? throw new InvalidOperationException($"missing recognizer {id}");

    private static ModelResult ResultWith(string text, params (string Key, object Value)[] resolution)
    {
        SortedDictionary<string, object> map = new();
        foreach ((string key, object value) in resolution)
            map[key] = value;

        return new ModelResult { Text = text, Start = 0, End = text.Length - 1, Resolution = map };
    }

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

    // ── FormatResolvedValue – resolution shapes (guards library coupling) ─────

    [Fact]
    public void FormatResolvedValue_ValuesAsStringDictionaries_ReadsValue()
    {
        // The current Recognizers-Text shape: "values" is a list of string→string dictionaries.
        ModelResult result = ResultWith("on 2026-01-15",
            ("values", new List<Dictionary<string, string>> { new() { ["value"] = "2026-01-15" } }));

        Assert.Equal("2026-01-15", RecognizerExecutor.FormatResolvedValue(result));
    }

    [Fact]
    public void FormatResolvedValue_ValuesAsObjectDictionaries_StillReadsValue()
    {
        // A hypothetical future shape: "values" holds string→object dictionaries. This must keep
        // resolving instead of silently falling back to the matched text (issue: type coupling).
        ModelResult result = ResultWith("next tuesday",
            ("values", new List<Dictionary<string, object>> { new() { ["value"] = "2026-01-20" } }));

        Assert.Equal("2026-01-20", RecognizerExecutor.FormatResolvedValue(result));
    }

    [Fact]
    public void FormatResolvedValue_ValuesWithStartAndEnd_FormatsRange()
    {
        ModelResult result = ResultWith("this week",
            ("values", new List<Dictionary<string, string>>
            {
                new() { ["start"] = "2026-01-01", ["end"] = "2026-01-05" }
            }));

        Assert.Equal("2026-01-01 → 2026-01-05", RecognizerExecutor.FormatResolvedValue(result));
    }

    [Fact]
    public void FormatResolvedValue_ValuesWithOnlyTimex_FallsBackToTimex()
    {
        ModelResult result = ResultWith("every monday",
            ("values", new List<Dictionary<string, string>> { new() { ["timex"] = "XXXX-WXX-1" } }));

        Assert.Equal("XXXX-WXX-1", RecognizerExecutor.FormatResolvedValue(result));
    }

    [Fact]
    public void FormatResolvedValue_NotResolvedValue_FallsBackToText()
    {
        ModelResult result = ResultWith("someday",
            ("values", new List<Dictionary<string, string>> { new() { ["value"] = "not resolved" } }));

        Assert.Equal("someday", RecognizerExecutor.FormatResolvedValue(result));
    }

    [Fact]
    public void FormatResolvedValue_ValueAndUnit_JoinsWithSpace()
    {
        ModelResult result = ResultWith("5 dollars", ("value", "5"), ("unit", "Dollar"));

        Assert.Equal("5 Dollar", RecognizerExecutor.FormatResolvedValue(result));
    }

    [Fact]
    public void FormatResolvedValue_EmptyResolution_ReturnsText()
    {
        Assert.Equal("plain text", RecognizerExecutor.FormatResolvedValue(ResultWith("plain text")));
    }

    // ── DateTime recognizer – real "values" path (pins the live library shape) ─

    [Fact]
    public void GetMatches_DateTime_ResolvesAbsoluteDate()
    {
        RecognizerMatch match = RecognizerExecutor.GetMatches(Get("datetime"), "meeting on 2026-01-15")[0];

        // Resolution must produce the normalized date, distinct from the matched span.
        Assert.Equal("2026-01-15", match.ResolvedValue);
    }

    [Fact]
    public void GetMatches_DateTime_ResolvesDateRange()
    {
        RecognizerMatch match =
            RecognizerExecutor.GetMatches(Get("datetime"), "from 2026-01-01 to 2026-01-05")[0];

        Assert.Equal("2026-01-01 → 2026-01-05", match.ResolvedValue);
    }
}
