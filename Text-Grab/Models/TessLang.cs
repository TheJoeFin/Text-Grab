using System.Globalization;
using Text_Grab.Interfaces;

namespace Text_Grab.Models;

public class TessLang : ILanguage
{
    private readonly string _tessLangTag;

    private readonly CultureInfo cultureInfo;

    private readonly bool isScript = false;

    public TessLang(string tessLangTag)
    {
        RawTag = tessLangTag;
        string cultureTag = tessLangTag;

        if (tessLangTag.Contains("script"))
        {
            isScript = true;
            cultureTag = cultureTag.Replace("script\\", "");
        }

        if (tessLangTag.Contains("vert"))
        {
            IsVertical = true;
            cultureTag = cultureTag.Replace("_vert", "");
        }
        cultureInfo = GetCultureInfoFromTesseractTag(cultureTag);

        _tessLangTag = tessLangTag;
    }

    private CultureInfo GetCultureInfoFromTesseractTag(string tessLangTag)
    {
        tessLangTag = tessLangTag.Replace("_frak", "");
        tessLangTag = tessLangTag.Replace("_old", "");
        tessLangTag = tessLangTag.Replace("_latn", "");

        if (isScript)
            tessLangTag = getTagFromEnglishName(tessLangTag);

        try
        {
            CultureInfo matchedInfo = tessLangTag switch
            {
                "chi_sim" => new CultureInfo("zh-Hans"),
                "chi_tra" => new CultureInfo("zh-Hant"),
                _ => new CultureInfo(tessLangTag)
            };
            return matchedInfo;
        }
        catch
        {
            string tag = getTagFromEnglishName(tessLangTag);

            return new CultureInfo(tag);
        }
    }

    private string getTagFromEnglishName(string EnglishName)
    {
        foreach (CultureInfo info in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            if (info.EnglishName.Equals(EnglishName, System.StringComparison.InvariantCultureIgnoreCase))
            {
                return info.IetfLanguageTag;
            }
        }

        return string.Empty;
    }

    public string AbbreviatedName => _tessLangTag;

    public bool IsVertical { get; set; } = false;

    public string CurrentInputMethodLanguageTag => string.Empty;

    public string CultureDisplayName
    {
        get
        {
            if (_tessLangTag == "dan_frak")
                return $"{cultureInfo.DisplayName} (Fraktur)";

            if (_tessLangTag == "deu_frak")
                return $"{cultureInfo.DisplayName} (Fraktur)";

            if (_tessLangTag == "ita_old")
                return $"{cultureInfo.DisplayName} (Old)";

            if (_tessLangTag == "kat_old")
                return $"{cultureInfo.DisplayName} (Old)";

            if (_tessLangTag == "slk_frak")
                return $"{cultureInfo.DisplayName} (Fraktur)";

            if (_tessLangTag == "spa_old")
                return $"{cultureInfo.DisplayName} (Old)";

            if (_tessLangTag == "srp_latn")
                return $"{cultureInfo.DisplayName} (Latin)";

            if (IsVertical)
                return $"{cultureInfo.DisplayName} Vertical";

            if (isScript)
                return $"{cultureInfo.DisplayName} Script";

            return $"{cultureInfo.DisplayName}";
        }
    }

    public string DisplayName => $"{CultureDisplayName} with Tesseract";

    public Windows.Globalization.LanguageLayoutDirection LayoutDirection
    {
        get
        {
            if (_tessLangTag.Contains("vert"))
                return Windows.Globalization.LanguageLayoutDirection.TtbRtl;

            return Windows.Globalization.LanguageLayoutDirection.Rtl;
        }
    }

    public string NativeName => cultureInfo.NativeName;

    public string Script => string.Empty;

    public string LanguageTag => RawTag;

    public string RawTag { get; set; } = string.Empty;
}
