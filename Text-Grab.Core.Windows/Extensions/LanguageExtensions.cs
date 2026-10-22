using System;
using System.Globalization;
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

    /// <summary>
    /// Whether text in this language reads right-to-left.
    ///
    /// The GlobalLang branch used to delegate to an overload taking
    /// <c>Windows.Globalization.Language</c>, which resolved the tag through
    /// <c>XmlLanguage.GetLanguage(tag).GetEquivalentCulture()</c>. XmlLanguage comes from
    /// PresentationCore, which is why batch 3d had to leave both overloads in the app. Batch 4c
    /// needed this one in Core.Windows for BuildTextFromOcrLines, so the tag is now resolved with
    /// CultureInfo directly. The two were probed against 24 tags - ar, ar-EG, ar-SA, he, he-IL,
    /// ur, ur-PK, fa, fa-IR, ckb, ps-AF, sd-Arab-PK, yi, he-Hebr-IL, ar-XX, en, en-US, ja,
    /// zh-Hans, de-DE, and the unresolvable xx, xx-YY, und and "" - and agreed on every one.
    /// </summary>
    public static bool IsRightToLeft(this ILanguage selectedLanguage)
    {
        if (selectedLanguage is GlobalLang language)
            return IsRightToLeftTag(language.OriginalLanguage.LanguageTag);

        // For other language types, use the LayoutDirection property
        return selectedLanguage.LayoutDirection == LanguageLayoutDirection.Rtl;
    }

    private static bool IsRightToLeftTag(string languageTag)
    {
        try
        {
            return CultureInfo.GetCultureInfo(languageTag).TextInfo.IsRightToLeft;
        }
        catch (CultureNotFoundException)
        {
            // XmlLanguage fell back to the invariant culture, which is left-to-right, for tags
            // it could not resolve. Keep that behaviour rather than throwing at a call site that
            // only wanted to know which way to order words.
            return false;
        }
    }
}
