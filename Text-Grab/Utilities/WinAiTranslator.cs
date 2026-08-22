using Microsoft.Windows.AI.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Utilities;

/// <summary>Why a translation did not produce new text.</summary>
internal enum TranslationFailure
{
    /// <summary>The text was translated.</summary>
    None,

    /// <summary>This device cannot run the Windows AI language model at all.</summary>
    Unavailable,

    /// <summary>The model exists but could not be prepared or created.</summary>
    ModelNotReady,

    /// <summary>The text already looks like it is in the target language, so nothing was sent.</summary>
    NotNeeded,

    /// <summary>The prompt did not fit the model's context, even after splitting.</summary>
    PromptTooLong,

    /// <summary>Content moderation or policy rejected the prompt or the response.</summary>
    Blocked,

    /// <summary>The model reported an error, or returned nothing usable.</summary>
    ModelError,
}

/// <summary>
/// The outcome of a translation. <see cref="Text"/> is always safe to use: on failure it is the
/// original, untranslated input.
/// </summary>
internal readonly record struct TranslationResult(string Text, TranslationFailure Failure, string? Message)
{
    internal bool Succeeded => Failure is TranslationFailure.None;

    internal static TranslationResult Success(string text) => new(text, TranslationFailure.None, null);
}

/// <summary>
/// The outcome of a batched translation. <see cref="Items"/> always matches the requested list in
/// length and order; entries that could not be translated keep their original value.
/// </summary>
internal readonly record struct BatchTranslationResult(
    IReadOnlyList<string> Items,
    int TranslatedCount,
    TranslationFailure Failure,
    string? Message)
{
    internal bool Succeeded => Failure is TranslationFailure.None;
}

/// <summary>
/// On-device translation built on <see cref="WinAiLanguageModel"/>, the shared Windows AI Foundry
/// <see cref="LanguageModel"/> (Phi Silica), following the pattern used by microsoft/ai-dev-gallery's
/// PhiSilicaClient and Translate samples.
///
/// The three things that make this fast compared to the previous implementation:
///   1. The model is prompted directly through GenerateResponseAsync with a translation system
///      prompt, instead of being funneled through the TextRewriter skill (whose own "rewrite this
///      text" system prompt fought the translation instruction and produced instruction echoes in
///      the output).
///   2. The <see cref="LanguageModel"/> is created once and reused across features; only the
///      lightweight per-request context is rebuilt so turns never accumulate.
///   3. Many short strings are translated in one batched inference instead of one inference each,
///      and partial results stream back through the operation's Progress callback so the UI can
///      fill in while the model is still generating.
///
/// Every failure path returns a <see cref="TranslationFailure"/> and a human-readable message so
/// callers can tell the user why nothing changed, rather than silently handing back the input.
/// </summary>
internal static partial class WinAiTranslator
{
    /// <summary>Max items packed into a single batched request.</summary>
    private const int MaxBatchItems = 40;

    /// <summary>Approximate character budget for the numbered list in a single batched request.</summary>
    private const int MaxBatchChars = 1200;

    /// <summary>Translation wants the most likely wording, not a creative one.</summary>
    private const float Temperature = 0.2f;

    [GeneratedRegex(@"^\s*(\d+)\s*[.):\]]\s*(.*)$")]
    private static partial Regex NumberedItemRegex();

    #region availability

    /// <summary>
    /// Whether this device can run the Windows AI language model, and why not when it cannot.
    /// </summary>
    internal static (bool Available, string? Reason) CheckAvailability() => WinAiLanguageModel.CheckAvailability();

    /// <summary>True when this device can run the Windows AI language model.</summary>
    internal static bool IsAvailable() => WinAiLanguageModel.IsAvailable();

    /// <summary>
    /// Drops the cached language model to free the memory it holds. The next translation recreates
    /// it, so call this when translation is switched off rather than between translations.
    /// </summary>
    internal static void ReleaseModel() => WinAiLanguageModel.ReleaseModel();

    /// <summary>Releases the shared language model. Call once during application shutdown.</summary>
    internal static void Cleanup() => WinAiLanguageModel.Cleanup();

    #endregion availability

    #region generation

