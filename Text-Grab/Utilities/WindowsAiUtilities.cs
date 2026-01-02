using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.AI.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Extensions;
using Text_Grab.Models;
using Text_Grab.Properties;
using Windows.Graphics.Imaging;

namespace Text_Grab.Utilities;

public static class WindowsAiUtilities
{
    private const string TranslationPromptTemplate = "Translate to {0}:\n\n{1}";
    private static LanguageModel? _translationLanguageModel;
    private static readonly SemaphoreSlim _modelInitializationLock = new(1, 1);
    private static bool _disposed;

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
    private static bool IsLikelyInTargetLanguage(string text, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
            return false;

        // Get language code for target
        if (!LanguageCodeMap.TryGetValue(targetLanguage, out string? targetCode))
            return false; // Unknown language, proceed with translation

        // Character range detection
        bool hasCJK = text.Any(c => (c >= 0x4E00 && c <= 0x9FFF) || // CJK Unified Ideographs
                                     (c >= 0x3040 && c <= 0x309F) || // Hiragana
                                     (c >= 0x30A0 && c <= 0x30FF) || // Katakana
                                     (c >= 0xAC00 && c <= 0xD7AF));  // Hangul

        bool hasArabic = text.Any(c => c >= 0x0600 && c <= 0x06FF);
        bool hasCyrillic = text.Any(c => c >= 0x0400 && c <= 0x04FF);
        bool hasDevanagari = text.Any(c => c >= 0x0900 && c <= 0x097F);
        bool hasLatin = text.Any(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));

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
                    string[] commonEnglishWords = { " the ", " and ", " or ", " is ", " are ", " was ", " were ", " in ", " on ", " at ", " to ", " of ", " for ", " with " };
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
                return text.Any(c => c >= 0xAC00 && c <= 0xD7AF) && !hasArabic && !hasCyrillic;

            case "ar":
                // Arabic - should have Arabic script
                return hasArabic && !hasCJK && !hasCyrillic;

