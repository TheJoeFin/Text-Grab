
using System.Collections.Generic;
using Windows.Globalization;

namespace Text_Grab.Interfaces;

public interface ILanguage
{
    public string AbbreviatedName { get; }

    public string CurrentInputMethodLanguageTag { get; }

    public string CultureDisplayName { get; }

    public string LanguageTag { get; }

    public LanguageLayoutDirection LayoutDirection { get; }

    public string NativeName { get; }

    public string Script { get; }


    public static bool TrySetInputMethodLanguageTag(string languageTag)
    {
        return false;
    }

    public static bool IsWellFormed(string languageTag)
    {
        return true;
    }

    public static IList<string> GetMuiCompatibleLanguageListFromLanguageTags(IEnumerable<string> languageTags)
    {
        return new List<string>();
    }

    public IReadOnlyList<string> GetExtensionSubtags(string singleton)
    {
        return new List<string>();
    }
}
