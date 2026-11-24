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

    [Theory]
    [InlineData(EtwEditorMode.Markdown, EtwEditorMode.Text, EtwEditorMode.Text)]
    [InlineData(EtwEditorMode.Text, EtwEditorMode.Markdown, EtwEditorMode.Markdown)]
    [InlineData(EtwEditorMode.Text, EtwEditorMode.Text, EtwEditorMode.Text)]
    [InlineData(EtwEditorMode.Spreadsheet, EtwEditorMode.Text, EtwEditorMode.Text)]
    [InlineData(EtwEditorMode.Spreadsheet, EtwEditorMode.Spreadsheet, EtwEditorMode.Spreadsheet)]
    [InlineData(EtwEditorMode.Text, EtwEditorMode.Spreadsheet, EtwEditorMode.Code)] // Not a table type: the layout decides.
    [InlineData(EtwEditorMode.Markdown, null, EtwEditorMode.Code)] // Nothing remembered: the layout decides.
    public void TheModeAFileWasLastShownIn_BeatsTheLayoutAndItsType(EtwEditorMode fileMode, EtwEditorMode? saved, EtwEditorMode expected)
        => Assert.Equal(expected, LayoutDefaults.ModeForFile(fileMode, Layout(EtwEditorMode.Code), saved));

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

    // ---- Switching layouts on a window with a document (E6 picker) ----

    [Theory]
    [InlineData(EtwEditorMode.Text, false, EtwEditorMode.Code)]
    [InlineData(EtwEditorMode.Code, false, EtwEditorMode.Code)]
    [InlineData(EtwEditorMode.Spreadsheet, false, EtwEditorMode.Code)]
    [InlineData(EtwEditorMode.Spreadsheet, true, EtwEditorMode.Spreadsheet)]
    [InlineData(EtwEditorMode.Markdown, false, EtwEditorMode.Markdown)]
    public void ASwitchToACodeLayout_ChangesTheModeUnlessThatWouldLoseSomething(EtwEditorMode current, bool formatted, EtwEditorMode expected)
        => Assert.Equal(expected, LayoutDefaults.ModeOnSwitch(current, Layout(EtwEditorMode.Code), formatted));

    [Theory]
    [InlineData(EtwEditorMode.Text)]
    [InlineData(EtwEditorMode.Code)]
    [InlineData(EtwEditorMode.Markdown)]
    [InlineData(EtwEditorMode.Spreadsheet)]
    public void ASwitchToALayoutWithNoMode_ChangesNothing(EtwEditorMode current)
    {
        Assert.Equal(current, LayoutDefaults.ModeOnSwitch(current, Layout(null), false));
        Assert.Equal(current, LayoutDefaults.ModeOnSwitch(current, BuiltInLayouts.Classic, true));
        Assert.Equal(current, LayoutDefaults.ModeOnSwitch(current, null, false));
    }

    [Fact]
    public void ASwitchToATextLayout_LeavesCodeModeForTextButNeverMarkdownOrFormattedTables()
    {
        Assert.Equal(EtwEditorMode.Text, LayoutDefaults.ModeOnSwitch(EtwEditorMode.Code, Layout(EtwEditorMode.Text), false));
        Assert.Equal(EtwEditorMode.Markdown, LayoutDefaults.ModeOnSwitch(EtwEditorMode.Markdown, Layout(EtwEditorMode.Text), false));
        Assert.Equal(EtwEditorMode.Spreadsheet, LayoutDefaults.ModeOnSwitch(EtwEditorMode.Spreadsheet, Layout(EtwEditorMode.Text), true));
    }

    [Fact]
    public void ASwitchSetsTheLanguageOnlyWhenTheWindowHasNone()
    {
        EtwLayoutDefinition layout = Layout(EtwEditorMode.Code, CodeLanguages.Markdown);

        Assert.Null(LayoutDefaults.CodeLanguageOnSwitch(languageAlreadySet: true, layout));
        Assert.Equal(new CodeLanguageChoice(CodeLanguages.Markdown, false), LayoutDefaults.CodeLanguageOnSwitch(languageAlreadySet: false, layout));
    }

    [Fact]
    public void ATableWithWrapWidthsOrHeights_HasViewFormatting()
    {
        EditTextTableDocument plain = EditTextTableDocument.CreateFromText("a\tb\n1\t2");
        Assert.False(plain.HasViewFormatting);

        EditTextTableDocument wrapped = EditTextTableDocument.CreateFromText("a\tb\n1\t2");
        wrapped.SetCellWrap(0, 0, true);
        Assert.True(wrapped.HasViewFormatting);

        EditTextTableDocument wide = EditTextTableDocument.CreateFromText("a\tb\n1\t2");
        wide.SetColumnWidth(0, 120);
        Assert.True(wide.HasViewFormatting);

        EditTextTableDocument tall = EditTextTableDocument.CreateFromText("a\tb\n1\t2");
        tall.SetRowHeight(0, 40);
        Assert.True(tall.HasViewFormatting);

        // It is not a stored member: the table's JSON does not change.
        Assert.DoesNotContain("HasViewFormatting", plain.SerializeToJson());
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
