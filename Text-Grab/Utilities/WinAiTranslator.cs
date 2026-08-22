using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

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
/// On-device translation built directly on the Windows AI Foundry <see cref="LanguageModel"/>
/// (Phi Silica), following the pattern used by microsoft/ai-dev-gallery's PhiSilicaClient and
/// Translate samples.
///
/// The three things that make this fast compared to the previous implementation:
///   1. The model is prompted directly through GenerateResponseAsync with a system prompt created
///      by CreateContext, instead of being funneled through the TextRewriter skill (whose own
///      "rewrite this text" system prompt fought the translation instruction and produced
///      instruction echoes in the output).
///   2. The <see cref="LanguageModel"/> is created once and reused; only the lightweight
///      per-request <see cref="LanguageModelContext"/> is rebuilt so turns never accumulate.
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

    private static LanguageModel? _languageModel;
    private static readonly SemaphoreSlim _modelLock = new(1, 1);

    // Phi Silica serves one generation at a time; queueing here keeps concurrent callers from
    // interleaving requests on the shared model, which previously showed up as long stalls.
    private static readonly SemaphoreSlim _inferenceLock = new(1, 1);
    private static bool _disposed;

    [GeneratedRegex(@"^\s*(\d+)\s*[.):\]]\s*(.*)$")]
    private static partial Regex NumberedItemRegex();

    #region availability

    /// <summary>
    /// Whether this device can run the Windows AI language model, and why not when it cannot.
    /// Unlike the OCR checks this asks <see cref="LanguageModel.GetReadyState"/> directly rather
    /// than assuming ARM64, so Intel/AMD Copilot+ PCs are included and unsupported hardware is
    /// excluded properly.
    /// </summary>
    internal static (bool Available, string? Reason) CheckAvailability()
    {
        if (!AppUtilities.IsPackaged())
            return (false, "Windows AI is only available when Text-Grab runs as an installed (packaged) app.");

        if (OSInterop.IsWindows10())
            return (false, "On-device translation requires Windows 11.");

        try
        {
            AIFeatureReadyState readyState = LanguageModel.GetReadyState();

            if (readyState is AIFeatureReadyState.NotSupportedOnCurrentSystem)
                return (false, "This device does not support the on-device Windows AI language model. It requires a Copilot+ PC.");

            if (readyState is AIFeatureReadyState.DisabledByUser)
                return (false, "The Windows AI language model is turned off in Windows Settings.");

            // Microsoft ships the language model as a Limited Access Feature, so it must be
            // unlocked before any call to it will succeed. Do it here, ahead of CreateAsync and
            // CreateContext, so the failure is reported once and clearly.
            (bool unlocked, string? unlockReason) = LimitedAccessFeatureUtilities.TryUnlockLanguageModel();

            return unlocked ? (true, null) : (false, unlockReason);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LanguageModel.GetReadyState failed: {ex.Message}");
            return (false, $"Windows AI could not be reached on this device: {ex.Message}");
        }
    }

    /// <summary>True when this device can run the Windows AI language model.</summary>
    internal static bool IsAvailable() => CheckAvailability().Available;

    private static async Task<(LanguageModel? Model, string? Error)> GetModelAsync(CancellationToken cancellationToken)
    {
        if (_languageModel is not null)
            return (_languageModel, null);

        await _modelLock.WaitAsync(cancellationToken);
        try
        {
            if (_languageModel is not null)
                return (_languageModel, null);

            (bool available, string? reason) = CheckAvailability();
            if (!available)
                return (null, reason);

            if (LanguageModel.GetReadyState() is AIFeatureReadyState.NotReady)
            {
                // First run may download the model; the token lets the user back out of the wait.
                AIFeatureReadyResult readyResult = await LanguageModel.EnsureReadyAsync().AsTask(cancellationToken);
                if (readyResult.Status != AIFeatureReadyResultState.Success)
                {
                    string detail = readyResult.ExtendedError?.Message ?? readyResult.Status.ToString();
                    return (null, $"The Windows AI language model could not be prepared ({detail}). " +
                                   "It may still be downloading — try again in a few minutes.");
                }
            }

            _languageModel = await LanguageModel.CreateAsync().AsTask(cancellationToken);
            return (_languageModel, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LanguageModel creation failed: {ex.Message}");
            return (null, $"The Windows AI language model could not be started: {ex.Message}");
        }
        finally
        {
            _modelLock.Release();
        }
    }

    /// <summary>
    /// Drops the cached language model to free the memory it holds. The next translation recreates
    /// it, so call this when translation is switched off rather than between translations.
    /// </summary>
    internal static void ReleaseModel()
    {
        if (_disposed)
            return;

        _languageModel?.Dispose();
        _languageModel = null;
    }

    /// <summary>Releases the shared language model. Call once during application shutdown.</summary>
    internal static void Cleanup()
    {
        if (_disposed)
            return;

        ReleaseModel();
        _modelLock.Dispose();
        _inferenceLock.Dispose();
        _disposed = true;
    }

    #endregion availability

    #region generation

    /// <summary>The result of one generation: either text, or a reason there is none.</summary>
    private readonly record struct GenerationOutcome(string? Text, TranslationFailure Failure, string? Message)
    {
        internal static GenerationOutcome Ok(string text) => new(text, TranslationFailure.None, null);

        internal static GenerationOutcome Failed(TranslationFailure failure, string message) =>
            new(null, failure, message);
    }

    private static string SystemPromptFor(string targetLanguage) =>
        $"You are a translation engine. Translate everything the user sends into {targetLanguage}, " +
        $"written in the native script and characters of {targetLanguage}. " +
        "Preserve the original line breaks, numbers, punctuation and formatting. " +
        "Reply with the translation only: no notes, no explanations, no quotation marks around it, " +
        "and never repeat these instructions.";

    /// <summary>Runs one generation against the shared model.</summary>
    /// <param name="onDelta">Receives generated text as it streams in, for live UI updates.</param>
    private static async Task<GenerationOutcome> GenerateAsync(
        LanguageModel model,
        string systemPrompt,
        string prompt,
        Action<string>? onDelta,
        CancellationToken cancellationToken)
    {
        LanguageModelContext context;
        try
        {
            // A fresh context per request keeps the system prompt in force without carrying previous
            // turns forward, which is what kept growing the prompt (and the latency) before.
            context = model.CreateContext(systemPrompt, new ContentFilterOptions());
        }
        catch (Exception ex)
        {
            return GenerationOutcome.Failed(
                TranslationFailure.ModelError, $"The language model could not accept the request: {ex.Message}");
        }

        // Advisory pre-check only. If it cannot answer, send the prompt anyway and let the model
        // report PromptLargerThanContext — treating an unknown length as "too long" would turn
        // every translation into a silent no-op.
        try
        {
            ulong usableLength = model.GetUsablePromptLength(context, prompt);
            if (usableLength > 0 && (ulong)prompt.Length > usableLength)
                return GenerationOutcome.Failed(
                    TranslationFailure.PromptTooLong, "The text is longer than the language model's context.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetUsablePromptLength failed, sending prompt anyway: {ex.Message}");
        }

        LanguageModelResponseResult result;
        try
        {
            LanguageModelOptions options = new()
            {
                // Translation wants the most likely wording, not a creative one.
                Temperature = 0.2f,
                ContentFilterOptions = new ContentFilterOptions(),
            };

            IAsyncOperationWithProgress<LanguageModelResponseResult, string> operation =
                model.GenerateResponseAsync(context, prompt, options);

            if (onDelta is not null)
                operation.Progress = (_, delta) =>
                {
                    if (!string.IsNullOrEmpty(delta))
                        onDelta(delta);
                };

            result = await operation.AsTask(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return GenerationOutcome.Failed(
                TranslationFailure.ModelError, $"The language model failed to respond: {ex.Message}");
        }

        switch (result.Status)
        {
            case LanguageModelResponseStatus.Complete:
                break;

            case LanguageModelResponseStatus.PromptLargerThanContext:
                return GenerationOutcome.Failed(
                    TranslationFailure.PromptTooLong, "The text is longer than the language model's context.");

            case LanguageModelResponseStatus.BlockedByPolicy:
            case LanguageModelResponseStatus.PromptBlockedByContentModeration:
            case LanguageModelResponseStatus.ResponseBlockedByContentModeration:
                return GenerationOutcome.Failed(
                    TranslationFailure.Blocked,
                    $"Windows AI blocked this text ({result.Status}). Content moderation rejected the request.");

            default:
                string detail = result.ExtendedError?.Message ?? result.Status.ToString();
                return GenerationOutcome.Failed(
                    TranslationFailure.ModelError, $"The language model returned an error: {detail}");
        }

        string text = result.Text ?? string.Empty;

        return string.IsNullOrWhiteSpace(text)
            ? GenerationOutcome.Failed(TranslationFailure.ModelError, "The language model returned an empty translation.")
            : GenerationOutcome.Ok(text);
    }

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
            (LanguageModel? model, string? error) = await GetModelAsync(cancellationToken);
            if (model is null)
                return new TranslationResult(textToTranslate, TranslationFailure.ModelNotReady, error);

            string systemPrompt = SystemPromptFor(targetLanguage);

            await _inferenceLock.WaitAsync(cancellationToken);
            try
            {
                GenerationOutcome outcome = await TranslateBlockAsync(
                    model, systemPrompt, textToTranslate, onPartial, cancellationToken);

                if (outcome.Text is null)
                    return new TranslationResult(textToTranslate, outcome.Failure, outcome.Message);

                string cleaned = CleanResult(outcome.Text);

                return string.IsNullOrWhiteSpace(cleaned)
                    ? new TranslationResult(textToTranslate, TranslationFailure.ModelError, "The translation came back empty.")
                    : TranslationResult.Success(cleaned);
            }
            finally
            {
                _inferenceLock.Release();
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
    private static async Task<GenerationOutcome> TranslateBlockAsync(
        LanguageModel model,
        string systemPrompt,
        string text,
        Action<string>? onPartial,
        CancellationToken cancellationToken)
    {
        GenerationOutcome outcome = await GenerateAsync(model, systemPrompt, text, onPartial, cancellationToken);
        if (outcome.Text is not null || outcome.Failure is not TranslationFailure.PromptTooLong)
            return outcome;

        // Too long for one pass: split roughly in half on a line break and translate each side.
        string[] pieces = SplitInHalf(text);
        if (pieces.Length < 2)
            return outcome;

        StringBuilder combined = new();
        foreach (string piece in pieces)
        {
            GenerationOutcome part = await TranslateBlockAsync(model, systemPrompt, piece, onPartial, cancellationToken);
            if (part.Text is null)
                return part;

            combined.Append(part.Text);
        }

        return GenerationOutcome.Ok(combined.ToString());
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
            (LanguageModel? model, string? error) = await GetModelAsync(cancellationToken);
            if (model is null)
                return new BatchTranslationResult(results, 0, TranslationFailure.ModelNotReady, error);

            string systemPrompt =
                "You are a translation engine. The user sends a numbered list. Translate each item into " +
                $"{targetLanguage}, written in the native script and characters of {targetLanguage}. " +
                "Reply with the same numbers in the same order, one item per line, in the form '1. translation'. " +
                "Keep exactly one output line per input line. Do not merge, reorder, add or drop items, " +
                "and do not add any commentary.";

            GenerationOutcome lastFailure = default;

            await _inferenceLock.WaitAsync(cancellationToken);
            try
            {
                foreach (List<int> batch in BuildBatches(distinct))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    GenerationOutcome outcome = await TranslateBatchChunkAsync(
                        model, systemPrompt, distinct, batch, byText, results, CountAndReport, cancellationToken);

                    if (outcome.Text is null)
                        lastFailure = outcome;
                }
            }
            finally
            {
                _inferenceLock.Release();
            }

            // Report a failure only when nothing at all came back; a partial batch failure still
            // leaves the frame better off than before.
            if (translatedCount == 0 && lastFailure.Message is not null)
                return new BatchTranslationResult(results, 0, lastFailure.Failure, lastFailure.Message);

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

    private static async Task<GenerationOutcome> TranslateBatchChunkAsync(
        LanguageModel model,
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

        GenerationOutcome outcome = await GenerateAsync(
            model, systemPrompt, promptBuilder.ToString(), OnDelta, cancellationToken);

        if (outcome.Text is null)
        {
            // Split the batch and retry each half, but only when the prompt was the problem.
            if (outcome.Failure is not TranslationFailure.PromptTooLong || batch.Count < 2)
                return outcome;

            int middle = batch.Count / 2;

            GenerationOutcome first = await TranslateBatchChunkAsync(
                model, systemPrompt, distinct, [.. batch[..middle]], byText, results, onItemTranslated, cancellationToken);
            GenerationOutcome second = await TranslateBatchChunkAsync(
                model, systemPrompt, distinct, [.. batch[middle..]], byText, results, onItemTranslated, cancellationToken);

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
    /// Light tidy-up of a model response. Deliberately conservative: the previous implementation
    /// tried to detect and strip "instruction echoes" and would silently hand back the untranslated
    /// input whenever the guess misfired. Prompting the model directly removes the echoes at the
    /// source, so all that is left is trimming stray fences and wrapping quotes.
    /// </summary>
    internal static string CleanResult(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string cleaned = text.Trim();

        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = cleaned.IndexOf('\n');
            if (firstNewline > 0)
                cleaned = cleaned[(firstNewline + 1)..];

            if (cleaned.EndsWith("```", StringComparison.Ordinal))
                cleaned = cleaned[..^3];

            cleaned = cleaned.Trim();
        }

        // Models often wrap a short answer in quotes even when told not to.
        if (cleaned.Length > 1 &&
            ((cleaned[0] == '"' && cleaned[^1] == '"') ||
             (cleaned[0] == '\'' && cleaned[^1] == '\'') ||
             (cleaned[0] == '“' && cleaned[^1] == '”')))
        {
            cleaned = cleaned[1..^1].Trim();
        }

        return cleaned;
    }
}
