using Text_Grab.Interfaces;
using Windows.Globalization;

namespace Text_Grab.Models;
public class WindowsAiLang : ILanguage
{
    public string AbbreviatedName => "WinAI";

    public string DisplayName => "Windows AI OCR 🆕";

    public string CurrentInputMethodLanguageTag => string.Empty;

    public string CultureDisplayName => "Windows AI OCR";

    public string LanguageTag => "WinAI";

    public LanguageLayoutDirection LayoutDirection => LanguageLayoutDirection.Ltr;

    public string NativeName => "Windows AI OCR";

    public string Script => string.Empty;
}
