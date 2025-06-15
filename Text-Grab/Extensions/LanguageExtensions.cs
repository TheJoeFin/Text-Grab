using System;
using System.Collections.Generic;
using System.Globalization;
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

    // Extension methods for ILanguage interface
    public static bool IsSpaceJoining(this ILanguage selectedLanguage)
    {
        if (selectedLanguage is GlobalLang language)
            return language.IsSpaceJoining();

        // For other language types, use the LanguageTag property
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
        if (selectedLanguage is GlobalLang language)
            return language.IsLatinBased();

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

        // Get the abbreviated name of the culture
        string abbreviatedName = selectedLanguage.AbbreviatedName.ToLowerInvariant();

        // Check if the abbreviated name of the culture is in the list of Latin-based languages
        return LatinLanguages.Contains(abbreviatedName);
    }

    // Helper method to convert ILanguage to Language when needed
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
