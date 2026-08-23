using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

internal static class CaptureLanguageUtilities
{
    /// <summary>
    /// Builds the language list for capture menus. The UI-automation / Windows AI / plain-OCR
    /// portion comes from <see cref="LanguageUtilities.GetAllLanguages"/>, which caches those
    /// checks (each of which can be a genuinely slow WinRT/WinAppSDK probe) instead of redoing
    /// them on every call — this used to duplicate that work uncached, which is what made menus
    /// like EditTextWindow's "Capture" menu slow to open, especially in new windows.
    /// </summary>
    public static async Task<List<ILanguage>> GetCaptureLanguagesAsync(bool includeTesseract)
    {
        List<ILanguage> languages = [.. LanguageUtilities.GetAllLanguages()];

        if (includeTesseract
            && AppUtilities.TextGrabSettings.UseTesseract
            && TesseractHelper.CanLocateTesseractExe())
        {
            List<ILanguage> tesseractLanguages = await TesseractHelper.TesseractLanguages();

            // Insert before the plain OCR languages (GlobalLang), after the UiAutomation/WindowsAi
            // pseudo-languages, to preserve the original ordering.
            int insertIndex = languages.FindIndex(l => l is GlobalLang);
            languages.InsertRange(insertIndex < 0 ? languages.Count : insertIndex, tesseractLanguages);
        }

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
        => language is not TessLang && language is not UiAutomationLang && language is not WindowsAiDescriptionLang;

    public static bool IsStaticImageCompatible(ILanguage language)
        => language is not UiAutomationLang;

    public static bool RequiresLiveUiAutomationSource(ILanguage language, bool isStaticImageSource, bool hasFrozenUiAutomationSnapshot)
        => language is UiAutomationLang && isStaticImageSource && !hasFrozenUiAutomationSnapshot;
}
