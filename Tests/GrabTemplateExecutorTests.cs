using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

public class GrabTemplateExecutorTests
{
    // ── ApplyOutputTemplate – basic substitution ──────────────────────────────

    [Fact]
    public void ApplyOutputTemplate_SingleRegion_SubstitutesCorrectly()
    {
        Dictionary<int, string> regions = new() { [1] = "Alice" };
        string result = GrabTemplateExecutor.ApplyOutputTemplate("Name: {1}", regions);
        Assert.Equal("Name: Alice", result);
    }

    [Fact]
    public void ApplyOutputTemplate_MultipleRegions_SubstitutesAll()
    {
        Dictionary<int, string> regions = new()
        {
            [1] = "Alice",
            [2] = "alice@example.com"
        };
        string result = GrabTemplateExecutor.ApplyOutputTemplate("{1} <{2}>", regions);
        Assert.Equal("Alice <alice@example.com>", result);
    }

    [Fact]
    public void ApplyOutputTemplate_MissingRegion_ReplacesWithEmpty()
    {
        Dictionary<int, string> regions = new() { [1] = "Alice" };
        // Region 2 not present
        string result = GrabTemplateExecutor.ApplyOutputTemplate("{1} {2}", regions);
        Assert.Equal("Alice ", result);
    }

    [Fact]
    public void ApplyOutputTemplate_EmptyTemplate_ReturnsEmpty()
    {
        Dictionary<int, string> regions = new() { [1] = "value" };
        string result = GrabTemplateExecutor.ApplyOutputTemplate(string.Empty, regions);
        Assert.Equal(string.Empty, result);
    }