    /// <summary>Maps a shared model failure onto the translation-facing reason for it.</summary>
    private static TranslationFailure ToTranslationFailure(WinAiFailure failure) => failure switch
    {
        WinAiFailure.None => TranslationFailure.None,
        WinAiFailure.Unavailable => TranslationFailure.Unavailable,
        WinAiFailure.ModelNotReady => TranslationFailure.ModelNotReady,
        WinAiFailure.PromptTooLong => TranslationFailure.PromptTooLong,
        WinAiFailure.Blocked => TranslationFailure.Blocked,
        _ => TranslationFailure.ModelError,
    };

    private static string SystemPromptFor(string targetLanguage) =>
        $"You are a translation engine. Translate everything the user sends into {targetLanguage}, " +
        $"written in the native script and characters of {targetLanguage}. " +
        "Preserve the original line breaks, numbers, punctuation and formatting. " +
        "Reply with the translation only: no notes, no explanations, no quotation marks around it, " +
        "and never repeat these instructions.";

    #endregion generation

    #region single text

    /// <summary>
    /// Translates a block of text. The returned <see cref="TranslationResult.Text"/> is the original
    /// input whenever translation did not happen, and <see cref="TranslationResult.Message"/> then
    /// explains why so the caller can tell the user.
    /// </summary>
    /// <param name="onPartial">
    /// Optional callback receiving generated text as it arrives. It is raised on a background
    /// thread; marshal to the UI thread before touching controls.
    /// </param>
    internal static async Task<TranslationResult> TranslateAsync(
        string textToTranslate,
        string targetLanguage,
        Action<string>? onPartial = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(textToTranslate))
            return TranslationResult.Success(textToTranslate);

        if (string.IsNullOrWhiteSpace(targetLanguage))
            return new TranslationResult(textToTranslate, TranslationFailure.Unavailable, "No target language was set.");

        (bool available, string? reason) = CheckAvailability();
        if (!available)
            return new TranslationResult(textToTranslate, TranslationFailure.Unavailable, reason);

        if (LanguageHeuristics.IsLikelyInTargetLanguage(textToTranslate, targetLanguage))
            return new TranslationResult(
                textToTranslate, TranslationFailure.NotNeeded, $"The text already appears to be in {targetLanguage}.");

