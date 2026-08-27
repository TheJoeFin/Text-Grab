using System;
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

    public static bool IsSpaceJoining(this ILanguage selectedLanguage)
    {
        if (selectedLanguage.LanguageTag.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase))
            return false;
        else if (selectedLanguage.LanguageTag.Equals("ja", StringComparison.InvariantCultureIgnoreCase))
            return false;
        return true;
    }

    public static bool IsLatinBased(this ILanguage selectedLanguage)
    {
        return string.Equals(selectedLanguage.Script, "Latn", StringComparison.OrdinalIgnoreCase);
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
