using Text_Grab.Interfaces;
using Windows.Globalization;

namespace Text_Grab.Models;

public class UiAutomationLang : ILanguage
{
    public const string Tag = "UIAutomation";

    public string AbbreviatedName => "UIA";

    public string DisplayName => "UI Automation Text";

    public string CurrentInputMethodLanguageTag => string.Empty;

    public string CultureDisplayName => "UI Automation Text";

    public string LanguageTag => Tag;

    public LanguageLayoutDirection LayoutDirection => LanguageLayoutDirection.Ltr;

    public string NativeName => "UI Automation Text";

    public string Script => string.Empty;
}
