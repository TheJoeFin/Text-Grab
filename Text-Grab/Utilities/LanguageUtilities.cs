using System.Collections.Generic;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>
/// Static utility class for language operations. 
/// Delegates to LanguageService singleton for cached operations to reduce memory allocations.
/// </summary>
public static class LanguageUtilities
{
    /// <summary>
    /// Gets the current input language from the cached service.
    /// </summary>
    public static ILanguage GetCurrentInputLanguage()
        => Singleton<LanguageService>.Instance.GetCurrentInputLanguage();

    /// <summary>
    /// Gets all available OCR languages from the cached service.
    /// </summary>
    public static IList<ILanguage> GetAllLanguages()
        => Singleton<LanguageService>.Instance.GetAllLanguages();

    /// <summary>
    /// Gets the language tag from a language object.
    /// </summary>
    public static string GetLanguageTag(object language)
        => LanguageService.GetLanguageTag(language);

    /// <summary>
    /// Gets the language kind from a language object.
    /// </summary>
    public static LanguageKind GetLanguageKind(object language)
        => LanguageService.GetLanguageKind(language);

    /// <summary>
    /// Gets the OCR language to use based on settings and available languages.
    /// Uses cached values when settings haven't changed.
    /// </summary>
    public static ILanguage GetOCRLanguage()
        => Singleton<LanguageService>.Instance.GetOCRLanguage();

    /// <summary>
    /// Checks if the current input language is Latin-based.
    /// </summary>
    public static bool IsCurrentLanguageLatinBased()
        => Singleton<LanguageService>.Instance.IsCurrentLanguageLatinBased();

    /// <summary>
    /// Gets the system language name suitable for Windows AI translation.
    /// Returns a user-friendly language name like "English", "Spanish", etc.
    /// </summary>
    /// <returns>Language name for translation, defaults to "English" if unable to determine</returns>
    public static string GetSystemLanguageForTranslation()
        => Singleton<LanguageService>.Instance.GetSystemLanguageForTranslation();

    /// <summary>
    /// Invalidates the cached languages list. Call this when new languages are installed.
    /// </summary>
    public static void InvalidateLanguagesCache()
        => Singleton<LanguageService>.Instance.InvalidateLanguagesCache();

    /// <summary>
    /// Invalidates the OCR language cache. Call this when LastUsedLang setting changes.
    /// </summary>
    public static void InvalidateOcrLanguageCache()
        => Singleton<LanguageService>.Instance.InvalidateOcrLanguageCache();

    /// <summary>
    /// Invalidates all caches. Call this when input language changes.
    /// </summary>
    public static void InvalidateAllCaches()
        => Singleton<LanguageService>.Instance.InvalidateAllCaches();
}
