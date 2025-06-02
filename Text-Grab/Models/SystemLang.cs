using Text_Grab.Interfaces;

namespace Text_Grab.Models;
internal class GlobalLang : ILanguage
{
    public GlobalLang(Windows.Globalization.Language lang)
    {
        AbbreviatedName = lang.AbbreviatedName;
        CultureDisplayName = lang.DisplayName;
        LanguageTag = lang.LanguageTag;
        LayoutDirection = lang.LayoutDirection;
        NativeName = lang.NativeName;
        Script = lang.Script;
    }

    public string AbbreviatedName { get; set; }

    public string CurrentInputMethodLanguageTag { get; set; } = string.Empty;

    public string CultureDisplayName { get; set; }

    public string LanguageTag { get; set; }

    public Windows.Globalization.LanguageLayoutDirection LayoutDirection { get; set; }

    public string NativeName { get; set; }

    public string Script { get; set; }

    public string DisplayName => CultureDisplayName;
}
