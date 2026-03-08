using Text_Grab.Interfaces;
using Windows.Globalization;

namespace Text_Grab.Models;

public class UiAutomationLang : ILanguage
{
    public const string Tag = "UIAutomation";

    public string AbbreviatedName => "DT";

    public string DisplayName => "Direct Text";

    public string CurrentInputMethodLanguageTag => string.Empty;

    public string CultureDisplayName => "Direct Text";

    public string LanguageTag => Tag;

    public LanguageLayoutDirection LayoutDirection => LanguageLayoutDirection.Ltr;

    public string NativeName => "Direct Text";

    public string Script => string.Empty;
}