            case "hi":
                // Hindi - should have Devanagari
                return hasDevanagari && !hasCJK && !hasArabic;
        }

        return false;
    }

    public static bool CanDeviceUseWinAI()
    {
        // Check if the app is packaged and if the AI feature is supported
        if (!AppUtilities.IsPackaged() || OSInterop.IsWindows10())
            return false;

        // Today, Windows AI Text Recognition is only supported on ARM64
        Architecture arch = RuntimeInformation.ProcessArchitecture;
        if (arch != Architecture.Arm64 && !Settings.Default.OverrideAiArchCheck)
            return false;

        // After checking for Arm64 the remainder checks should be good to catch supporting devices

        try
        {
            AIFeatureReadyState readyState = TextRecognizer.GetReadyState();
            if (readyState == AIFeatureReadyState.NotSupportedOnCurrentSystem)
                return false;
            else
                return true;
        }
        catch (Exception)
        {
#if DEBUG
            throw;
#endif

#pragma warning disable CS0162 // Unreachable code detected
            return false;
#pragma warning restore CS0162 // Unreachable code detected
        }
    }

    public static async Task<string> GetTextWithWinAI(string imagePath)
    {
        if (!CanDeviceUseWinAI())
            return "ERROR: Cannot use Windows AI on this device.";

        AIFeatureReadyState readyState = TextRecognizer.GetReadyState();
        if (readyState == AIFeatureReadyState.NotReady)
        {
            AIFeatureReadyResult op = await TextRecognizer.EnsureReadyAsync();
        }

        using TextRecognizer textRecognizer = await TextRecognizer.CreateAsync();

        SoftwareBitmap bitmap = await imagePath.FilePathToSoftwareBitmapAsync();
        ImageBuffer imageBuffer = ImageBuffer.CreateForSoftwareBitmap(bitmap);

        RecognizedText? result = textRecognizer?
            .RecognizeTextFromImage(imageBuffer);

        if (result is null || result.Lines is null)
            return string.Empty;

        StringBuilder stringBuilder = new();

        foreach (RecognizedLine? line in result.Lines)
            stringBuilder.AppendLine(line.Text);

        return stringBuilder.ToString();
    }

    public static async Task<WinAiOcrLinesWords?> GetOcrResultAsync(Bitmap bmp)
    {
        string tempFilePath = System.IO.Path.GetTempFileName();
        bmp.Save(tempFilePath, System.Drawing.Imaging.ImageFormat.Png);
        SoftwareBitmap softwareBitmap = await tempFilePath.FilePathToSoftwareBitmapAsync();

        // for some reason "await bmp.CreateSoftwareBitmap()" does not work, so we use the file path method instead
        RecognizedText? recognizedText = await GetOcrResultAsync(softwareBitmap);

        if (recognizedText is null)
            return null;

        return new WinAiOcrLinesWords(recognizedText);
    }

    public static async Task<RecognizedText?> GetOcrResultAsync(SoftwareBitmap softwareBitmap)
    {
        if (!CanDeviceUseWinAI())
            return null;

        AIFeatureReadyState readyState = TextRecognizer.GetReadyState();
        if (readyState == AIFeatureReadyState.NotReady)
        {
            AIFeatureReadyResult op = await TextRecognizer.EnsureReadyAsync();
        }

        using TextRecognizer textRecognizer = await TextRecognizer.CreateAsync();
        ImageBuffer imageBuffer = ImageBuffer.CreateForSoftwareBitmap(softwareBitmap);

        RecognizedText? result = textRecognizer?
            .RecognizeTextFromImage(imageBuffer);

        return result;
    }

    internal static async Task<string> SummarizeParagraph(string textToSummarize)
    {
        using LanguageModel languageModel = await LanguageModel.CreateAsync();

        TextSummarizer textSummarizer = new(languageModel);

        bool wasTruncated = false;

        // TODO: in WinAppSDK 1.8+ we can use this API when the GitHub Actions runner passes
        // if (textSummarizer.IsPromptLargerThanContext(textToSummarize, out ulong cutOff))
        // {
        //     textToSummarize = textToSummarize[..(int)cutOff];
        //     wasTruncated = true;
        // }

        try
        {
            LanguageModelResponseResult result = await textSummarizer.SummarizeParagraphAsync(textToSummarize);

            if (result.Status == LanguageModelResponseStatus.Complete)
            {
                if (wasTruncated)
                    return $"NOTE: The input text was too long and had to be truncated.\n\nSummary:\n{result.Text}";
                else
                    return result.Text;
            }
            else
                return $"ERROR: Unable to summarize text. {result.ExtendedError.Message}";
        }
        catch (Exception ex)
        {
            return $"ERROR: Unable to summarize text. {ex.Message}";
        }
    }

    internal static async Task<string> Rewrite(string textToRewrite)
    {
        using LanguageModel languageModel = await LanguageModel.CreateAsync();

        TextRewriter textRewriter = new(languageModel);
        try
        {
            // TODO: in WinAppSDK 1.8+ we can use this API when the GitHub Actions runner passes
            //LanguageModelResponseResult result = await textRewriter.RewriteAsync(textToRewrite, TextRewriteTone.Concise);
            LanguageModelResponseResult result = await textRewriter.RewriteAsync(textToRewrite);
            if (result.Status == LanguageModelResponseStatus.Complete)
            {
                return result.Text;
            }
            else
                return $"ERROR: Unable to rewrite text. {result.ExtendedError.Message}";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to Rewrite: {ex.Message}";
        }
    }

    internal static async Task<string> TextToTable(string textToTable)
    {
        using LanguageModel languageModel = await LanguageModel.CreateAsync();

        TextToTableConverter toTableConverter = new(languageModel);
        try
        {
            TextToTableResponseResult result = await toTableConverter.ConvertAsync(textToTable);
            if (result.Status == LanguageModelResponseStatus.Complete)
            {
                TextToTableRow[] rows = result.GetRows();
                StringBuilder sb = new();
                foreach (TextToTableRow row in rows)
                {
                    string[] columns = row.GetColumns();
                    sb.AppendLine(string.Join("\t", columns));
                }
                return sb.ToString();
            }
            else
                return $"ERROR: Unable to rewrite text. {result.ExtendedError.Message}";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to Rewrite: {ex.Message}";
        }
    }

        /// <summary>
        /// Cleans up translation result by removing instruction echoes and unwanted prefixes.
        /// </summary>
        private static string CleanTranslationResult(string translatedText, string originalText)
        {
            if (string.IsNullOrWhiteSpace(translatedText))
                return originalText;

            string cleaned = translatedText.Trim();

            // Remove common instruction echoes (case-insensitive)
            string[] instructionPhrases = 
            [
                "translate",
                "translation",
                "translated",
                "do not reply",
                "do not respond",
                "extraneous content",
                "besides the translated text",
                "other than the translated text",
                "here is the translation",
                "here's the translation",
                "the translation is",
            ];

            string lowerCleaned = cleaned.ToLowerInvariant();

            // If the result contains instruction-like phrases, try to extract just the translation
            if (instructionPhrases.Any(phrase => lowerCleaned.Contains(phrase)))
            {
                // Split by common delimiters and take the longest non-instruction part
                string[] parts = cleaned.Split(['\n', '.', ':', '"'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                string? bestPart = null;
                int maxLength = 0;

                foreach (string part in parts)
                {
                    string lowerPart = part.ToLowerInvariant();
                    bool hasInstructions = instructionPhrases.Any(phrase => lowerPart.Contains(phrase));

                    if (!hasInstructions && part.Length > maxLength && part.Length >= 3)
                    {
                        bestPart = part;
                        maxLength = part.Length;
                    }
                }

                if (bestPart != null && bestPart.Length > originalText.Length / 3)
                {
                    cleaned = bestPart.Trim();
                }
                else
                {
                    // Couldn't extract clean translation, return original
                    Debug.WriteLine($"Translation contained instructions, returning original text");
                    return originalText;
                }
            }

            // Remove common prefixes that might leak through
            string[] commonPrefixes = 
            [
                "translation: ",
                "translated: ",
                "result: ",
                "output: ",
            ];

            foreach (string prefix in commonPrefixes.Where(prefix => cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                cleaned = cleaned[prefix.Length..].Trim();
            }

            // If cleaned result is suspiciously short or empty, return original
            if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length < 2)
            {
                Debug.WriteLine($"Translation result too short, returning original text");
                return originalText;
            }

            return cleaned;
        }

        /// <summary>
        /// Initializes the shared LanguageModel for translation if not already created.
        /// Thread-safe initialization using SemaphoreSlim.
        /// </summary>
        private static async Task EnsureTranslationModelInitializedAsync()
        {
            if (!object.ReferenceEquals(_translationLanguageModel, null))
                return;

            await _modelInitializationLock.WaitAsync();
            try
            {
                if (object.ReferenceEquals(_translationLanguageModel, null))
                {
                    _translationLanguageModel = await LanguageModel.CreateAsync();
                }
            }
            finally
            {
                _modelInitializationLock.Release();
            }
        }

        /// <summary>
        /// Disposes the shared LanguageModel to free resources.
        /// Should be called when translation is no longer needed.
        /// </summary>
        public static void DisposeTranslationModel()
        {
            _translationLanguageModel?.Dispose();
            _translationLanguageModel = null;
        }


    /// <summary>
    /// Releases resources held by static members of <see cref="WindowsAiUtilities"/>.
    /// Should be called once during application shutdown.
    /// </summary>
    public static void Cleanup()
    {
        if (_disposed)
            return;

        DisposeTranslationModel();
        _modelInitializationLock.Dispose();
        _disposed = true;
    }
    /// <summary>
    /// Translates text to a target language using Windows AI LanguageModel.
    /// Reuses a shared LanguageModel instance for improved performance.
    /// Includes fast language detection to skip translation if text is already in target language.
    /// Filters out instruction echoes from AI responses.
    /// </summary>
    /// <param name="textToTranslate">The text to translate</param>
    /// <param name="targetLanguage">The target language (e.g., "English", "Spanish")</param>
    /// <returns>The translated text, or the original text if translation fails or is unnecessary</returns>
    /// <remarks>
    /// This implementation uses TextRewriter with a custom prompt as a workaround
    /// since Microsoft.Windows.AI.Text doesn't include a dedicated translation API.
    /// Translation quality may vary compared to dedicated translation services.
    /// The LanguageModel is reused across calls for better performance.
    /// Fast language detection is performed first to avoid unnecessary API calls.
    /// Result is cleaned to remove any instruction echoes from the AI response.
    /// </remarks>
    internal static async Task<string> TranslateText(string textToTranslate, string targetLanguage)
    {
    if (!CanDeviceUseWinAI())
        return textToTranslate; // Return original text if Windows AI is not available

    // Quick check: if text appears to already be in target language, skip translation
    if (IsLikelyInTargetLanguage(textToTranslate, targetLanguage))
    {
        Debug.WriteLine($"Skipping translation - text appears to already be in {targetLanguage}");
        return textToTranslate;
    }

    try
    {
        await EnsureTranslationModelInitializedAsync();

        if (_translationLanguageModel == null)
        return textToTranslate;

        // Note: This uses TextRewriter with a simple prompt
        // We use a minimal prompt to reduce the chance of instruction echoes
        TextRewriter textRewriter = new(_translationLanguageModel);
        string translationPrompt = string.Format(TranslationPromptTemplate, targetLanguage, textToTranslate);

        LanguageModelResponseResult result = await textRewriter.RewriteAsync(translationPrompt);

        if (result.Status == LanguageModelResponseStatus.Complete)
        {
        // Clean the result to remove any instruction echoes
        string cleanedResult = CleanTranslationResult(result.Text, textToTranslate);
        return cleanedResult;
        }
        else
        {
        // Log the error if debugging is enabled
        Debug.WriteLine($"Translation failed with status: {result.Status}");
        if (result.ExtendedError != null)
        Debug.WriteLine($"Translation error: {result.ExtendedError.Message}");
        return textToTranslate; // Return original text on error
        }
        catch (Exception ex)
        {
        // Log the exception for debugging
        Debug.WriteLine($"Translation exception: {ex.Message}");
        return textToTranslate; // Return original text on error
        }
        }
        }
