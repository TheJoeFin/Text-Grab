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

    /// <summary>Internal so <see cref="WindowsAiSpeechTranscriptionUtilities"/> can reuse this gate without going through <see cref="CanDeviceUseWinAiFeature"/>, which calls <c>GetReadyState()</c> synchronously - safe for OCR/image-description, but not for Speech (see that type's remarks).</summary>
    internal static bool MeetsWindowsAiPrerequisites()
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

    /// <summary>
    /// Summarizes text with the shared Windows AI language model.
    /// </summary>
    /// <returns>
    /// The summary in <see cref="WinAiGenerationResult.Text"/>, or a <see cref="WinAiFailure"/> and a
    /// human-readable message. A failure must never be shown as if it were a summary, which is why
    /// this reports one rather than returning an "ERROR: …" string.
    /// </returns>
    internal static async Task<WinAiGenerationResult> SummarizeParagraph(string textToSummarize)
    {
        bool wasTruncated = false;

        // TODO: in WinAppSDK 1.8+ we can use this API when the GitHub Actions runner passes
        // if (textSummarizer.IsPromptLargerThanContext(textToSummarize, out ulong cutOff))
        // {
        //     textToSummarize = textToSummarize[..(int)cutOff];
        //     wasTruncated = true;
        // }

        // Going through the shared model reuses the one LanguageModel the other AI features hold,
        // and means a dropped connection to the Windows AI runtime ("The RPC server is unavailable")
        // restarts the model and tries again instead of failing until Text-Grab is restarted.
        (LanguageModelResponseResult? result, string? error) = await WinAiLanguageModel.RunWithModelAsync(
            (model, token) => new TextSummarizer(model).SummarizeParagraphAsync(textToSummarize).AsTask(token));

        if (result is null)
            return WinAiGenerationResult.Failed(WinAiFailure.ModelNotReady, $"Unable to summarize text. {error}");

        if (result.Status != LanguageModelResponseStatus.Complete)
            return WinAiGenerationResult.Failed(
                WinAiFailure.ModelError,
                $"Unable to summarize text. {result.ExtendedError?.Message ?? result.Status.ToString()}");

        return WinAiGenerationResult.Ok(wasTruncated
            ? $"NOTE: The input text was too long and had to be truncated.\n\nSummary:\n{result.Text}"
            : result.Text);
    }

    internal static async Task<string> Rewrite(string textToRewrite)
    {
        // TODO: in WinAppSDK 1.8+ we can pass TextRewriteTone.Concise when the GitHub Actions runner passes
        (LanguageModelResponseResult? result, string? error) = await WinAiLanguageModel.RunWithModelAsync(
            (model, token) => new TextRewriter(model).RewriteAsync(textToRewrite).AsTask(token));

        if (result is null)
            return $"ERROR: Failed to Rewrite: {error}";

        return result.Status == LanguageModelResponseStatus.Complete
            ? result.Text
            : $"ERROR: Unable to rewrite text. {result.ExtendedError?.Message ?? result.Status.ToString()}";
    }

    internal static async Task<string> TextToTable(string textToTable)
    {
        (TextToTableResponseResult? result, string? error) = await WinAiLanguageModel.RunWithModelAsync(
            (model, token) => new TextToTableConverter(model).ConvertAsync(textToTable).AsTask(token));

        if (result is null)
            return $"ERROR: Failed to convert the text to a table. {error}";

        if (result.Status != LanguageModelResponseStatus.Complete)
            return $"ERROR: Unable to convert the text to a table. {result.ExtendedError?.Message ?? result.Status.ToString()}";

        StringBuilder sb = new();
        foreach (TextToTableRow row in result.GetRows())
            sb.AppendLine(string.Join("\t", row.GetColumns()));

        return sb.ToString();
    }

    /// <summary>
    /// Releases resources held by static members of <see cref="WindowsAiUtilities"/>.
    /// Should be called once during application shutdown.
    /// </summary>
    public static void Cleanup() => WinAiLanguageModel.Cleanup();

    /// <summary>
    /// Drops the language model this process is holding so the next AI request builds a fresh
    /// connection to the Windows AI runtime. The AI features already do this by themselves when a
    /// request finds the runtime gone; this is for offering the user a manual reconnect.
    /// </summary>
    public static Task RestartWindowsAiAsync() => WinAiLanguageModel.RestartModelAsync();

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
