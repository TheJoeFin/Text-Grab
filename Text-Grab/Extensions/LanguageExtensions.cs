using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Markup;
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

    public static bool IsLatinBased(this Language language)
    {
        // List of Latin-based languages
        List<string> LatinLanguages = new List<string>()
        {
            "en",  // English
            "es",  // Spanish
            "fr",  // French
            "it",  // Italian
            "ro",  // Romanian
            "pt"   // Portuguese
        };

        // Get the abbreviated name of the culture
        string abbreviatedName = language.AbbreviatedName.ToLowerInvariant();

        // Check if the abbreviated name of the culture is in the list of Latin-based languages
        return LatinLanguages.Contains(abbreviatedName);
    }
}
