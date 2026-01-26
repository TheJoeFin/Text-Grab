using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.Globalization;

namespace Tests;

public class LanguageServiceTests
{
    [Fact]
    public void GetLanguageTag_WithGlobalLang_ReturnsCorrectTag()
    {
        // Arrange
        GlobalLang globalLang = new("en-US");

        // Act
        string tag = LanguageService.GetLanguageTag(globalLang);

        // Assert
        Assert.Equal("en-US", tag);
    }

    [Fact]
    public void GetLanguageTag_WithWindowsAiLang_ReturnsWinAI()
    {
        // Arrange
        WindowsAiLang windowsAiLang = new();

        // Act
        string tag = LanguageService.GetLanguageTag(windowsAiLang);

        // Assert
        Assert.Equal("WinAI", tag);
    }

    [Fact]
    public void GetLanguageTag_WithTessLang_ReturnsRawTag()
    {
        // Arrange
        TessLang tessLang = new("eng");

        // Act
        string tag = LanguageService.GetLanguageTag(tessLang);

        // Assert
        Assert.Equal("eng", tag);
    }

    [Fact]
    public void GetLanguageTag_WithLanguage_ReturnsLanguageTag()
    {
        // Arrange
        Language language = new("en-US");

        // Act
        string tag = LanguageService.GetLanguageTag(language);

        // Assert
        Assert.Equal("en-US", tag);
    }

    [Fact]
    public void GetLanguageKind_WithGlobalLang_ReturnsGlobal()
    {
        // Arrange
        GlobalLang globalLang = new("en-US");

        // Act
        LanguageKind kind = LanguageService.GetLanguageKind(globalLang);

        // Assert
        Assert.Equal(LanguageKind.Global, kind);
    }

    [Fact]
    public void GetLanguageKind_WithWindowsAiLang_ReturnsWindowsAi()
    {
        // Arrange
        WindowsAiLang windowsAiLang = new();

        // Act
        LanguageKind kind = LanguageService.GetLanguageKind(windowsAiLang);

        // Assert
        Assert.Equal(LanguageKind.WindowsAi, kind);
    }

    [Fact]
    public void GetLanguageKind_WithTessLang_ReturnsTesseract()
    {
        // Arrange
        TessLang tessLang = new("eng");

        // Act
        LanguageKind kind = LanguageService.GetLanguageKind(tessLang);

        // Assert
        Assert.Equal(LanguageKind.Tesseract, kind);
    }

    [Fact]
    public void GetLanguageKind_WithLanguage_ReturnsGlobal()
    {
        // Arrange
        Language language = new("en-US");

        // Act
        LanguageKind kind = LanguageService.GetLanguageKind(language);

        // Assert
        Assert.Equal(LanguageKind.Global, kind);
    }

    [Fact]
    public void GetLanguageKind_WithUnknownType_ReturnsGlobal()
    {
        // Arrange
        object unknownLang = "some string";

        // Act
        LanguageKind kind = LanguageService.GetLanguageKind(unknownLang);

        // Assert
        Assert.Equal(LanguageKind.Global, kind); // Default fallback
    }

    [Fact]
    public void LanguageService_IsSingleton()
    {
        // Act
        var instance1 = Singleton<LanguageService>.Instance;
        var instance2 = Singleton<LanguageService>.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void LanguageUtilities_DelegatesTo_LanguageService()
    {
        // This test ensures backward compatibility - static methods should work
        // Arrange & Act
        var globalLang = new GlobalLang("en-US");
        string tag = LanguageUtilities.GetLanguageTag(globalLang);
        LanguageKind kind = LanguageUtilities.GetLanguageKind(globalLang);

        // Assert
        Assert.Equal("en-US", tag);
        Assert.Equal(LanguageKind.Global, kind);
    }
}