    // ── Modifiers ──────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyOutputTemplate_TrimModifier_TrimsWhitespace()
    {
        Dictionary<int, string> regions = new() { [1] = "  hello  " };
        string result = GrabTemplateExecutor.ApplyOutputTemplate("{1:trim}", regions);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ApplyOutputTemplate_UpperModifier_ConvertsToUpper()
    {
        Dictionary<int, string> regions = new() { [1] = "hello" };
        string result = GrabTemplateExecutor.ApplyOutputTemplate("{1:upper}", regions);
        Assert.Equal("HELLO", result);
    }

    [Fact]
    public void ApplyOutputTemplate_LowerModifier_ConvertsToLower()
    {
        Dictionary<int, string> regions = new() { [1] = "HELLO" };
        string result = GrabTemplateExecutor.ApplyOutputTemplate("{1:lower}", regions);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ApplyOutputTemplate_UnknownModifier_LeavesTextAsIs()
    {
        Dictionary<int, string> regions = new() { [1] = "hello" };
        string result = GrabTemplateExecutor.ApplyOutputTemplate("{1:unknown}", regions);
        Assert.Equal("hello", result);
    }

    // ── Escape sequences ──────────────────────────────────────────────────────

    [Fact]
    public void ApplyOutputTemplate_NewlineEscape_InsertsNewline()
    {
        Dictionary<int, string> regions = new() { [1] = "A", [2] = "B" };
        string result = GrabTemplateExecutor.ApplyOutputTemplate("{1}\\n{2}", regions);
        Assert.Equal("A\nB", result);
    }

    [Fact]
    public void ApplyOutputTemplate_TabEscape_InsertsTab()
    {
        Dictionary<int, string> regions = new() { [1] = "A", [2] = "B" };
        string result = GrabTemplateExecutor.ApplyOutputTemplate("{1}\\t{2}", regions);
        Assert.Equal("A\tB", result);
    }

    [Fact]
    public void ApplyOutputTemplate_LiteralBraceEscape_PreservesBrace()
    {
        Dictionary<int, string> regions = new() { [1] = "value" };
        // \{ in template produces literal {, then {1} → value, then literal text }
        string result = GrabTemplateExecutor.ApplyOutputTemplate("\\{{1}}", regions);
        Assert.Equal("{value}", result);
    }

    [Fact]
    public void ApplyOutputTemplate_DoubleBackslash_PreservesBackslash()
    {
        Dictionary<int, string> regions = new() { [1] = "A" };
        string result = GrabTemplateExecutor.ApplyOutputTemplate("{1}\\\\{1}", regions);
        Assert.Equal(@"A\A", result);
    }

    // ── ValidateOutputTemplate ────────────────────────────────────────────────

    [Fact]
    public void ValidateOutputTemplate_ValidTemplate_ReturnsNoIssues()
    {
        List<string> issues = GrabTemplateExecutor.ValidateOutputTemplate("{1} {2}", [1, 2]);
        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateOutputTemplate_OutOfRangeRegion_ReturnsIssue()
    {
        List<string> issues = GrabTemplateExecutor.ValidateOutputTemplate("{3}", [1, 2]);
        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Contains('3'));
    }

    [Fact]
    public void ValidateOutputTemplate_EmptyTemplate_ReturnsIssue()
    {
        List<string> issues = GrabTemplateExecutor.ValidateOutputTemplate(string.Empty, [1]);
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void ValidateOutputTemplate_NoRegionsReferenced_ReturnsIssue()
    {
        // Template has no {N} references
        List<string> issues = GrabTemplateExecutor.ValidateOutputTemplate("static text", [1, 2]);
        Assert.NotEmpty(issues);
    }

    // ── Pattern placeholder – ApplyPatternPlaceholders ────────────────────────

    [Fact]
    public void ApplyPatternPlaceholders_FirstMatch_ReturnsFirstOccurrence()
    {
        string fullText = "Contact: alice@test.com and bob@test.com";
        List<TemplatePatternMatch> patterns =
        [
            new("id1", "Email", "first")
        ];
        Dictionary<string, string> regexes = new()
        {
            ["id1"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
            ["Email"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"
        };

        string result = GrabTemplateExecutor.ApplyPatternPlaceholders(
            "Email: {p:Email:first}", fullText, patterns, regexes);

        Assert.Equal("Email: alice@test.com", result);
    }

    [Fact]
    public void ApplyPatternPlaceholders_LastMatch_ReturnsLastOccurrence()
    {
        string fullText = "Contact: alice@test.com and bob@test.com";
        List<TemplatePatternMatch> patterns =
        [
            new("id1", "Email", "last")
        ];
        Dictionary<string, string> regexes = new()
        {
            ["id1"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
            ["Email"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"
        };

        string result = GrabTemplateExecutor.ApplyPatternPlaceholders(
            "Email: {p:Email:last}", fullText, patterns, regexes);

        Assert.Equal("Email: bob@test.com", result);
    }

    [Fact]
    public void ApplyPatternPlaceholders_AllMatches_JoinsWithDefaultSeparator()
    {
        string fullText = "Contact: alice@test.com and bob@test.com";
        List<TemplatePatternMatch> patterns =
        [
            new("id1", "Email", "all", ", ")
        ];
        Dictionary<string, string> regexes = new()
        {
            ["id1"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
            ["Email"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"
        };

        string result = GrabTemplateExecutor.ApplyPatternPlaceholders(
            "Emails: {p:Email:all}", fullText, patterns, regexes);

        Assert.Equal("Emails: alice@test.com, bob@test.com", result);
    }

    [Fact]
    public void ApplyPatternPlaceholders_AllMatchesCustomSeparator_UsesOverride()
    {
        string fullText = "Contact: alice@test.com and bob@test.com";
        List<TemplatePatternMatch> patterns =
        [
            new("id1", "Email", "all", ", ")
        ];
        Dictionary<string, string> regexes = new()
        {
            ["id1"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
            ["Email"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"
        };

        string result = GrabTemplateExecutor.ApplyPatternPlaceholders(
            "Emails: {p:Email:all: | }", fullText, patterns, regexes);

        Assert.Equal("Emails: alice@test.com | bob@test.com", result);
    }

    [Fact]
    public void ApplyPatternPlaceholders_NthMatch_ReturnsSingleIndex()
    {
        string fullText = "Numbers: 100 200 300";
        List<TemplatePatternMatch> patterns =
        [
            new("id1", "Integer", "2")
        ];
        Dictionary<string, string> regexes = new()
        {
            ["id1"] = @"\d+",
            ["Integer"] = @"\d+"
        };

        string result = GrabTemplateExecutor.ApplyPatternPlaceholders(
            "Second: {p:Integer:2}", fullText, patterns, regexes);

        Assert.Equal("Second: 200", result);
    }

    [Fact]
    public void ApplyPatternPlaceholders_MultipleIndices_JoinsSelected()
    {
        string fullText = "Numbers: 100 200 300 400";
        List<TemplatePatternMatch> patterns =
        [
            new("id1", "Integer", "1,3", "; ")
        ];
        Dictionary<string, string> regexes = new()
        {
            ["id1"] = @"\d+",
            ["Integer"] = @"\d+"
        };

        string result = GrabTemplateExecutor.ApplyPatternPlaceholders(
            "Selected: {p:Integer:1,3}", fullText, patterns, regexes);

        Assert.Equal("Selected: 100; 300", result);
    }

    [Fact]
    public void ApplyPatternPlaceholders_NoMatches_ReturnsEmpty()
    {
        string fullText = "No emails here";
        List<TemplatePatternMatch> patterns =
        [
            new("id1", "Email", "first")
        ];
        Dictionary<string, string> regexes = new()
        {
            ["id1"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
            ["Email"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"
        };

        string result = GrabTemplateExecutor.ApplyPatternPlaceholders(
            "Email: {p:Email:first}", fullText, patterns, regexes);

        Assert.Equal("Email: ", result);
    }

    [Fact]
    public void ApplyPatternPlaceholders_PatternNotFound_ReturnsEmpty()
    {
        string fullText = "Some text";
        List<TemplatePatternMatch> patterns =
        [
            new("id1", "Email", "first")
        ];
        // No regexes provided for this pattern
        Dictionary<string, string> regexes = [];

        string result = GrabTemplateExecutor.ApplyPatternPlaceholders(
            "Email: {p:Email:first}", fullText, patterns, regexes);

        Assert.Equal("Email: ", result);
    }

    [Fact]
    public void ApplyPatternPlaceholders_UnknownPatternName_LeavesPlaceholder()
    {
        string fullText = "Some text";
        List<TemplatePatternMatch> patterns = []; // no patterns registered
        Dictionary<string, string> regexes = [];

        string result = GrabTemplateExecutor.ApplyPatternPlaceholders(
            "Data: {p:Unknown:first}", fullText, patterns, regexes);

        Assert.Equal("Data: {p:Unknown:first}", result);
    }

    [Fact]
    public void ApplyPatternPlaceholders_IndexOutOfRange_ReturnsEmpty()
    {
        string fullText = "One: 100";
        List<TemplatePatternMatch> patterns =
        [
            new("id1", "Integer", "5") // only 1 match, requesting 5th
        ];
        Dictionary<string, string> regexes = new()
        {
            ["id1"] = @"\d+",
            ["Integer"] = @"\d+"
        };

        string result = GrabTemplateExecutor.ApplyPatternPlaceholders(
            "Fifth: {p:Integer:5}", fullText, patterns, regexes);

        Assert.Equal("Fifth: ", result);
    }

    // ── ExtractMatchesByMode ──────────────────────────────────────────────────

    [Fact]
    public void ExtractMatchesByMode_First_ReturnsFirst()
    {
        System.Text.RegularExpressions.MatchCollection matches =
            System.Text.RegularExpressions.Regex.Matches("abc def ghi", @"\w+");

        string result = GrabTemplateExecutor.ExtractMatchesByMode(matches, "first", ", ");
        Assert.Equal("abc", result);
    }

    [Fact]
    public void ExtractMatchesByMode_Last_ReturnsLast()
    {
        System.Text.RegularExpressions.MatchCollection matches =
            System.Text.RegularExpressions.Regex.Matches("abc def ghi", @"\w+");

        string result = GrabTemplateExecutor.ExtractMatchesByMode(matches, "last", ", ");
        Assert.Equal("ghi", result);
    }

    [Fact]
    public void ExtractMatchesByMode_All_JoinsAll()
    {
        System.Text.RegularExpressions.MatchCollection matches =
            System.Text.RegularExpressions.Regex.Matches("abc def ghi", @"\w+");

        string result = GrabTemplateExecutor.ExtractMatchesByMode(matches, "all", " | ");
        Assert.Equal("abc | def | ghi", result);
    }

    // ── Hybrid template (regions + patterns) ──────────────────────────────────

    [Fact]
    public void HybridTemplate_RegionsAndPatterns_BothResolved()
    {
        // First apply regions
        Dictionary<int, string> regions = new() { [1] = "John Doe" };
        string template = "Name: {1}\\nEmail: {p:Email:first}";
        string afterRegions = GrabTemplateExecutor.ApplyOutputTemplate(template, regions);

        // Then apply patterns
        string fullText = "Contact john@example.com for details";
        List<TemplatePatternMatch> patterns =
        [
            new("id1", "Email", "first")
        ];
        Dictionary<string, string> regexes = new()
        {
            ["id1"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
            ["Email"] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"
        };

        string result = GrabTemplateExecutor.ApplyPatternPlaceholders(
            afterRegions, fullText, patterns, regexes);

        Assert.Equal("Name: John Doe\nEmail: john@example.com", result);
    }

    // ── GrabTemplate model ────────────────────────────────────────────────────

    [Fact]
    public void GrabTemplate_IsValid_PatternOnlyTemplate()
    {
        GrabTemplate template = new("Test")
        {
            OutputTemplate = "{p:Email:first}",
            PatternMatches = [new("id1", "Email", "first")]
        };

        Assert.True(template.IsValid);
    }

    [Fact]
    public void GrabTemplate_IsValid_RequiresNameAndOutput()
    {
        GrabTemplate template = new()
        {
            PatternMatches = [new("id1", "Email", "first")]
        };

        Assert.False(template.IsValid);
    }

    [Fact]
    public void GrabTemplate_GetReferencedPatternNames_ParsesNames()
    {
        GrabTemplate template = new("Test")
        {
            OutputTemplate = "Email: {p:Email Address:first}\\nPhone: {p:Phone Number:all:, }"
        };

        List<string> names = [.. template.GetReferencedPatternNames()];
        Assert.Equal(2, names.Count);
        Assert.Contains("Email Address", names);
        Assert.Contains("Phone Number", names);
    }

    // ── ValidateOutputTemplate with patterns ──────────────────────────────────

    [Fact]
    public void ValidateOutputTemplate_ValidPatternPlaceholder_NoIssues()
    {
        List<string> issues = GrabTemplateExecutor.ValidateOutputTemplate(
            "{p:Email:first}",
            [],
            ["Email"]);

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateOutputTemplate_UnknownPatternName_ReturnsIssue()
    {
        List<string> issues = GrabTemplateExecutor.ValidateOutputTemplate(
            "{p:Unknown Pattern:first}",
            [],
            ["Email"]);

        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Contains("Unknown Pattern"));
    }

    [Fact]
    public void ValidateOutputTemplate_InvalidMatchMode_ReturnsIssue()
    {
        List<string> issues = GrabTemplateExecutor.ValidateOutputTemplate(
            "{p:Email:invalid_mode}",
            [],
            ["Email"]);

        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Contains("invalid_mode"));
    }
}
