using Text_Grab.Interfaces;

namespace Text_Grab.Models;

public class GlobalLang : ILanguage
{
    public GlobalLang(Windows.Globalization.Language lang)
    {
        AbbreviatedName = lang.AbbreviatedName;
        CultureDisplayName = lang.DisplayName;
        LanguageTag = lang.LanguageTag;
        LayoutDirection = lang.LayoutDirection;
        NativeName = lang.NativeName;
        Script = lang.Script;
        OriginalLanguage = lang;
    }

    public GlobalLang(string inputLang)
    {
        if (inputLang == "English")
            inputLang = "en-US";

        Windows.Globalization.Language language = new(System.Globalization.CultureInfo.CurrentCulture.Name);
        try
        {
            language = new(inputLang);
        }
        catch (System.ArgumentException)
        {

        }
        AbbreviatedName = language.AbbreviatedName;
        CultureDisplayName = language.DisplayName;
        LanguageTag = language.LanguageTag;
        LayoutDirection = language.LayoutDirection;
        NativeName = language.NativeName;
        Script = language.Script;
        OriginalLanguage = language;
    }

    public Windows.Globalization.Language OriginalLanguage { get; set; }

    public string AbbreviatedName { get; set; }

    public string CurrentInputMethodLanguageTag { get; set; } = string.Empty;

    public string CultureDisplayName { get; set; }

    public string LanguageTag { get; set; }

    public Windows.Globalization.LanguageLayoutDirection LayoutDirection { get; set; }

    public string NativeName { get; set; }

    public string Script { get; set; }

    public string DisplayName => CultureDisplayName;
}
