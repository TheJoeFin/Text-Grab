using Text_Grab;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.Globalization;

namespace Tests;

[Collection("Settings isolation")]
public class LanguageServiceTests : IDisposable
{
    private readonly string _originalLastUsedLang;
    private readonly bool _originalUiAutomationEnabled;
    private readonly bool _originalWindowsAiDescriptionEnabled;

    public LanguageServiceTests()
    {
        _originalLastUsedLang = Settings.Default.LastUsedLang;
        _originalUiAutomationEnabled = Settings.Default.UiAutomationEnabled;
        _originalWindowsAiDescriptionEnabled = Settings.Default.WindowsAiDescriptionEnabled;
    }

    public void Dispose()
    {
        Settings.Default.LastUsedLang = _originalLastUsedLang;
        Settings.Default.UiAutomationEnabled = _originalUiAutomationEnabled;
        Settings.Default.WindowsAiDescriptionEnabled = _originalWindowsAiDescriptionEnabled;
        Settings.Default.Save();
        LanguageUtilities.InvalidateAllCaches();
    }

    [Fact]
    public void GetLanguageTag_WithGlobalLang_ReturnsCorrectTag()
    {
        GlobalLang globalLang = new("en-US");

        string tag = LanguageService.GetLanguageTag(globalLang);

        Assert.Equal("en-US", tag);
    }

    [Fact]
    public void GetLanguageTag_WithWindowsAiLang_ReturnsWinAI()
    {
        WindowsAiLang windowsAiLang = new();

        string tag = LanguageService.GetLanguageTag(windowsAiLang);

        Assert.Equal("WinAI", tag);
    }

    [Fact]
    public void GetLanguageTag_WithWindowsAiDescriptionLang_ReturnsDescriptionTag()
    {
        WindowsAiDescriptionLang windowsAiDescriptionLang = new();

        string tag = LanguageService.GetLanguageTag(windowsAiDescriptionLang);

        Assert.Equal(WindowsAiDescriptionLang.Tag, tag);
    }

    [Fact]
    public void GetLanguageTag_WithUiAutomationLang_ReturnsUiAutomationTag()
    {
        UiAutomationLang uiAutomationLang = new();

        string tag = LanguageService.GetLanguageTag(uiAutomationLang);

        Assert.Equal(UiAutomationLang.Tag, tag);
    }

    [Fact]
    public void GetLanguageTag_WithTessLang_ReturnsRawTag()
    {
        TessLang tessLang = new("eng");

        string tag = LanguageService.GetLanguageTag(tessLang);

        Assert.Equal("eng", tag);
    }

    [Fact]
    public void GetLanguageTag_WithLanguage_ReturnsLanguageTag()
    {
        Language language = new("en-US");

        string tag = LanguageService.GetLanguageTag(language);

        Assert.Equal("en-US", tag);
    }

    [Fact]
    public void GetLanguageKind_WithGlobalLang_ReturnsGlobal()
    {
        GlobalLang globalLang = new("en-US");

        LanguageKind kind = LanguageService.GetLanguageKind(globalLang);

        Assert.Equal(LanguageKind.Global, kind);
    }

    [Fact]
    public void GetLanguageKind_WithWindowsAiLang_ReturnsWindowsAi()
    {
        WindowsAiLang windowsAiLang = new();

        LanguageKind kind = LanguageService.GetLanguageKind(windowsAiLang);

        Assert.Equal(LanguageKind.WindowsAi, kind);
    }

    [Fact]
    public void GetLanguageKind_WithWindowsAiDescriptionLang_ReturnsWindowsAiDescription()
    {
        WindowsAiDescriptionLang windowsAiDescriptionLang = new();

        LanguageKind kind = LanguageService.GetLanguageKind(windowsAiDescriptionLang);

        Assert.Equal(LanguageKind.WindowsAiDescription, kind);
    }

