using Text_Grab.Interfaces;
using Windows.Globalization;

namespace Text_Grab.Models;

public class WindowsAiDescriptionLang : ILanguage
{
    public const string Tag = "WinAI-Desc";
    public const string DisplayLabel = "Windows AI Description";

    public string AbbreviatedName => "WinAI Desc";

    public string DisplayName => DisplayLabel;

    public string CurrentInputMethodLanguageTag => string.Empty;

    public string CultureDisplayName => DisplayLabel;

    public string LanguageTag => Tag;

    public LanguageLayoutDirection LayoutDirection => LanguageLayoutDirection.Ltr;

    public string NativeName => DisplayLabel;

    public string Script => string.Empty;
}