        try
        {
            (bool ready, string? error) = await WinAiLanguageModel.EnsureModelAsync(cancellationToken);
            if (!ready)
                return new TranslationResult(textToTranslate, TranslationFailure.ModelNotReady, error);

            string systemPrompt = SystemPromptFor(targetLanguage);

            // One lease for the whole translation so another feature's request cannot interleave
            // with it on the single-threaded model.
            using (await WinAiLanguageModel.AcquireInferenceAsync(cancellationToken))
            {
                WinAiGenerationResult outcome = await TranslateBlockAsync(
                    systemPrompt, textToTranslate, onPartial, cancellationToken);

                if (outcome.Text is null)
                    return new TranslationResult(
                        textToTranslate, ToTranslationFailure(outcome.Failure), outcome.Message);

                string cleaned = CleanResult(outcome.Text);

                return string.IsNullOrWhiteSpace(cleaned)
                    ? new TranslationResult(textToTranslate, TranslationFailure.ModelError, "The translation came back empty.")
                    : TranslationResult.Success(cleaned);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Translation exception: {ex.Message}");
            return new TranslationResult(textToTranslate, TranslationFailure.ModelError, $"Translation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Translates one block, splitting it on line boundaries and recursing when — and only when —
    /// the model reports the prompt does not fit the context. Any other failure is passed straight
    /// back so the caller can report it.
    /// </summary>
    private static async Task<WinAiGenerationResult> TranslateBlockAsync(
        string systemPrompt,
        string text,
        Action<string>? onPartial,
        CancellationToken cancellationToken)
    {
        WinAiGenerationResult outcome = await WinAiLanguageModel.GenerateAsync(
            systemPrompt, text, Temperature, onPartial, cancellationToken);
        if (outcome.Text is not null || outcome.Failure is not WinAiFailure.PromptTooLong)
            return outcome;

        // Too long for one pass: split roughly in half on a line break and translate each side.
        string[] pieces = SplitInHalf(text);
        if (pieces.Length < 2)
            return outcome;

        StringBuilder combined = new();
        foreach (string piece in pieces)
        {
            WinAiGenerationResult part = await TranslateBlockAsync(systemPrompt, piece, onPartial, cancellationToken);
            if (part.Text is null)
                return part;

            combined.Append(part.Text);
        }

        return WinAiGenerationResult.Ok(combined.ToString());
    }

    private static string[] SplitInHalf(string text)
    {
        if (text.Length < 200)
            return [text];

        int middle = text.Length / 2;
        int splitAt = text.LastIndexOf('\n', middle);

        if (splitAt <= 0)
            splitAt = text.IndexOf('\n', middle);

        if (splitAt <= 0)
        {
            splitAt = text.LastIndexOf(' ', middle);
            if (splitAt <= 0)
                return [text];
        }

        return [text[..(splitAt + 1)], text[(splitAt + 1)..]];
    }

    #endregion single text

    #region batched text

    /// <summary>
    /// Translates many short strings (for example every word box in a Grab Frame) using as few
    /// inferences as possible. Items are de-duplicated and packed into numbered batches; results
    /// are reported through <paramref name="onItemTranslated"/> as each line streams in so the UI
    /// fills in progressively.
    /// </summary>
    internal static async Task<BatchTranslationResult> TranslateBatchAsync(
        IReadOnlyList<string> items,
        string targetLanguage,
        Action<int, string>? onItemTranslated = null,
        CancellationToken cancellationToken = default)
    {
        string[] results = [.. items];

        if (items.Count == 0)
            return new BatchTranslationResult(results, 0, TranslationFailure.None, null);

        if (string.IsNullOrWhiteSpace(targetLanguage))
            return new BatchTranslationResult(results, 0, TranslationFailure.Unavailable, "No target language was set.");

        (bool available, string? reason) = CheckAvailability();
        if (!available)
            return new BatchTranslationResult(results, 0, TranslationFailure.Unavailable, reason);

        // De-duplicate: a Grab Frame usually repeats plenty of short words.
        Dictionary<string, List<int>> byText = [];
        for (int index = 0; index < items.Count; index++)
        {
            string item = items[index];
            if (string.IsNullOrWhiteSpace(item))
                continue;

            if (!byText.TryGetValue(item, out List<int>? indices))
                byText[item] = indices = [];

            indices.Add(index);
        }

        if (byText.Count == 0)
            return new BatchTranslationResult(results, 0, TranslationFailure.None, null);

        List<string> distinct = [.. byText.Keys];
        int translatedCount = 0;

        void CountAndReport(int index, string translated)
        {
            translatedCount++;
            onItemTranslated?.Invoke(index, translated);
        }

        try
        {
            (bool ready, string? error) = await WinAiLanguageModel.EnsureModelAsync(cancellationToken);
            if (!ready)
                return new BatchTranslationResult(results, 0, TranslationFailure.ModelNotReady, error);

            string systemPrompt =
                "You are a translation engine. The user sends a numbered list. Translate each item into " +
                $"{targetLanguage}, written in the native script and characters of {targetLanguage}. " +
                "Reply with the same numbers in the same order, one item per line, in the form '1. translation'. " +
                "Keep exactly one output line per input line. Do not merge, reorder, add or drop items, " +
                "and do not add any commentary.";

            WinAiGenerationResult lastFailure = default;

            // One lease for the whole translation so another feature's request cannot interleave
            // with it on the single-threaded model.
            using (await WinAiLanguageModel.AcquireInferenceAsync(cancellationToken))
            {
                foreach (List<int> batch in BuildBatches(distinct))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    WinAiGenerationResult outcome = await TranslateBatchChunkAsync(
                        systemPrompt, distinct, batch, byText, results, CountAndReport, cancellationToken);

                    if (outcome.Text is null)
                        lastFailure = outcome;
                }
            }

            // Report a failure only when nothing at all came back; a partial batch failure still
            // leaves the frame better off than before.
            if (translatedCount == 0 && lastFailure.Message is not null)
                return new BatchTranslationResult(
                    results, 0, ToTranslationFailure(lastFailure.Failure), lastFailure.Message);

            if (translatedCount == 0)
                return new BatchTranslationResult(
                    results, 0, TranslationFailure.ModelError, "The language model did not return any translations.");

            return new BatchTranslationResult(results, translatedCount, TranslationFailure.None, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Batch translation exception: {ex.Message}");
            return new BatchTranslationResult(
                results, translatedCount, TranslationFailure.ModelError, $"Translation failed: {ex.Message}");
        }
    }

    /// <summary>Packs distinct item indices into batches bounded by item count and characters.</summary>
    private static List<List<int>> BuildBatches(List<string> distinct)
    {
        List<List<int>> batches = [];
        List<int> current = [];
        int currentChars = 0;

        for (int i = 0; i < distinct.Count; i++)
        {
            int itemChars = distinct[i].Length + 6; // "NN. " plus newline

            if (current.Count > 0 && (current.Count >= MaxBatchItems || currentChars + itemChars > MaxBatchChars))
            {
                batches.Add(current);
                current = [];
                currentChars = 0;
            }

            current.Add(i);
            currentChars += itemChars;
        }

        if (current.Count > 0)
            batches.Add(current);

        return batches;
    }

    private static async Task<WinAiGenerationResult> TranslateBatchChunkAsync(
        string systemPrompt,
        List<string> distinct,
        List<int> batch,
        Dictionary<string, List<int>> byText,
        string[] results,
        Action<int, string> onItemTranslated,
        CancellationToken cancellationToken)
    {
        StringBuilder promptBuilder = new();
        for (int position = 0; position < batch.Count; position++)
            promptBuilder.Append(position + 1).Append(". ").AppendLine(distinct[batch[position]]);

        // Streaming: apply each numbered line the moment the model finishes generating it.
        StringBuilder streamed = new();
        int appliedLines = 0;
        Lock streamLock = new();

        void OnDelta(string delta)
        {
            lock (streamLock)
            {
                streamed.Append(delta);
                string[] lines = streamed.ToString().Split('\n');

                // The last element is still being generated, so stop one short of it.
                for (; appliedLines < lines.Length - 1; appliedLines++)
                    ApplyLine(lines[appliedLines], distinct, batch, byText, results, onItemTranslated);
            }
        }

        WinAiGenerationResult outcome = await WinAiLanguageModel.GenerateAsync(
            systemPrompt, promptBuilder.ToString(), Temperature, OnDelta, cancellationToken);

        if (outcome.Text is null)
        {
            // Split the batch and retry each half, but only when the prompt was the problem.
            if (outcome.Failure is not WinAiFailure.PromptTooLong || batch.Count < 2)
                return outcome;

            int middle = batch.Count / 2;

            WinAiGenerationResult first = await TranslateBatchChunkAsync(
                systemPrompt, distinct, [.. batch[..middle]], byText, results, onItemTranslated, cancellationToken);
            WinAiGenerationResult second = await TranslateBatchChunkAsync(
                systemPrompt, distinct, [.. batch[middle..]], byText, results, onItemTranslated, cancellationToken);

            return first.Text is null ? first : second;
        }

        // Authoritative pass over the completed response; the streamed pass above is only a preview.
        foreach (string line in outcome.Text.Split('\n'))
            ApplyLine(line, distinct, batch, byText, results, onItemTranslated);

        return outcome;
    }

    private static void ApplyLine(
        string line,
        List<string> distinct,
        List<int> batch,
        Dictionary<string, List<int>> byText,
        string[] results,
        Action<int, string> onItemTranslated)
    {
        Match match = NumberedItemRegex().Match(line.TrimEnd('\r'));
        if (!match.Success)
            return;

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int position))
            return;

        position--; // the prompt numbers from 1
        if (position < 0 || position >= batch.Count)
            return;

        string translated = CleanResult(match.Groups[2].Value);
        if (string.IsNullOrWhiteSpace(translated))
            return;

        string source = distinct[batch[position]];
        if (!byText.TryGetValue(source, out List<int>? indices))
            return;

        foreach (int index in indices)
        {
            if (string.Equals(results[index], translated, StringComparison.Ordinal))
                continue;

            results[index] = translated;
            onItemTranslated(index, translated);
        }
    }

    #endregion batched text

    /// <summary>
    /// Light tidy-up of a model response, shared with the other language model features.
    /// </summary>
    internal static string CleanResult(string text) => WinAiLanguageModel.CleanResponse(text);
}
