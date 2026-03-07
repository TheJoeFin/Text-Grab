using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

public class CaptureLanguageUtilitiesTests
{
    [Fact]
    public void MatchesPersistedLanguage_MatchesByLanguageTag()
    {
        UiAutomationLang language = new();

        bool matches = CaptureLanguageUtilities.MatchesPersistedLanguage(language, UiAutomationLang.Tag);

        Assert.True(matches);
    }

    [Fact]
    public void MatchesPersistedLanguage_MatchesLegacyTesseractDisplayName()
    {
        TessLang language = new("eng");

        bool matches = CaptureLanguageUtilities.MatchesPersistedLanguage(language, language.CultureDisplayName);

        Assert.True(matches);
    }

    [Fact]
    public void FindPreferredLanguageIndex_PrefersPersistedMatchBeforeFallbackLanguage()
    {
        List<Text_Grab.Interfaces.ILanguage> languages =
        [
            new UiAutomationLang(),
            new WindowsAiLang(),
            new GlobalLang("en-US")
        ];

        int index = CaptureLanguageUtilities.FindPreferredLanguageIndex(
            languages,
            UiAutomationLang.Tag,
            new GlobalLang("en-US"));

        Assert.Equal(0, index);
    }

    [Fact]
    public void SupportsTableOutput_ReturnsFalseForUiAutomation()
    {
        Assert.False(CaptureLanguageUtilities.SupportsTableOutput(new UiAutomationLang()));
    }
}
