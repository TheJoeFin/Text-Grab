using Text_Grab.Core.Editing;

namespace Text_Grab.Tests.Core;

/// <summary>Each test lists the text each folding region would hide, in order.</summary>
public class BraceFoldingTests
{
    private static readonly BraceFoldingSyntax CSharp = BraceFoldingSyntax.CStyle with { VerbatimStrings = true, TripleQuotedStrings = true };

    private static string[] Folded(string text, BraceFoldingSyntax syntax)
        => [.. BraceFolding.Find(text, syntax).Select(region => text.Substring(region.Start, region.Length))];

    [Fact]
    public void BracesOnTheirOwnLine_FoldFromTheEndOfTheLineAbove()
    {
        string text = "class A\n{\n    void M()\n    {\n        int x = 1;\n    }\n}";

        Assert.Equal(
            ["\n{\n    void M()\n    {\n        int x = 1;\n    }\n}", "\n    {\n        int x = 1;\n    }"],
            Folded(text, CSharp));
    }

    [Fact]
    public void BracesAtTheEndOfALine_FoldFromTheBrace()
    {
        Assert.Equal(["{\n  return 1;\n}"], Folded("function f() {\n  return 1;\n}", BraceFoldingSyntax.CStyle));
    }

    [Fact]
    public void WindowsLineBreaks_StayOutsideTheSignatureLine()
    {
        Assert.Equal(["\r\n{\r\n    y();\r\n}"], Folded("if (x)\r\n{\r\n    y();\r\n}", CSharp));
    }

    [Fact]
    public void BracesOnOneLine_DoNotFold()
    {
        Assert.Empty(Folded("var p = new { A = 1 };\nvar q = new { B = 2 };", CSharp));
    }

    [Fact]
    public void BracesInStringsCommentsAndCharacters_AreIgnored()
    {
        string text = "void M()\n{\n    var s = \"}\"; // }\n    char c = '{';\n    /* { */\n    var v = @\"{\n\"\"}\"\"\";\n    var r = \"\"\"\n}\n\"\"\";\n}";

        Assert.Equal([text["void M()".Length..]], Folded(text, CSharp));
    }

    [Fact]
    public void UnbalancedBrackets_StillFoldWhatPairs()
    {
        Assert.Equal(["{\n}"], Folded("{\n{\n}", CSharp));
        Assert.Equal(["{\n  a\n}"], Folded("x }\n{\n  a\n}\n}", BraceFoldingSyntax.CStyle));
    }

    [Fact]
    public void ABraceUnderAStatementOrBlock_FoldsFromTheBrace()
    {
        Assert.Equal(["{\n  b();\n}"], Folded("a();\n{\n  b();\n}", CSharp));
    }

    [Fact]
    public void AnUnterminatedQuote_EndsAtTheLineBreak()
    {
        string text = "x = 'it\ny {\n  z\n}";

        Assert.Equal(["{\n  z\n}"], Folded(text, BraceFoldingSyntax.CStyle));
    }

    [Fact]
    public void Json_FoldsObjectsAndArrays()
    {
        string text = "{\n  \"a\": [\n    1,\n    2\n  ],\n  \"b\": \"}\"\n}";

        Assert.Equal([text, "[\n    1,\n    2\n  ]"], Folded(text, BraceFoldingSyntax.CStyle with { Brackets = ["{}", "[]"], Quotes = "\"" }));
    }

    [Fact]
    public void JavaScriptTemplateLiterals_CanSpanLines()
    {
        string text = "const s = `\n}\n`;\nfunction f() {\n  return s;\n}";

        Assert.Equal(["{\n  return s;\n}"], Folded(text, BraceFoldingSyntax.CStyle with { MultiLineQuotes = "`" }));
    }

    [Fact]
    public void PowerShell_SkipsHashCommentsAndBlockComments()
    {
        string text = "# {\nfunction F {\n  <# }\n  #>\n  'it''s'\n}";
        BraceFoldingSyntax powerShell = BraceFoldingSyntax.CStyle with { LineComments = ["#"], BlockComments = [("<#", "#>")], EscapeCharacter = '`' };

        Assert.Equal(["{\n  <# }\n  #>\n  'it''s'\n}"], Folded(text, powerShell));
    }
}

