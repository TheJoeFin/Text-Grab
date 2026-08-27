using System.Globalization;
using System.Windows.Markup;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Windows.Globalization;

namespace Text_Grab;

/// <summary>
/// The WPF-bound half of what used to be Extensions/LanguageExtensions.cs. Split out because
/// <see cref="XmlLanguage"/> comes from PresentationCore, so it cannot cross into
/// Text-Grab.Core.Windows (see docs/Core-Split-Plan.md, batch 3d). The portable members
/// (IsSpaceJoining, IsLatinBased, AsLanguage, AsILanguage) moved to Core.Windows keeping the
/// original LanguageExtensions type name so existing call sites resolve unchanged; these two
/// overloads stay here under a distinct name to avoid a same-named-extension-method ambiguity
/// (CS0121) between the two assemblies.
/// </summary>
public static class LanguageRtlExtensions
{
    public static bool IsRightToLeft(this Language language)
    {
        XmlLanguage lang = XmlLanguage.GetLanguage(language.LanguageTag);
        CultureInfo culture = lang.GetEquivalentCulture();
        return culture.TextInfo.IsRightToLeft;
    }

    public static bool IsRightToLeft(this ILanguage selectedLanguage)
    {
        if (selectedLanguage is GlobalLang language)
            return language.OriginalLanguage.IsRightToLeft();

        // For other language types, use the LayoutDirection property
        return selectedLanguage.LayoutDirection == LanguageLayoutDirection.Rtl;
    }
}
