using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Tests;

[Collection("Settings isolation")]
public class CaptureLanguageUtilitiesTests : IDisposable
{
    private readonly bool _originalUiAutomationEnabled;
    private readonly bool _originalWindowsAiDescriptionEnabled;

    public CaptureLanguageUtilitiesTests()
    {
        _originalUiAutomationEnabled = Settings.Default.UiAutomationEnabled;
        _originalWindowsAiDescriptionEnabled = Settings.Default.WindowsAiDescriptionEnabled;
    }

    public void Dispose()
    {
        Settings.Default.UiAutomationEnabled = _originalUiAutomationEnabled;
        Settings.Default.WindowsAiDescriptionEnabled = _originalWindowsAiDescriptionEnabled;
        Settings.Default.Save();
        LanguageUtilities.InvalidateAllCaches();
    }

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
    public void MatchesPersistedLanguage_MatchesWindowsAiDescriptionTag()
    {
        WindowsAiDescriptionLang language = new();

        bool matches = CaptureLanguageUtilities.MatchesPersistedLanguage(language, WindowsAiDescriptionLang.Tag);

        Assert.True(matches);
    }

    [Fact]
    public void FindPreferredLanguageIndex_PrefersPersistedMatchBeforeFallbackLanguage()
    {
        List<ILanguage> languages =
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

    [WpfFact]
    public async Task GetCaptureLanguagesAsync_ExcludesUiAutomationByDefault()
    {
        Settings.Default.UiAutomationEnabled = false;
        Settings.Default.Save();
        LanguageUtilities.InvalidateAllCaches();

        List<ILanguage> languages = await CaptureLanguageUtilities.GetCaptureLanguagesAsync(includeTesseract: false);

        Assert.DoesNotContain(languages, language => language is UiAutomationLang);
    }

    [WpfFact]
    public async Task GetCaptureLanguagesAsync_IncludesUiAutomationWhenEnabled()
    {
        Settings.Default.UiAutomationEnabled = true;
        Settings.Default.Save();
        LanguageUtilities.InvalidateAllCaches();

        List<ILanguage> languages = await CaptureLanguageUtilities.GetCaptureLanguagesAsync(includeTesseract: false);

        Assert.Contains(languages, language => language is UiAutomationLang);
    }

    [WpfFact]
    public async Task GetCaptureLanguagesAsync_ExcludesWindowsAiDescriptionByDefault()
    {
        Settings.Default.WindowsAiDescriptionEnabled = false;
        Settings.Default.Save();
        LanguageUtilities.InvalidateAllCaches();

        List<ILanguage> languages = await CaptureLanguageUtilities.GetCaptureLanguagesAsync(includeTesseract: false);

        Assert.DoesNotContain(languages, language => language is WindowsAiDescriptionLang);
    }

    [WpfFact]
    public async Task GetCaptureLanguagesAsync_IncludesWindowsAiDescriptionOnlyWhenSupported()
    {
        Settings.Default.WindowsAiDescriptionEnabled = true;
        Settings.Default.Save();
        LanguageUtilities.InvalidateAllCaches();

        List<ILanguage> languages = await CaptureLanguageUtilities.GetCaptureLanguagesAsync(includeTesseract: false);

        if (WindowsAiUtilities.CanDeviceDescribeImagesWithWinAI())
            Assert.Contains(languages, language => language is WindowsAiDescriptionLang);
        else
            Assert.DoesNotContain(languages, language => language is WindowsAiDescriptionLang);
    }

    [Fact]
    public void SupportsTableOutput_ReturnsFalseForUiAutomation()
    {
        Assert.False(CaptureLanguageUtilities.SupportsTableOutput(new UiAutomationLang()));
    }

    [Fact]
    public void SupportsTableOutput_ReturnsFalseForWindowsAiDescription()
    {
        Assert.False(CaptureLanguageUtilities.SupportsTableOutput(new WindowsAiDescriptionLang()));
    }

    [Fact]
    public void RequiresLiveUiAutomationSource_ReturnsTrueForStaticUiAutomationWithoutSnapshot()
    {
        bool requiresLiveSource = CaptureLanguageUtilities.RequiresLiveUiAutomationSource(
            new UiAutomationLang(),
            isStaticImageSource: true,
            hasFrozenUiAutomationSnapshot: false);

        Assert.True(requiresLiveSource);
    }

    [Fact]
    public void RequiresLiveUiAutomationSource_ReturnsFalseWhenFrozenSnapshotExists()
    {
        bool requiresLiveSource = CaptureLanguageUtilities.RequiresLiveUiAutomationSource(
            new UiAutomationLang(),
            isStaticImageSource: true,
            hasFrozenUiAutomationSnapshot: true);

        Assert.False(requiresLiveSource);
    }

    [Fact]
    public void RequiresLiveUiAutomationSource_ReturnsFalseForOcrLanguageOnStaticImage()
    {
        bool requiresLiveSource = CaptureLanguageUtilities.RequiresLiveUiAutomationSource(
            new GlobalLang("en-US"),
            isStaticImageSource: true,
            hasFrozenUiAutomationSnapshot: false);

        Assert.False(requiresLiveSource);
    }
}
