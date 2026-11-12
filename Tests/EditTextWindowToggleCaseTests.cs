using System.Globalization;
using Text_Grab;
using Text_Grab.Models;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// Characterization tests for the Edit Text Window's Toggle Case command, written against the code
/// before A1 lifted the case logic into <c>StringMethods.ToggleCase</c>. They pin what the window does
/// today, including the quirk that the first press only re-applies the case the text already has.
/// </summary>
public class EditTextWindowToggleCaseTests
{
    private static void PressToggleCase(EditTextWindow window)
        => EditTextWindow.ToggleCaseCmd.Execute(null, RawTextBox(window));

    [WpfFact]
    public void LowerText_FirstPressKeepsItAndLaterPressesCycleTitleUpperLower()
    {
        EditTextWindow window = CreateWindow("the quick fox");

        PressToggleCase(window);
        Assert.Equal("the quick fox", RawText(window));
        Assert.Equal(CurrentCase.Camel, window.CaseStatusOfToggle);

        PressToggleCase(window);
        Assert.Equal("The Quick Fox", RawText(window));
        Assert.Equal(CurrentCase.Upper, window.CaseStatusOfToggle);

        PressToggleCase(window);
        Assert.Equal("THE QUICK FOX", RawText(window));
        Assert.Equal(CurrentCase.Lower, window.CaseStatusOfToggle);

        PressToggleCase(window);
        Assert.Equal("the quick fox", RawText(window));
        Assert.Equal(CurrentCase.Camel, window.CaseStatusOfToggle);
    }

    [WpfFact]
    public void UpperText_FirstPressKeepsItThenLowers()
    {
        EditTextWindow window = CreateWindow("THE QUICK FOX");

        PressToggleCase(window);
        Assert.Equal("THE QUICK FOX", RawText(window));
        Assert.Equal(CurrentCase.Lower, window.CaseStatusOfToggle);

        PressToggleCase(window);
        Assert.Equal("the quick fox", RawText(window));
        Assert.Equal(CurrentCase.Camel, window.CaseStatusOfToggle);
    }

    [WpfFact]
    public void MixedText_FirstPressTitleCases()
    {
        EditTextWindow window = CreateWindow("tHe CaSe");

        PressToggleCase(window);

        Assert.Equal("The Case", RawText(window));
        Assert.Equal(CurrentCase.Upper, window.CaseStatusOfToggle);
    }

    [WpfFact]
    public void StateCarriesOverToDifferentText()
    {
        // The window remembers the next case, not the text it saw, so new text continues the cycle.
        EditTextWindow window = CreateWindow("the quick fox");
        PressToggleCase(window);
        Assert.Equal(CurrentCase.Camel, window.CaseStatusOfToggle);

        RawTextBox(window).Text = "SHOUTING";
        PressToggleCase(window);

        // Title case leaves an all-capitals word alone (TextInfo.ToTitleCase treats it as an acronym).
        Assert.Equal("SHOUTING", RawText(window));
        Assert.Equal(CurrentCase.Upper, window.CaseStatusOfToggle);

        RawTextBox(window).Text = "whisper";
        PressToggleCase(window);

        Assert.Equal("WHISPER", RawText(window));
        Assert.Equal(CurrentCase.Lower, window.CaseStatusOfToggle);
    }

    [WpfFact]
    public void WhitespaceOnlyText_IsUnchangedAndTheStateStaysUnknown()
    {
        EditTextWindow window = CreateWindow("   ");

        PressToggleCase(window);

        Assert.Equal("   ", RawText(window));
        Assert.Equal(CurrentCase.Unknown, window.CaseStatusOfToggle);
    }

    [WpfFact]
    public void WithASelection_OnlyTheSelectionChanges()
    {
        EditTextWindow window = CreateWindow("the quick fox");
        window.CaseStatusOfToggle = CurrentCase.Upper;

        window.SelectInEditor("the ".Length, "quick".Length);
        PressToggleCase(window);

        Assert.Equal("the QUICK fox", RawText(window));
        Assert.Equal(CurrentCase.Lower, window.CaseStatusOfToggle);
    }

    [WpfFact]
    public void WithASelection_FirstPressDetectsTheCaseOfTheSelectionOnly()
    {
        EditTextWindow window = CreateWindow("THE quick FOX");

        window.SelectInEditor("THE ".Length, "quick".Length);
        PressToggleCase(window);

        Assert.Equal("THE quick FOX", RawText(window));
        Assert.Equal(CurrentCase.Camel, window.CaseStatusOfToggle);
    }

    [WpfFact]
    public void UsesTheWindowsCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            // The window takes its culture from CurrentCulture when it is built.
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            EditTextWindow window = CreateWindow("istanbul");
            window.CaseStatusOfToggle = CurrentCase.Upper;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            PressToggleCase(window);

            Assert.Equal("İSTANBUL", RawText(window));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [WpfFact]
    public void Spreadsheet_EachCellGetsItsOwnDetectedCaseAndTheStateResets()
    {
        EditTextWindow window = CreateWindow("aNN\tparis\r\nBOB\tlIMA");
        window.CaseStatusOfToggle = CurrentCase.Upper;
        SwitchMode(window, EtwEditorMode.Spreadsheet);

        EditTextWindow.ToggleCaseCmd.Execute(null, Grid(window));

        // Lower and upper cells keep their case; mixed-case cells are title cased.
        Assert.Equal("Ann\tparis\r\nBOB\tLima", RawText(window));
        Assert.Equal(CurrentCase.Unknown, window.CaseStatusOfToggle);
    }
}
