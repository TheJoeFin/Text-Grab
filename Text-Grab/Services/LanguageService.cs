using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Text_Grab.Services;

/// <summary>
/// Service that provides cached access to language information for OCR operations.
/// Reduces memory allocations by caching language data instead of recreating it on every call.
/// </summary>
public class LanguageService
{
    #region Fields

    private IList<ILanguage>? _cachedAllLanguages;
    private ILanguage? _cachedCurrentInputLanguage;
    private string? _cachedCurrentInputLanguageTag;
    private string? _cachedSystemLanguageForTranslation;
    private string? _cachedLastUsedLang;
    private ILanguage? _cachedOcrLanguage;
    private readonly object _cacheLock = new();

    // Static instance of WindowsAiLang to avoid allocations
    private static readonly WindowsAiLang _windowsAiLangInstance = new();
    private static readonly string _windowsAiLangTag = _windowsAiLangInstance.LanguageTag;

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Gets the current input language. Cached until the input language changes.
    /// </summary>
    public ILanguage GetCurrentInputLanguage()
    {
        string currentInputLangTag = InputLanguageManager.Current.CurrentInputLanguage.Name;

        lock (_cacheLock)
        {
            // Return cached value if the input language hasn't changed
            if (_cachedCurrentInputLanguage is not null &&
                _cachedCurrentInputLanguageTag == currentInputLangTag)
            {
                return _cachedCurrentInputLanguage;
            }

            // Create and cache new language
            _cachedCurrentInputLanguageTag = currentInputLangTag;
            _cachedCurrentInputLanguage = new GlobalLang(currentInputLangTag);
            return _cachedCurrentInputLanguage;
        }
    }

    /// <summary>
    /// Gets all available OCR languages. Cached on first call.
    /// Call InvalidateLanguagesCache() if the available languages change.
    /// </summary>
    public IList<ILanguage> GetAllLanguages()
    {
        lock (_cacheLock)
        {
            if (_cachedAllLanguages is not null)
                return _cachedAllLanguages;

            List<ILanguage> languages = [];

            if (WindowsAiUtilities.CanDeviceUseWinAI())
            {
                // Add Windows AI languages - use static instance
                languages.Add(_windowsAiLangInstance);
            }

            foreach (Language lang in OcrEngine.AvailableRecognizerLanguages)
            {
                // Wrap Windows.Globalization.Language in a compatible ILanguage implementation
                languages.Add(new GlobalLang(lang));
            }

            _cachedAllLanguages = languages;
            return _cachedAllLanguages;
        }
    }

    /// <summary>
    /// Gets the language tag from a language object.
    /// </summary>
    public static string GetLanguageTag(object language)
    {
        return language switch
        {
            Language lang => lang.LanguageTag,
            WindowsAiLang => _windowsAiLangTag,
            TessLang tessLang => tessLang.RawTag,
            GlobalLang gLang => gLang.LanguageTag,
            _ => throw new ArgumentException("Unsupported language type", nameof(language)),
        };
    }

    /// <summary>
    /// Gets the language kind from a language object.
    /// </summary>
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

    /// <summary>
    /// Gets the OCR language to use based on settings and available languages.
    /// Cached based on LastUsedLang setting.
    /// </summary>
    public ILanguage GetOCRLanguage()
    {
        string lastUsedLang = AppUtilities.TextGrabSettings.LastUsedLang;

        lock (_cacheLock)
        {
            // Return cached value if settings haven't changed
            if (_cachedOcrLanguage is not null && _cachedLastUsedLang == lastUsedLang)
            {
                return _cachedOcrLanguage;
            }

            _cachedLastUsedLang = lastUsedLang;
            ILanguage selectedLanguage = GetCurrentInputLanguage();

            if (!string.IsNullOrEmpty(lastUsedLang))
            {
                if (lastUsedLang == _windowsAiLangTag)
                {
                    // If the last used language is Windows AI, return static instance
                    _cachedOcrLanguage = _windowsAiLangInstance;
                    return _cachedOcrLanguage;
                }

                try
                {
                    selectedLanguage = new GlobalLang(lastUsedLang);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to parse LastUsedLang: {lastUsedLang}\n{ex.Message}");
                    // if the language tag is invalid, reset to current input language
                    selectedLanguage = GetCurrentInputLanguage();
                }
            }

            IList<ILanguage> possibleOCRLanguages = GetAllLanguages();

            if (possibleOCRLanguages.Count == 0)
            {
                _cachedOcrLanguage = new GlobalLang("en-US");
                return _cachedOcrLanguage;
            }

            // check to see if the selected language is in the list of available OCR languages
            if (possibleOCRLanguages.All(l => l.LanguageTag != selectedLanguage.LanguageTag))
            {
                List<ILanguage> similarLanguages = [.. possibleOCRLanguages.Where(
                    la => la.LanguageTag.Contains(selectedLanguage.LanguageTag)
                    || selectedLanguage.LanguageTag.Contains(la.LanguageTag)
                )];

                if (similarLanguages.Count > 0)
                {
                    _cachedOcrLanguage = new GlobalLang(similarLanguages.First().LanguageTag);
                    return _cachedOcrLanguage;
                }
                else
                {
                    _cachedOcrLanguage = new GlobalLang(possibleOCRLanguages.First().LanguageTag);
                    return _cachedOcrLanguage;
                }
            }

            _cachedOcrLanguage = selectedLanguage;
            return _cachedOcrLanguage;
        }
    }

