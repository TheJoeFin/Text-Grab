using System.Globalization;
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
}
