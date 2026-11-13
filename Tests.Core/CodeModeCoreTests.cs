using System.Drawing;
using Text_Grab.Core.Editing;

namespace Text_Grab.Tests.Core;

public class CodeLanguageDetectorTests
{
    [Theory]
    [InlineData("using System;\r\n\r\nnamespace Demo;\r\n\r\npublic class Greeter\r\n{\r\n    public string Name { get; set; }\r\n}", CodeLanguages.CSharp)]
    [InlineData("public static async Task Main()\n{\n    Console.WriteLine(\"hi\");\n    await Task.Delay(10);\n}", CodeLanguages.CSharp)]
    [InlineData("import java.util.List;\n\npublic class App {\n    public static void main(String[] args) {\n        System.out.println(\"hi\");\n    }\n}", CodeLanguages.Java)]
    [InlineData("const items = [1, 2, 3];\nfunction total(list) {\n  return list.reduce((a, b) => a + b, 0);\n}\nconsole.log(total(items));", CodeLanguages.JavaScript)]
    [InlineData("import os\nfrom pathlib import Path\n\ndef main() -> None:\n    for p in Path('.').iterdir():\n        print(p)\n\nif __name__ == '__main__':\n    main()", CodeLanguages.Python)]
    [InlineData("param([string]$Path)\nGet-ChildItem $Path | Where-Object { $_.Length -gt 1kb } | ForEach-Object { Write-Host $_.Name }", CodeLanguages.PowerShell)]
    [InlineData("SELECT u.Name, COUNT(*) AS Orders\nFROM Users u\nINNER JOIN Orders o ON o.UserId = u.Id\nWHERE u.Active = 1\nGROUP BY u.Name\nORDER BY Orders DESC;", CodeLanguages.Sql)]
    [InlineData("#include <iostream>\n\nint main() {\n    std::cout << \"hi\" << std::endl;\n    return 0;\n}", CodeLanguages.Cpp)]
    [InlineData(".card {\n  margin: 0 auto;\n  padding: 12px;\n  color: #333;\n}\n@media (max-width: 600px) {\n  .card { padding: 4px; }\n}", CodeLanguages.Css)]
    [InlineData("Public Class Form1\n    Private Sub Button1_Click()\n        Dim count As Integer = 1\n    End Sub\nEnd Class", CodeLanguages.VisualBasic)]
    [InlineData("{ \"name\": \"text-grab\", \"tags\": [\"ocr\", \"wpf\"], }", CodeLanguages.Json)]
    [InlineData("<?php\necho 'hi';", CodeLanguages.Php)]
    [InlineData("<!DOCTYPE html>\n<html><body><p>hi</p></body></html>", CodeLanguages.Html)]
    [InlineData("<?xml version=\"1.0\"?>\n<Project Sdk=\"Microsoft.NET.Sdk\"></Project>", CodeLanguages.Xml)]
    [InlineData("--- a/file.txt\n+++ b/file.txt\n@@ -1,2 +1,2 @@\n-old\n+new", CodeLanguages.Patch)]
    public void Detect_RecognizesCommonLanguages(string text, string expected)
    {
        Assert.Equal(expected, CodeLanguageDetector.Detect(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n ")]
    [InlineData("Meeting notes\r\nBuy milk, call Sam, finish the report by Friday.")]
    [InlineData("Please select the items from the list where it applies.")]
    [InlineData("{ this is not json")]
    public void Detect_ReturnsNullForProseAndNothing(string text)
    {
        Assert.Null(CodeLanguageDetector.Detect(text));
    }
}

public class CodeLanguagesTests
{
    [Theory]
    [InlineData(".cs", CodeLanguages.CSharp)]
    [InlineData("CS", CodeLanguages.CSharp)]
    [InlineData(".tsx", CodeLanguages.JavaScript)]
    [InlineData(".csproj", CodeLanguages.Xml)]
    [InlineData(".SQL", CodeLanguages.Sql)]
    public void FindByExtension_IgnoresCaseAndTheDot(string extension, string expected)
    {
        Assert.Equal(expected, CodeLanguages.FindByExtension(extension)?.Name);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData("")]
    [InlineData(null)]
    public void FindByExtension_ReturnsNullForNonCode(string? extension)
    {
        Assert.Null(CodeLanguages.FindByExtension(extension));
    }

    [Fact]
    public void GetDisplayName_UsesFriendlyNamesAndPlainTextForNone()
    {
        Assert.Equal("SQL", CodeLanguages.GetDisplayName(CodeLanguages.Sql));
        Assert.Equal(CodeLanguages.PlainTextDisplayName, CodeLanguages.GetDisplayName(null));
        Assert.Equal("Unknown", CodeLanguages.GetDisplayName("Unknown"));
    }

    [Fact]
    public void EveryLanguageHasADistinctNameAndAtLeastOneExtension()
    {
        Assert.Equal(CodeLanguages.All.Count, CodeLanguages.All.Select(l => l.Name).Distinct().Count());
        Assert.All(CodeLanguages.All, language => Assert.NotEmpty(language.Extensions));
    }
}

public class TextEditTests
{
    [Theory]
    [InlineData("int x = 1;", "int x = 42;", 8, 1, "42")]
    [InlineData("abc", "abc", 3, 0, "")]
    [InlineData("", "new", 0, 0, "new")]
    [InlineData("gone", "", 0, 4, "")]
    [InlineData("aaa", "aaaa", 3, 0, "a")]
    [InlineData("one\r\ntwo", "one\r\nTWO", 5, 3, "TWO")]
    public void Between_FindsTheSmallestReplacement(string oldText, string newText, int start, int removed, string inserted)
    {
        TextEdit edit = TextEdit.Between(oldText, newText);

        Assert.Equal(new TextSpan(start, removed), edit.Removed);
        Assert.Equal(inserted, edit.Inserted);
        Assert.Equal(newText, edit.ApplyTo(oldText));
    }
}

public class BracketMatcherTests
{
    [Fact]
    public void Find_OpenerAfterCaret_ReturnsItsCloser()
    {
        Assert.Equal((0, 3), BracketMatcher.Find("(ab)", 0));
    }

    [Fact]
    public void Find_CloserBeforeCaret_ReturnsItsOpener()
    {
        Assert.Equal((3, 0), BracketMatcher.Find("(ab)", 4));
    }

    [Fact]
    public void Find_SkipsNestedPairs()
    {
        Assert.Equal((0, 6), BracketMatcher.Find("{a{b}c}", 0));
        Assert.Equal((2, 4), BracketMatcher.Find("{a{b}c}", 2));
    }

    [Fact]
    public void Find_PrefersTheBracketBeforeTheCaret()
    {
        // Caret between "(" and ")": the "(" before it wins.
        Assert.Equal((0, 1), BracketMatcher.Find("()", 1));
    }

    [Theory]
    [InlineData("(ab", 0)]
    [InlineData("ab)", 3)]
    [InlineData("abc", 1)]
    [InlineData("", 0)]
    public void Find_ReturnsNullWhenThereIsNoMatch(string text, int caret)
    {
        Assert.Null(BracketMatcher.Find(text, caret));
    }

    [Fact]
    public void Find_IgnoresOtherBracketKinds()
    {
        // The "[" is not closed by ")", so it is unmatched; the "(" pairs across it.
        Assert.Null(BracketMatcher.Find("[x)", 0));
        Assert.Equal((0, 3), BracketMatcher.Find("(a[)", 0));
    }

    [Fact]
    public void Find_CaretOutOfRange_ReturnsNull()
    {
        Assert.Null(BracketMatcher.Find("()", 5));
        Assert.Null(BracketMatcher.Find("()", -1));
    }
}

public class CodeHighlightColorsTests
{
    [Theory]
    [InlineData(0x00, 0x00, 0xFF)] // AvalonEdit's keyword blue
    [InlineData(0x00, 0x80, 0x00)] // comment green
    [InlineData(0xA3, 0x15, 0x15)] // string dark red
    [InlineData(0x00, 0x00, 0x00)] // black
    [InlineData(0x80, 0x80, 0x80)] // gray
    public void ForegroundForDarkTheme_IsReadableOnTheDarkBackground(int r, int g, int b)
    {
        Color adjusted = CodeHighlightColors.ForegroundForDarkTheme(Color.FromArgb(r, g, b));

        Assert.True(CodeHighlightColors.ContrastRatio(adjusted, CodeHighlightColors.DarkBackground) >= CodeHighlightColors.MinimumContrast);
    }

    [Fact]
    public void ForegroundForDarkTheme_KeepsTheHueFamily()
    {
        Color green = CodeHighlightColors.ForegroundForDarkTheme(Color.FromArgb(0x00, 0x80, 0x00));

        Assert.True(green.G > green.R && green.G > green.B);
    }

    [Fact]
    public void BackgroundForDarkTheme_IsDark()
    {
        Color adjusted = CodeHighlightColors.BackgroundForDarkTheme(Color.FromArgb(0xFF, 0xFF, 0xE0));

        Assert.True(CodeHighlightColors.RelativeLuminance(adjusted) < 0.1);
    }

    [Fact]
    public void IsDarkThemeText_IsTrueForLightText()
    {
        Assert.True(CodeHighlightColors.IsDarkThemeText(Color.White));
        Assert.False(CodeHighlightColors.IsDarkThemeText(Color.FromArgb(0x1A, 0x1A, 0x1A)));
    }
}