    /// <summary>
    /// Checks if the current input language is Latin-based.
    /// </summary>
    public bool IsCurrentLanguageLatinBased()
    {
        ILanguage lang = GetCurrentInputLanguage();
        return lang.IsLatinBased();
    }

    /// <summary>
    /// Gets the system language name suitable for Windows AI translation.
    /// Returns a user-friendly language name like "English", "Spanish", etc.
    /// Cached based on current input language.
    /// </summary>
    public string GetSystemLanguageForTranslation()
    {
        string currentInputLangTag = InputLanguageManager.Current.CurrentInputLanguage.Name;

        lock (_cacheLock)
        {
            // Return cached value if the input language hasn't changed
            if (_cachedSystemLanguageForTranslation is not null &&
                _cachedCurrentInputLanguageTag == currentInputLangTag)
            {
                return _cachedSystemLanguageForTranslation;
            }

            try
            {
                ILanguage currentLang = GetCurrentInputLanguage();
                string displayName = currentLang.DisplayName;

                // Extract base language name (before any parenthetical region info)
                if (displayName.Contains('('))
                    displayName = displayName[..displayName.IndexOf('(')].Trim();

                // Map common language tags to translation-friendly names
                string languageTag = currentLang.LanguageTag.ToLowerInvariant();
                _cachedSystemLanguageForTranslation = languageTag switch
                {
                    var tag when tag.StartsWith("en") => "English",
                    var tag when tag.StartsWith("es") => "Spanish",
                    var tag when tag.StartsWith("fr") => "French",
                    var tag when tag.StartsWith("de") => "German",
                    var tag when tag.StartsWith("it") => "Italian",
                    var tag when tag.StartsWith("pt") => "Portuguese",
                    var tag when tag.StartsWith("ru") => "Russian",
                    var tag when tag.StartsWith("ja") => "Japanese",
                    var tag when tag.StartsWith("zh") => "Chinese",
                    var tag when tag.StartsWith("ko") => "Korean",
                    var tag when tag.StartsWith("ar") => "Arabic",
                    var tag when tag.StartsWith("hi") => "Hindi",
                    _ => displayName // Use display name as fallback
                };

                return _cachedSystemLanguageForTranslation;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get system language for translation: {ex.Message}");
                _cachedSystemLanguageForTranslation = "English"; // Safe default
                return _cachedSystemLanguageForTranslation;
            }
        }
    }

    /// <summary>
    /// Invalidates the cached languages list. Call this when new languages are installed.
    /// </summary>
    public void InvalidateLanguagesCache()
    {
        lock (_cacheLock)
        {
            _cachedAllLanguages = null;
            _cachedOcrLanguage = null;
        }
    }

    /// <summary>
    /// Invalidates the OCR language cache. Call this when LastUsedLang setting changes.
    /// </summary>
    public void InvalidateOcrLanguageCache()
    {
        lock (_cacheLock)
        {
            _cachedOcrLanguage = null;
            _cachedLastUsedLang = null;
        }
    }

    /// <summary>
    /// Invalidates all caches. Call this when input language changes.
    /// </summary>
    public void InvalidateAllCaches()
    {
        lock (_cacheLock)
        {
            _cachedAllLanguages = null;
            _cachedCurrentInputLanguage = null;
            _cachedCurrentInputLanguageTag = null;
            _cachedSystemLanguageForTranslation = null;
            _cachedLastUsedLang = null;
            _cachedOcrLanguage = null;
        }
    }

    #endregion Public Methods
}
