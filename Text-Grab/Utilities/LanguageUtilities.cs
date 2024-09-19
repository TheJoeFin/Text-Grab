using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Text_Grab.Utilities;

public static class LanguageUtilities
{
    public static Language GetCurrentInputLanguage()
    {
        // use currently selected Language
        string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;
        return new(inputLang);
    }

    public static Language GetOCRLanguage()
    {
        Language selectedLanguage = GetCurrentInputLanguage();

        if (!string.IsNullOrEmpty(AppUtilities.TextGrabSettings.LastUsedLang))
        {
            try
            {
                selectedLanguage = new(AppUtilities.TextGrabSettings.LastUsedLang);
            }
            catch
            {
                selectedLanguage = GetCurrentInputLanguage();
            }
        }

        List<Language> possibleOCRLanguages = OcrEngine.AvailableRecognizerLanguages.ToList();

        if (possibleOCRLanguages.Count == 0)
        {
            System.Windows.MessageBox.Show("No possible OCR languages are installed.", "Text Grab");
            throw new Exception("No possible OCR languages are installed");
        }

        // If the selected input language or last used language is not a possible OCR Language
        // then we need to find a similar language to use
        if (possibleOCRLanguages.All(l => l.LanguageTag != selectedLanguage.LanguageTag))
        {
            List<Language> similarLanguages = possibleOCRLanguages.Where(
                la => la.AbbreviatedName == selectedLanguage.AbbreviatedName).ToList();

            if (similarLanguages is not null && similarLanguages.Count > 0)
                selectedLanguage = similarLanguages.First();
            else
                selectedLanguage = possibleOCRLanguages.First();
        }

        return selectedLanguage;
    }

    public static bool IsCurrentLanguageLatinBased()
    {
        Language lang = GetCurrentInputLanguage();
        return lang.IsLatinBased();
    }
}