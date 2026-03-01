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
}
