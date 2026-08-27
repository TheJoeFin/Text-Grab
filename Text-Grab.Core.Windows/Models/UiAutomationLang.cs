using Text_Grab.Interfaces;
using Windows.Globalization;

namespace Text_Grab.Models;

public class UiAutomationLang : ILanguage
{
    public const string Tag = "Direct-Txt";
    public const string BetaDisplayName = "Direct Text (Beta)";

    public string AbbreviatedName => "DT";

    public string DisplayName => BetaDisplayName;

    public string CurrentInputMethodLanguageTag => string.Empty;

    public string CultureDisplayName => BetaDisplayName;

    public string LanguageTag => Tag;

    public LanguageLayoutDirection LayoutDirection => LanguageLayoutDirection.Ltr;

    public string NativeName => BetaDisplayName;

    public string Script => string.Empty;
}
