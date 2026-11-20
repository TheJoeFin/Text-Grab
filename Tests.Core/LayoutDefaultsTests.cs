using Text_Grab.Core.Editing;
using Text_Grab.Core.Layouts;
using Text_Grab.Models;

namespace Text_Grab.Tests.Core;

/// <summary>
/// How a layout's DefaultMode and DefaultCodeLanguage meet the mode a window would open in (v5 E5
/// <c>mermaid</c>). The rule is written out on <see cref="LayoutDefaults"/>.
/// </summary>
public class LayoutDefaultsTests
{
    private static EtwLayoutDefinition Layout(EtwEditorMode? mode, string? language = null)
        => BuiltInLayouts.Classic with { Id = "test", DefaultMode = mode, DefaultCodeLanguage = language };

    // ---- A file opened while the layout shows ----

    [Theory]
    [InlineData(EtwEditorMode.Text, EtwEditorMode.Code)]
    [InlineData(EtwEditorMode.Markdown, EtwEditorMode.Code)]
    [InlineData(EtwEditorMode.Code, EtwEditorMode.Code)]
    [InlineData(EtwEditorMode.Spreadsheet, EtwEditorMode.Spreadsheet)]
    public void ACodeLayout_ReplacesTheTextAndMarkdownModesOfAFileAndNothingElse(EtwEditorMode fileMode, EtwEditorMode expected)
        => Assert.Equal(expected, LayoutDefaults.ModeForFile(fileMode, Layout(EtwEditorMode.Code)));

    [Theory]
    [InlineData(EtwEditorMode.Text)]
    [InlineData(EtwEditorMode.Markdown)]
    [InlineData(EtwEditorMode.Code)]
    [InlineData(EtwEditorMode.Spreadsheet)]
    public void NoDefaultMode_ChangesNothing(EtwEditorMode fileMode)
    {
        Assert.Equal(fileMode, LayoutDefaults.ModeForFile(fileMode, Layout(null)));
        Assert.Equal(fileMode, LayoutDefaults.ModeForFile(fileMode, BuiltInLayouts.Classic));
        Assert.Equal(fileMode, LayoutDefaults.ModeForFile(fileMode, null));
    }

    // ---- A window with no file ----

    [Theory]
    [InlineData(EtwEditorMode.Text, EtwEditorMode.Code)]
    [InlineData(EtwEditorMode.Markdown, EtwEditorMode.Markdown)]
    [InlineData(EtwEditorMode.Spreadsheet, EtwEditorMode.Spreadsheet)]
    [InlineData(EtwEditorMode.Code, EtwEditorMode.Code)]
    public void ABlankWindow_TakesTheLayoutsModeOnlyFromItsDefaultTextMode(EtwEditorMode current, EtwEditorMode expected)
        => Assert.Equal(expected, LayoutDefaults.ModeForBlankWindow(current, Layout(EtwEditorMode.Code)));

    [Fact]
    public void ABlankWindow_WithNoDefaultMode_KeepsItsMode()
    {
        Assert.Equal(EtwEditorMode.Text, LayoutDefaults.ModeForBlankWindow(EtwEditorMode.Text, Layout(null)));
        Assert.Equal(EtwEditorMode.Text, LayoutDefaults.ModeForBlankWindow(EtwEditorMode.Text, null));
    }

    // ---- The language ----

    [Fact]
    public void TheFilesOwnLanguage_BeatsTheLayoutsDefault()
        => Assert.Equal(new CodeLanguageChoice(CodeLanguages.CSharp, false), LayoutDefaults.CodeLanguageFor(CodeLanguages.CSharp, Layout(EtwEditorMode.Code, CodeLanguages.Markdown)));

    [Fact]
    public void ANamedDefaultLanguage_IsUsedWithItsCanonicalName()
        => Assert.Equal(new CodeLanguageChoice(CodeLanguages.Markdown, false), LayoutDefaults.CodeLanguageFor(null, Layout(EtwEditorMode.Code, "markdown")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoDefaultLanguage_IsPlainTextNeverDetection(string? language)
        => Assert.Equal(new CodeLanguageChoice(null, false), LayoutDefaults.CodeLanguageFor(null, Layout(EtwEditorMode.Code, language)));

    [Fact]
    public void AnUnknownDefaultLanguage_FallsBackToDetection()
        => Assert.Equal(new CodeLanguageChoice(null, true), LayoutDefaults.CodeLanguageFor(null, Layout(EtwEditorMode.Code, "Klingon")));

    [Fact]
    public void NoLayout_IsPlainText()
        => Assert.Equal(new CodeLanguageChoice(null, false), LayoutDefaults.CodeLanguageFor(null, null));
}
