using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.AI.Text;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
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

    internal static async Task<string> TranslateText(string textToTranslate, string targetLanguage)
    {
        if (!CanDeviceUseWinAI())
            return textToTranslate; // Return original text if Windows AI is not available

        try
        {
            using LanguageModel languageModel = await LanguageModel.CreateAsync();

            // Note: This uses TextRewriter as a workaround since Microsoft.Windows.AI.Text
            // doesn't have a dedicated TextTranslator class in WindowsAppSDK 1.8.
            // For more accurate translation, consider using Microsoft.Extensions.AI
            // with IChatClient as shown in the AI Dev Gallery examples.
            TextRewriter textRewriter = new(languageModel);
            string translationPrompt = $"Translate the following text to {targetLanguage}:\n\n{textToTranslate}";
            
            LanguageModelResponseResult result = await textRewriter.RewriteAsync(translationPrompt);

            if (result.Status == LanguageModelResponseStatus.Complete)
            {
                return result.Text;
            }
            else
            {
                // Log the error if debugging is enabled
                Debug.WriteLine($"Translation failed with status: {result.Status}");
                if (result.ExtendedError != null)
                    Debug.WriteLine($"Translation error: {result.ExtendedError.Message}");
                return textToTranslate; // Return original text on error
            }
        }
        catch (Exception ex)
        {
            // Log the exception for debugging
            Debug.WriteLine($"Translation exception: {ex.Message}");
            return textToTranslate; // Return original text on error
        }
    }
}
