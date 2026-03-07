using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Windows.Media.Ocr;

namespace Text_Grab.Utilities;

internal static class CaptureLanguageUtilities
{
    public static async Task<List<ILanguage>> GetCaptureLanguagesAsync(bool includeTesseract)
    {
        List<ILanguage> languages = [];

        if (AppUtilities.TextGrabSettings.UiAutomationEnabled)
            languages.Add(new UiAutomationLang());

        if (WindowsAiUtilities.CanDeviceUseWinAI())
            languages.Add(new WindowsAiLang());

        if (includeTesseract
            && AppUtilities.TextGrabSettings.UseTesseract
            && TesseractHelper.CanLocateTesseractExe())
        {
            languages.AddRange(await TesseractHelper.TesseractLanguages());
        }

        foreach (Windows.Globalization.Language language in OcrEngine.AvailableRecognizerLanguages)
            languages.Add(new GlobalLang(language));

        return languages;
    }

    public static bool MatchesPersistedLanguage(ILanguage language, string persistedLanguage)
    {
        if (string.IsNullOrWhiteSpace(persistedLanguage))
            return false;

        return string.Equals(language.LanguageTag, persistedLanguage, StringComparison.CurrentCultureIgnoreCase)
            || string.Equals(language.CultureDisplayName, persistedLanguage, StringComparison.CurrentCultureIgnoreCase)
            || string.Equals(language.DisplayName, persistedLanguage, StringComparison.CurrentCultureIgnoreCase);
    }

    public static int FindPreferredLanguageIndex(IReadOnlyList<ILanguage> languages, string persistedLanguage, ILanguage fallbackLanguage)
    {
        for (int i = 0; i < languages.Count; i++)
        {
            if (MatchesPersistedLanguage(languages[i], persistedLanguage))
                return i;
        }

        for (int i = 0; i < languages.Count; i++)
        {
            if (string.Equals(languages[i].LanguageTag, fallbackLanguage.LanguageTag, StringComparison.CurrentCultureIgnoreCase))
                return i;
        }

        return languages.Count > 0 ? 0 : -1;
    }

    public static void PersistSelectedLanguage(ILanguage language)
    {
        AppUtilities.TextGrabSettings.LastUsedLang = language.LanguageTag;
        AppUtilities.TextGrabSettings.Save();
        LanguageUtilities.InvalidateOcrLanguageCache();
    }

    public static ILanguage GetUiAutomationFallbackLanguage()
    {
        ILanguage currentInputLanguage = LanguageUtilities.GetCurrentInputLanguage();

        return currentInputLanguage as GlobalLang ?? new GlobalLang(currentInputLanguage.LanguageTag);
    }

    public static bool SupportsTableOutput(ILanguage language)
        => language is not TessLang && language is not UiAutomationLang;

    public static bool IsStaticImageCompatible(ILanguage language)
        => language is not UiAutomationLang;
}
