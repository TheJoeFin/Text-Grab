using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Text_Grab.Utilities;

public static class LanguageUtilities
{
    public static ILanguage GetCurrentInputLanguage()
    {
        // use currently selected Language
        string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;
        return new GlobalLang(inputLang);
    }

    public static IList<ILanguage> GetAllLanguages()
    {
        List<ILanguage> languages = [];

        if (WindowsAiUtilities.CanDeviceUseWinAI())
        {
            // Add Windows AI languages
            languages.Add(new WindowsAiLang());
        }

        foreach (Language lang in OcrEngine.AvailableRecognizerLanguages)
        {
            // Wrap Windows.Globalization.Language in a compatible ILanguage implementation
            languages.Add(new GlobalLang(lang));
        }

        return languages;
    }

    public static string GetLanguageTag(object language)
    {
        return language switch
        {
            Language lang => lang.LanguageTag,
            WindowsAiLang => "WinAI",
            TessLang tessLang => tessLang.RawTag,
            GlobalLang gLang => gLang.LanguageTag,
            _ => throw new ArgumentException("Unsupported language type", nameof(language)),
        };
    }

    public static LanguageKind GetLanguageKind(object language)
    {
        return language switch
        {
            Language => LanguageKind.Global,
            WindowsAiLang => LanguageKind.WindowsAi,
            TessLang => LanguageKind.Tesseract,
            _ => LanguageKind.Global, // Default fallback
        };
    }

    public static ILanguage GetOCRLanguage()
    {
        string lastUsedLang = AppUtilities.TextGrabSettings.LastUsedLang;

        ILanguage selectedLanguage = GetCurrentInputLanguage();

        if (!string.IsNullOrEmpty(lastUsedLang))
        {
            if (lastUsedLang == new WindowsAiLang().LanguageTag)
            {
                // If the last used language is Windows AI, return it directly
                return new WindowsAiLang();
            }

            try
            {
                selectedLanguage = new GlobalLang(AppUtilities.TextGrabSettings.LastUsedLang);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to parse LastUsedLang: {AppUtilities.TextGrabSettings.LastUsedLang}\n{ex.Message}");

                // if the language tag is invalid, reset to current input language
                selectedLanguage = GetCurrentInputLanguage();
            }
        }

        List<ILanguage> possibleOCRLanguages = [.. GetAllLanguages()];

        if (possibleOCRLanguages.Count == 0)
            return new GlobalLang("en-US");

        // check to see if the selected language is in the list of available OCR languages
        if (possibleOCRLanguages.All(l => l.LanguageTag != selectedLanguage.LanguageTag))
        {
            List<ILanguage>? similarLanguages = [.. possibleOCRLanguages.Where(
                la => la.LanguageTag.Contains(selectedLanguage.LanguageTag)
                || selectedLanguage.LanguageTag.Contains(la.LanguageTag)
            )];

            if (similarLanguages is not null && similarLanguages.Count > 0)
                return new GlobalLang(similarLanguages.First().LanguageTag);
            else
                return new GlobalLang(possibleOCRLanguages.First().LanguageTag);
        }

        return selectedLanguage ?? new GlobalLang("en-US");
    }

    public static bool IsCurrentLanguageLatinBased()
    {
        ILanguage lang = GetCurrentInputLanguage();
        return lang.IsLatinBased();
    }
}
