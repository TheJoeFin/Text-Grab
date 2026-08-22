using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
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
    public static bool CanDeviceUseWinAI()
    {
        return CanDeviceUseWinAiFeature(TextRecognizer.GetReadyState);
    }

    public static bool CanDeviceDescribeImagesWithWinAI()
    {
        return CanDeviceUseWinAiFeature(ImageDescriptionGenerator.GetReadyState);
    }

    private static bool CanDeviceUseWinAiFeature(Func<AIFeatureReadyState> getReadyState)
    {
        if (!MeetsWindowsAiPrerequisites())
            return false;

        try
        {
            return getReadyState() != AIFeatureReadyState.NotSupportedOnCurrentSystem;
        }
        catch (Exception)
        {
#if DEBUG
            throw;
#else
            return false;
#endif
        }
    }

    private static bool MeetsWindowsAiPrerequisites()
    {
        // Check if the app is packaged and if the AI feature is supported
        if (!AppUtilities.IsPackaged() || OSInterop.IsWindows10())
            return false;

        // Today, Windows AI features are only supported on ARM64 unless overridden for debugging.
        Architecture arch = RuntimeInformation.ProcessArchitecture;
        if (arch != Architecture.Arm64 && !Settings.Default.OverrideAiArchCheck)
            return false;

        return true;
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
        using ImageBuffer imageBuffer = ImageBuffer.CreateForSoftwareBitmap(bitmap);

        RecognizedText? result = textRecognizer?
            .RecognizeTextFromImage(imageBuffer);

        if (result is null || result.Lines is null)
            return string.Empty;

        StringBuilder stringBuilder = new();

        foreach (RecognizedLine? line in result.Lines)
            stringBuilder.AppendLine(line.Text);

        return stringBuilder.ToString();
    }

    public static async Task<string> GetTextDescriptionWithWinAI(string imagePath)
    {
        using SoftwareBitmap bitmap = await imagePath.FilePathToSoftwareBitmapAsync();
        return await GetTextDescriptionWithWinAI(bitmap);
    }

    /// <summary>
    /// Describes a <see cref="Bitmap"/> with Windows AI. The <paramref name="cancellationToken"/>
    /// aborts the on-device inference; a cancelled call throws <see cref="OperationCanceledException"/>.
    /// </summary>
    public static async Task<string> GetTextDescriptionWithWinAI(Bitmap bmp, CancellationToken cancellationToken)
    {
        string tempFilePath = AutomationProfile.GetTemporaryFilePath(".png");
        bmp.Save(tempFilePath, System.Drawing.Imaging.ImageFormat.Png);
        try
        {
            using SoftwareBitmap softwareBitmap = await tempFilePath.FilePathToSoftwareBitmapAsync();
            return await GetTextDescriptionWithWinAI(softwareBitmap, cancellationToken);
        }
        finally
        {
            if (System.IO.File.Exists(tempFilePath))
                System.IO.File.Delete(tempFilePath);
        }
    }

    public static async Task<string> GetTextDescriptionWithWinAI(SoftwareBitmap bitmap, CancellationToken cancellationToken = default)
    {
        // Return empty rather than an error message so callers treat this as a
        // failed grab instead of committing the message as recognized text.
        if (!CanDeviceDescribeImagesWithWinAI())
            return string.Empty;

        AIFeatureReadyState readyState = ImageDescriptionGenerator.GetReadyState();
        if (readyState == AIFeatureReadyState.NotReady)
        {
            // EnsureReadyAsync may download the model; thread the token so Cancel
            // aborts the wait, and bail out if the feature still failed to get ready.
            AIFeatureReadyResult readyResult = await ImageDescriptionGenerator.EnsureReadyAsync().AsTask(cancellationToken);
            if (readyResult.Status != AIFeatureReadyResultState.Success)
            {
                Debug.WriteLine($"Image description model not ready: {readyResult.Status}");
                return string.Empty;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        using ImageDescriptionGenerator imageDescriptionGenerator = await ImageDescriptionGenerator.CreateAsync();
        using ImageBuffer imageBuffer = ImageBuffer.CreateForSoftwareBitmap(bitmap);
        return await GetTextDescriptionWithWinAI(imageDescriptionGenerator, imageBuffer, cancellationToken);
    }

    private static async Task<string> GetTextDescriptionWithWinAI(ImageDescriptionGenerator imageDescriptionGenerator, ImageBuffer imageBuffer, CancellationToken cancellationToken = default)
    {
        // Create content moderation thresholds object.
        ContentFilterOptions filterOptions = new();
        filterOptions.ResponseMaxAllowedSeverityLevel.SelfHarm = SeverityLevel.Medium;
        filterOptions.ResponseMaxAllowedSeverityLevel.Violent = SeverityLevel.Medium;

        try
        {
            // Get text description. Awaiting DescribeAsync already waits for the on-device
            // inference to finish; AsTask threads the cancellation token so the model call
            // itself is aborted when the user cancels.
            ImageDescriptionResult languageModelResponse = await imageDescriptionGenerator.DescribeAsync(
                                                                                imageBuffer,
                                                                                ImageDescriptionKind.AccessibleDescription,
                                                                                filterOptions).AsTask(cancellationToken);

            if (languageModelResponse.Status != ImageDescriptionResultStatus.Complete)
            {
                Debug.WriteLine($"Image description did not complete. Status: {languageModelResponse.Status}");
                return string.Empty;
            }

            return languageModelResponse.Description?.Trim() ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            // Let cancellation propagate so callers can distinguish it from an empty result.
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Image description failed: {ex.Message}");
            return string.Empty;
        }
    }

    public static async Task<WinAiOcrLinesWords?> GetOcrResultAsync(Bitmap bmp)
    {
        string tempFilePath = AutomationProfile.GetTemporaryFilePath(".png");
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
        (bool available, string? reason) = WinAiLanguageModel.CheckAvailability();
        if (!available)
            return $"ERROR: {reason}";

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
        (bool available, string? reason) = WinAiLanguageModel.CheckAvailability();
        if (!available)
            return $"ERROR: {reason}";

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
        (bool available, string? reason) = WinAiLanguageModel.CheckAvailability();
        if (!available)
            return $"ERROR: {reason}";

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
    /// Releases resources held by static members of <see cref="WindowsAiUtilities"/>.
    /// Should be called once during application shutdown.
    /// </summary>
    public static void Cleanup() => WinAiLanguageModel.Cleanup();

    /// <summary>
    /// Extracts a regular expression pattern from text using the shared Windows AI language model.
    /// </summary>
    /// <param name="textDescription">The text describing what to match, or example text to match</param>
    /// <param name="cancellationToken">Aborts the on-device inference.</param>
    /// <returns>
    /// The pattern in <see cref="WinAiGenerationResult.Text"/>, or a <see cref="WinAiFailure"/> and
    /// a human-readable message explaining why there is none.
    /// </returns>
    /// <remarks>
    /// This goes through <see cref="WinAiLanguageModel"/> like translation does, so it shares the
    /// Limited Access Feature unlock and the one cached <c>LanguageModel</c>, and prompts the model
    /// directly with a regex system prompt rather than bending the TextRewriter skill into the job.
    /// </remarks>
    internal static async Task<WinAiGenerationResult> ExtractRegex(
        string textDescription,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(textDescription))
            return WinAiGenerationResult.Failed(WinAiFailure.ModelError, "There was no text to build a pattern from.");

        const string systemPrompt =
            "You are a regular expression generator. The user describes what to match, or gives an example " +
            "of the text they want to match. Reply with a single .NET regular expression pattern that matches " +
            "it. Generalize: match text of that kind, not only the exact sample. " +
            "Reply with the pattern only: no delimiters, no code fences, no flags, no explanation, " +
            "and never repeat these instructions.";

        // Pattern generation should be as deterministic as the model allows.
        WinAiGenerationResult result = await WinAiLanguageModel.PromptAsync(
            systemPrompt, textDescription, temperature: 0.1f, cancellationToken: cancellationToken);

        if (result.Text is null)
        {
            Debug.WriteLine($"Regex extraction failed ({result.Failure}): {result.Message}");
            return result;
        }

        string pattern = CleanRegexResult(result.Text);

        return string.IsNullOrWhiteSpace(pattern)
            ? WinAiGenerationResult.Failed(WinAiFailure.ModelError, "The language model did not return a usable pattern.")
            : WinAiGenerationResult.Ok(pattern);
    }

    /// <summary>
    /// Cleans the AI-generated regex result by removing markdown formatting, code blocks, and explanations.
    /// </summary>
    /// <param name="regexText">The raw AI response containing the regex pattern</param>
    /// <returns>The cleaned regex pattern string</returns>
    public static string CleanRegexResult(string regexText)
    {
        if (string.IsNullOrWhiteSpace(regexText))
            return string.Empty;

        string cleaned = regexText.Trim();

        // Remove markdown code blocks
        if (cleaned.StartsWith("```"))
        {
            // Remove opening code fence
            int firstNewline = cleaned.IndexOf('\n');
            if (firstNewline > 0)
                cleaned = cleaned[(firstNewline + 1)..];

            // Remove closing code fence
            if (cleaned.EndsWith("```"))
                cleaned = cleaned[..^3];

            cleaned = cleaned.Trim();
        }

        // Remove backticks
        cleaned = cleaned.Trim('`');

        // Split by newlines and process lines
        string[] lines = cleaned.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Find the first line that looks like a regex pattern
        string? regexPattern = lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal) &&
                          !line.StartsWith('#') &&
                          !line.StartsWith("Expression:", StringComparison.OrdinalIgnoreCase))
            .Select(line =>
            {
                // Remove common prefixes
                if (line.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
                    return line[6..].Trim();
                else if (line.StartsWith("pattern:", StringComparison.OrdinalIgnoreCase))
                    return line[8..].Trim();
                return line;
            })
            .FirstOrDefault(line => line.Length > 0 &&
                                   (line.Contains('[') || line.Contains('(') ||
                                    line.Contains('\\') || line.Contains('^') || line.Contains('$') ||
                                    line.Contains('+') || line.Contains('*') || line.Contains('?') ||
                                    line.Contains('|') || line.Contains('.')));

        // If a regex pattern was found, return it; otherwise return the cleaned text as-is
        return regexPattern ?? cleaned;
    }
}
