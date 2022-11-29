using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using System.Windows.Markup;
using Text_Grab.Properties;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Text_Grab.Utilities;

public static class LanguageUtilities
{

    public static bool IsLanguageSpaceJoining(Language selectedLanguage)
    {
        if (selectedLanguage.LanguageTag.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase))
            return false;
        else if (selectedLanguage.LanguageTag.Equals("ja", StringComparison.InvariantCultureIgnoreCase))
            return false;
        return true;
    }

    public static bool IsLanguageRightToLeft(Language language)
    {
        XmlLanguage lang = XmlLanguage.GetLanguage(language.LanguageTag);
        CultureInfo culture = lang.GetEquivalentCulture();
        return culture.TextInfo.IsRightToLeft;
    }

    public static Language GetCurrentInputLanguage()
    {
        // use currently selected Language
        string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;
        return new(inputLang);
    }

    public static Language GetOCRLanguage()
    {
        Language selectedLanguage = GetCurrentInputLanguage();

        if (!string.IsNullOrEmpty(Settings.Default.LastUsedLang))
            selectedLanguage = new(Settings.Default.LastUsedLang);

        List<Language> possibleOCRLangs = OcrEngine.AvailableRecognizerLanguages.ToList();

        if (possibleOCRLangs.Count == 0)
        {
            System.Windows.MessageBox.Show("No possible OCR languages are installed.", "Text Grab");
            throw new Exception("No possible OCR languages are installed");
        }

        // If the selected input language or last used language is not a possible OCR Language
        // then we need to find a similar language to use
        if (possibleOCRLangs.All(l => l.LanguageTag != selectedLanguage.LanguageTag))
        {
            List<Language> similarLanguages = possibleOCRLangs.Where(
                la => la.AbbreviatedName == selectedLanguage.AbbreviatedName).ToList();

            if (similarLanguages is not null && similarLanguages.Count > 0)
                selectedLanguage = similarLanguages.First();
            else
                selectedLanguage = possibleOCRLangs.First();
        }

        return selectedLanguage;
    }
}