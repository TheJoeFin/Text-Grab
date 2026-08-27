using System;
using System.Collections.Generic;
using System.Linq;

namespace Text_Grab.Utilities;

/// <summary>
/// Cheap script-based guesses about what language a string is already in, used to skip
/// translation work that would be a no-op.
/// </summary>
internal static class LanguageHeuristics
{

    // Language code mapping for quick lookup
    private static readonly Dictionary<string, string> LanguageCodeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "English", "en" },
        { "Spanish", "es" },
        { "French", "fr" },
        { "German", "de" },
        { "Italian", "it" },
        { "Portuguese", "pt" },
        { "Russian", "ru" },
        { "Japanese", "ja" },
        { "Chinese (Simplified)", "zh-Hans" },
        { "Chinese", "zh-Hans" },
        { "Korean", "ko" },
        { "Arabic", "ar" },
        { "Hindi", "hi" },
    };

    /// <summary>
    /// Quickly detects if text is likely in the target language using simple heuristics.
    /// This is a fast check to avoid expensive translation calls.
    /// </summary>
    /// <param name="text">Text to analyze</param>
    /// <param name="targetLanguage">Target language name (e.g., "English", "Spanish")</param>
    /// <returns>True if text appears to already be in target language</returns>
    internal static bool IsLikelyInTargetLanguage(string text, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
            return false;

        // Get language code for target
        if (!LanguageCodeMap.TryGetValue(targetLanguage, out string? targetCode))
            return false; // Unknown language, proceed with translation

        // Character range detection
        bool hasCJK = text.Any(c => c is >= (char)0x4E00 and <= (char)0x9FFF or // CJK Unified Ideographs
                                     >= (char)0x3040 and <= (char)0x309F or // Hiragana
                                     >= (char)0x30A0 and <= (char)0x30FF or // Katakana
                                     >= (char)0xAC00 and <= (char)0xD7AF);  // Hangul

        bool hasArabic = text.Any(c => c is >= (char)0x0600 and <= (char)0x06FF);
        bool hasCyrillic = text.Any(c => c is >= (char)0x0400 and <= (char)0x04FF);
        bool hasDevanagari = text.Any(c => c is >= (char)0x0900 and <= (char)0x097F);
        bool hasLatin = text.Any(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

        // Quick script-based checks
        switch (targetCode)
        {
            case "en":
            case "es":
            case "fr":
            case "de":
            case "it":
            case "pt":
                // Latin script languages - if mostly CJK/Arabic/Cyrillic, definitely not in target
                if (hasCJK || hasArabic || hasCyrillic || hasDevanagari)
                    return false;
                // If has Latin characters, might be in target language
                if (hasLatin && text.Length > 10 && targetCode == "en")
                {
                    // Check for common English words as additional heuristic
                    string lowerText = text.ToLowerInvariant();
                    string[] commonEnglishWords = [" the ", " and ", " or ", " is ", " are ", " was ", " were ", " in ", " on ", " at ", " to ", " of ", " for ", " with "];
                    int englishWordCount = commonEnglishWords.Count(w => lowerText.Contains(w));
                    // If text contains multiple common English words, likely already English
                    if (englishWordCount >= 2)
                        return true;
                }
                break;

            case "ru":
                // Russian - should have Cyrillic
                return hasCyrillic && !hasCJK && !hasArabic;

            case "ja":
                // Japanese - should have Hiragana/Katakana/Kanji
                return hasCJK && !hasArabic && !hasCyrillic;

            case "zh-Hans":
                // Chinese - should have CJK
                return hasCJK && !hasArabic && !hasCyrillic;

            case "ko":
                // Korean - should have Hangul
                return text.Any(c => c is >= (char)0xAC00 and <= (char)0xD7AF) && !hasArabic && !hasCyrillic;

            case "ar":
                // Arabic - should have Arabic script
                return hasArabic && !hasCJK && !hasCyrillic;

            case "hi":
                // Hindi - should have Devanagari
                return hasDevanagari && !hasCJK && !hasArabic;
        }

        return false;
    }

}
