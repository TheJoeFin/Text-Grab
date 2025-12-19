using System.Globalization;
using Text_Grab;
using Text_Grab.Models;

namespace Tests;

public class LanguageTests
{
    [Theory]
    [InlineData("zh-Hant")]
    [InlineData("zh-Hans")]
    public void CanParseEveryLanguageTag(string langTag)
    {
        CultureInfo culture = new(langTag);
        Assert.NotNull(culture);
    }

    [Theory]
    [InlineData("chi_sim", "Chinese (Simplified)")]
    [InlineData("chi_tra", "Chinese (Traditional)")]
    [InlineData("chi_sim_vert", "Chinese (Simplified) Vertical")]
    [InlineData("chi_tra_vert", "Chinese (Traditional) Vertical")]
    public void CanParseChineseLanguageTag(string langTag, string expectedDisplayName)
    {
        TessLang tessLang = new(langTag);
        Assert.Equal(expectedDisplayName, tessLang.CultureDisplayName);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("fr-FR")]
    [InlineData("it-IT")]
    [InlineData("ro-RO")]
    [InlineData("pt-BR")]
    public void IsLatinBased_WithLatinLanguages_ReturnsTrue(string languageTag)
    {
        // Arrange
        GlobalLang language = new(languageTag);
        TessLang tessLang = new(languageTag);

        // Act
        bool result = language.IsLatinBased();
        bool tessResult = tessLang.IsLatinBased();

        // Assert
        Assert.True(result);
        Assert.True(tessResult);
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("ja-JP")]
    [InlineData("ar-SA")]
    [InlineData("ru-RU")]
    [InlineData("hi-IN")]
    public void IsLatinBased_WithNonLatinLanguages_ReturnsFalse(string languageTag)
    {
        // Arrange
        GlobalLang language = new(languageTag);

        // Act
        bool result = language.IsLatinBased();

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("en-CA")]
    [InlineData("es-MX")]
    [InlineData("fr-CA")]
    [InlineData("pt-PT")]
    public void IsLatinBased_WithLatinLanguageVariants_ReturnsTrue(string languageTag)
    {
        // Arrange
        GlobalLang language = new(languageTag);

        // Act
        bool result = language.IsLatinBased();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLatinBased_WithMixedCaseLanguageTag_WorksCorrectly()
    {
        // Arrange
        GlobalLang language = new("En-us");

        // Act
        bool result = language.IsLatinBased();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLatinBased_WithWindowsAiLang_ReturnsFalse()
    {
        // Arrange
        WindowsAiLang windowsAiLang = new();
        // Act
        bool result = windowsAiLang.IsLatinBased();
        // Assert
        Assert.False(result);
    }
}