    [Fact]
    public void GetLanguageKind_WithUiAutomationLang_ReturnsUiAutomation()
    {
        UiAutomationLang uiAutomationLang = new();

        LanguageKind kind = LanguageService.GetLanguageKind(uiAutomationLang);

        Assert.Equal(LanguageKind.UiAutomation, kind);
    }

    [Fact]
    public void GetLanguageKind_WithTessLang_ReturnsTesseract()
    {
        TessLang tessLang = new("eng");

        LanguageKind kind = LanguageService.GetLanguageKind(tessLang);

        Assert.Equal(LanguageKind.Tesseract, kind);
    }

    [Fact]
    public void GetLanguageKind_WithLanguage_ReturnsGlobal()
    {
        Language language = new("en-US");

        LanguageKind kind = LanguageService.GetLanguageKind(language);

        Assert.Equal(LanguageKind.Global, kind);
    }

    [Fact]
    public void GetLanguageKind_WithUnknownType_ReturnsGlobal()
    {
        object unknownLang = "some string";

        LanguageKind kind = LanguageService.GetLanguageKind(unknownLang);

        Assert.Equal(LanguageKind.Global, kind);
    }

    [Fact]
    public void GetPersistedLanguageIdentity_ForUiAutomationUsesRollbackSafeGlobalLanguage()
    {
        (string languageTag, LanguageKind languageKind, bool usedUiAutomation) =
            LanguageService.GetPersistedLanguageIdentity(new UiAutomationLang());

        Assert.True(usedUiAutomation);
        Assert.Equal(LanguageKind.Global, languageKind);
        Assert.NotEqual(UiAutomationLang.Tag, languageTag);
    }

    [Fact]
    public void GetOCRLanguage_WhenUiAutomationWasLastUsedButFeatureIsDisabled_FallsBack()
    {
        Settings.Default.UiAutomationEnabled = false;
        Settings.Default.LastUsedLang = UiAutomationLang.Tag;
        Settings.Default.Save();
        LanguageUtilities.InvalidateAllCaches();

        ILanguage language = Singleton<LanguageService>.Instance.GetOCRLanguage();

        Assert.IsNotType<UiAutomationLang>(language);
    }

    [Fact]
    public void GetOCRLanguage_WhenWindowsAiDescriptionWasLastUsedButFeatureIsDisabled_FallsBack()
    {
        Settings.Default.WindowsAiDescriptionEnabled = false;
        Settings.Default.LastUsedLang = WindowsAiDescriptionLang.Tag;
        Settings.Default.Save();
        LanguageUtilities.InvalidateAllCaches();

        ILanguage language = Singleton<LanguageService>.Instance.GetOCRLanguage();

        Assert.IsNotType<WindowsAiDescriptionLang>(language);
    }

    [Fact]
    public void LanguageService_IsSingleton()
    {
        LanguageService instance1 = Singleton<LanguageService>.Instance;
        LanguageService instance2 = Singleton<LanguageService>.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void LanguageUtilities_DelegatesTo_LanguageService()
    {
        GlobalLang globalLang = new("en-US");
        string tag = LanguageUtilities.GetLanguageTag(globalLang);
        LanguageKind kind = LanguageUtilities.GetLanguageKind(globalLang);

        Assert.Equal("en-US", tag);
        Assert.Equal(LanguageKind.Global, kind);
    }

    [Fact]
    public void HistoryInfo_OcrLanguage_FallsBackForUiAutomationPersistence()
    {
        HistoryInfo historyInfo = new()
        {
            LanguageTag = UiAutomationLang.Tag,
            LanguageKind = LanguageKind.UiAutomation,
        };

        Assert.IsNotType<UiAutomationLang>(historyInfo.OcrLanguage);
    }

    [Fact]
    public void HistoryInfo_OcrLanguage_ReturnsWindowsAiDescriptionLanguage()
    {
        HistoryInfo historyInfo = new()
        {
            LanguageTag = WindowsAiDescriptionLang.Tag,
            LanguageKind = LanguageKind.WindowsAiDescription,
        };

        Assert.IsType<WindowsAiDescriptionLang>(historyInfo.OcrLanguage);
    }
}