/// <summary>Each test lists the text each folding region would hide, in order.</summary>
public class TagFoldingTests
{
    private static string[] Folded(string text, bool isHtml = false)
        => [.. TagFolding.Find(text, isHtml).Select(region => text.Substring(region.Start, region.Length))];

    [Fact]
    public void NestedElements_FoldTheirContentBetweenTheTags()
    {
        string text = "<root>\n  <child a=\"1\">\n    <leaf/>\n  </child>\n</root>";

        Assert.Equal(["\n  <child a=\"1\">\n    <leaf/>\n  </child>\n", "\n    <leaf/>\n  "], Folded(text));
    }

    [Fact]
    public void ElementsOnOneLine_DoNotFold()
    {
        Assert.Equal(["\n<b>x</b>\n"], Folded("<a>\n<b>x</b>\n</a>"));
    }

    [Fact]
    public void MultiLineStartTags_FoldFromTheTagName()
    {
        string text = "<Button\n  Click=\"C\">\n  <TextBlock/>\n</Button>";

        Assert.Equal(["\n  Click=\"C\">\n  <TextBlock/>\n"], Folded(text));
    }

    [Fact]
    public void MultiLineSelfClosingTags_FoldTheirAttributes()
    {
        string text = "<Grid>\n  <TextBox\n    x:Name=\"A\"\n    Text=\"B\" />\n</Grid>";

        Assert.Equal(
            ["\n  <TextBox\n    x:Name=\"A\"\n    Text=\"B\" />\n", "\n    x:Name=\"A\"\n    Text=\"B\" "],
            Folded(text));
    }

    [Fact]
    public void Comments_FoldBetweenTheirMarkers()
    {
        Assert.Equal(["\nnote\n"], Folded("<!--\nnote\n-->\n<a/>"));
    }

    [Fact]
    public void Html_VoidAndUnclosedElementsDoNotBreakTheirParent()
    {
        string text = "<div>\n<br>\n<p>one\n<img src=\"x\">\n</DIV>";

        Assert.Equal(["\n<br>\n<p>one\n<img src=\"x\">\n"], Folded(text, isHtml: true));
    }

    [Fact]
    public void Html_ScriptContentIsNotScannedForTags()
    {
        string text = "<script>\nif (a < b) { x(\"</div>\"); }\n</script>";

        Assert.Equal(["\nif (a < b) { x(\"</div>\"); }\n"], Folded(text, isHtml: true));
    }

    [Fact]
    public void QuotedAttributesMayContainAngleBrackets()
    {
        Assert.Equal(["\n"], Folded("<a title=\"x > y\">\n</a>"));
    }

    [Fact]
    public void ABrokenTag_DoesNotStopTheRest()
    {
        Assert.Equal(["\n"], Folded("<a\n<b>\n</b>"));
    }
}

public class CodeFoldingTests
{
    [Theory]
    [InlineData(CodeLanguages.CSharp, true)]
    [InlineData(CodeLanguages.Json, true)]
    [InlineData(CodeLanguages.PowerShell, true)]
    [InlineData(CodeLanguages.Xml, true)]
    [InlineData(CodeLanguages.Html, true)]
    [InlineData(CodeLanguages.Python, false)]
    [InlineData(CodeLanguages.Sql, false)]
    [InlineData(null, false)]
    public void IsSupported_CoversBraceAndMarkupLanguages(string? languageName, bool expected)
    {
        Assert.Equal(expected, CodeFolding.IsSupported(languageName));
    }

    [Fact]
    public void Find_UsesTheLanguagesRule()
    {
        string text = "{\n  \"a\": 1\n}";

        Assert.Single(CodeFolding.Find(text, CodeLanguages.Json));
        Assert.Empty(CodeFolding.Find(text, CodeLanguages.Python));
        Assert.Empty(CodeFolding.Find(text, null));
    }
}
