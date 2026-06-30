using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

public class PatternExecutorTests
{
    // A deterministic saved-regex item that does not depend on the machine's saved patterns.
    private static PatternItem SavedEmail() =>
        new(new StoredRegex("Email Address", @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", true));

    private static PatternItem RecognizerByName(string name) =>
        new(BuiltInRecognizer.GetByName(name) ?? throw new InvalidOperationException($"missing recognizer {name}"));

    // ── PatternItem catalog ───────────────────────────────────────────────────

    [Fact]
    public void GetAll_ListsSavedRegexesBeforeRecognizers()
    {
        IReadOnlyList<PatternItem> all = PatternItem.GetAll();

        int firstRecognizer = -1;
        int lastSaved = -1;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Kind == PatternKind.Recognizer && firstRecognizer < 0)
                firstRecognizer = i;
            if (all[i].Kind == PatternKind.SavedRegex)
                lastSaved = i;
        }

        Assert.True(firstRecognizer >= 0, "expected at least one recognizer item");
        Assert.True(lastSaved < firstRecognizer, "all saved regexes should precede recognizers");
    }

    [Fact]
    public void GetAll_IncludesEveryRecognizerWithSmartGroup()
    {
        List<PatternItem> recognizers = [.. PatternItem.GetAll().Where(p => p.Kind == PatternKind.Recognizer)];

        Assert.Equal(BuiltInRecognizer.GetAll().Count, recognizers.Count);
        Assert.All(recognizers, p => Assert.Equal(PatternItem.SmartGroup, p.GroupLabel));
    }

    [Fact]
    public void GetByName_FindsRecognizer_CaseInsensitive()
    {
        PatternItem? email = PatternItem.GetByName("EMAIL");

        Assert.NotNull(email);
        Assert.Equal(PatternKind.Recognizer, email!.Kind);
    }

    // ── PatternExecutor – recognizer-backed ───────────────────────────────────

    [Fact]
    public void HasMatch_Recognizer_DetectsEntity()
    {
        Assert.True(PatternExecutor.HasMatch(RecognizerByName("Email"), "reach me at a@b.com"));
        Assert.False(PatternExecutor.HasMatch(RecognizerByName("Email"), "no address here"));
    }

    [Fact]
    public void Apply_Recognizer_NormalizesCurrencyResolvedValue()
    {
        string result = PatternExecutor.Apply(
            RecognizerByName("Currency"), "it costs $5", "first", ", ", RecognizerOutputKind.ResolvedValue);
        Assert.Equal("5 Dollar", result);
    }

    [Fact]
    public void Apply_Recognizer_MatchedText_KeepsOriginalSpan()
    {
        string result = PatternExecutor.Apply(
            RecognizerByName("Currency"), "it costs $5", "first", ", ", RecognizerOutputKind.MatchedText);
        Assert.Equal("$5", result);
    }

    // ── PatternExecutor – saved-regex-backed ──────────────────────────────────

    [Fact]
    public void GetMatches_SavedRegex_ReportsSpanAndMatchedText()
    {
        RecognizerMatch match = PatternExecutor.GetMatches(SavedEmail(), "write to a@b.com please")[0];

        Assert.Equal("a@b.com", match.Text);
        Assert.Equal("a@b.com", match.ResolvedValue); // regex has no resolution
        Assert.Equal(9, match.Start);
        Assert.Equal("a@b.com".Length, match.Length);
    }

    [Fact]
    public void HasMatch_SavedRegex_TrueWhenPresent()
    {
        Assert.True(PatternExecutor.HasMatch(SavedEmail(), "x a@b.com y"));
        Assert.False(PatternExecutor.HasMatch(SavedEmail(), "nothing here"));
    }

    [Fact]
    public void Apply_SavedRegex_All_JoinsMatchedTextWithSeparator()
    {
        string result = PatternExecutor.Apply(SavedEmail(), "a@b.com and c@d.org", "all");
        Assert.Equal("a@b.com, c@d.org", result);
    }

    [Fact]
    public void Apply_SavedRegex_RespectsModeAndSeparator()
    {
        PatternItem email = SavedEmail();
        const string text = "a@b.com and c@d.org";

        Assert.Equal("a@b.com", PatternExecutor.Apply(email, text, "first"));
        Assert.Equal("c@d.org", PatternExecutor.Apply(email, text, "last"));
        Assert.Equal("c@d.org", PatternExecutor.Apply(email, text, "2"));
        Assert.Equal("a@b.com | c@d.org", PatternExecutor.Apply(email, text, "all", " | "));
    }

    [Fact]
    public void Apply_SavedRegex_NoMatch_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PatternExecutor.Apply(SavedEmail(), "no emails here", "all"));
    }

    [Fact]
    public void GetMatches_InvalidRegex_ReturnsEmpty_DoesNotThrow()
    {
        PatternItem bad = new(new StoredRegex("Bad", "([unclosed", false));
        Assert.Empty(PatternExecutor.GetMatches(bad, "anything"));
    }

    [Fact]
    public void GetMatches_EmptyText_ReturnsEmpty()
    {
        Assert.Empty(PatternExecutor.GetMatches(SavedEmail(), string.Empty));
        Assert.Empty(PatternExecutor.GetMatches(RecognizerByName("Number"), string.Empty));
    }
}
