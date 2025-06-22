using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Markup;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Windows.Globalization;

namespace Text_Grab;

public static class LanguageExtensions
{
    public static bool IsSpaceJoining(this Language selectedLanguage)
    {
        if (selectedLanguage.LanguageTag.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase))
            return false;
        else if (selectedLanguage.LanguageTag.Equals("ja", StringComparison.InvariantCultureIgnoreCase))
            return false;
        return true;
    }

    public static bool IsRightToLeft(this Language language)
    {
        XmlLanguage lang = XmlLanguage.GetLanguage(language.LanguageTag);
        CultureInfo culture = lang.GetEquivalentCulture();
        return culture.TextInfo.IsRightToLeft;
    }

    public static bool IsSpaceJoining(this ILanguage selectedLanguage)
    {
        if (selectedLanguage.LanguageTag.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase))
            return false;
        else if (selectedLanguage.LanguageTag.Equals("ja", StringComparison.InvariantCultureIgnoreCase))
            return false;
        return true;
    }

    public static bool IsRightToLeft(this ILanguage selectedLanguage)
    {
        if (selectedLanguage is GlobalLang language)
            return language.OriginalLanguage.IsRightToLeft();

        // For other language types, use the LayoutDirection property
        return selectedLanguage.LayoutDirection == LanguageLayoutDirection.Rtl;
    }

    public static bool IsLatinBased(this ILanguage selectedLanguage)
    {
        // List of Latin-based languages
        List<string> LatinLanguages =
        [
            "en",  // English
            "es",  // Spanish
            "fr",  // French
            "it",  // Italian
            "ro",  // Romanian
            "pt"   // Portuguese
        ];

        string languageTag = selectedLanguage.LanguageTag;

        return LatinLanguages.Any(lang => languageTag.StartsWith(lang, StringComparison.InvariantCultureIgnoreCase));
    }

    public static Language? AsLanguage(this ILanguage iLanguage)
    {
        if (iLanguage is GlobalLang language)
            return language.OriginalLanguage;

        string tag = iLanguage.LanguageTag;

        try
        {
            return new Language(tag);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static ILanguage? AsILanguage(this Language language)
    {
        if (language is null)
            return null;
        return new GlobalLang(language);
    }
}
