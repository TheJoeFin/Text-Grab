using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Text;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Text_Grab.Utilities;

/// <summary>Why a request to the Windows AI language model did not produce text.</summary>
internal enum WinAiFailure
{
    /// <summary>The model answered.</summary>
    None,

    /// <summary>This device cannot run the Windows AI language model at all.</summary>
    Unavailable,

    /// <summary>The model exists but could not be prepared or created.</summary>
    ModelNotReady,

    /// <summary>The prompt did not fit the model's context.</summary>
    PromptTooLong,

    /// <summary>Content moderation or policy rejected the prompt or the response.</summary>
    Blocked,

    /// <summary>The model reported an error, or returned nothing usable.</summary>
    ModelError,
}

/// <summary>The result of one generation: either text, or a reason there is none.</summary>
internal readonly record struct WinAiGenerationResult(string? Text, WinAiFailure Failure, string? Message)
{
    internal bool Succeeded => Failure is WinAiFailure.None && Text is not null;

    internal static WinAiGenerationResult Ok(string text) => new(text, WinAiFailure.None, null);

    internal static WinAiGenerationResult Failed(WinAiFailure failure, string message) =>
        new(null, failure, message);
}

/// <summary>
/// Shared access to the Windows AI Foundry <see cref="LanguageModel"/> (Phi Silica), following the
/// pattern used by microsoft/ai-dev-gallery's PhiSilicaClient.
///
/// Everything in Text-Grab that prompts the language model — translation, meeting notes, regex
/// extraction — goes through here so they all get the same three things:
///   1. The Limited Access Feature unlock, checked once and reported with a message a user can act
///      on rather than an "Access is denied" exception from deep inside the model call.
///   2. One <see cref="LanguageModel"/> created and reused across features; only the lightweight
///      per-request <see cref="LanguageModelContext"/> is rebuilt, so turns never accumulate and
///      the second feature to run does not pay the model creation cost again.
///   3. Prompting the model directly through GenerateResponseAsync with a purpose-built system
///      prompt, instead of bending a skill such as TextRewriter into the job (whose own system
///      prompt fights the instruction and leaves instruction echoes in the output).
///
/// Every failure path returns a <see cref="WinAiFailure"/> and a human-readable message so callers
/// can tell the user why nothing came back.
/// </summary>
internal static class WinAiLanguageModel
{
    private static LanguageModel? _languageModel;
    private static readonly SemaphoreSlim _modelLock = new(1, 1);

    // Phi Silica serves one generation at a time; queueing here keeps concurrent callers from
    // interleaving requests on the shared model, which previously showed up as long stalls.
    private static readonly SemaphoreSlim _inferenceLock = new(1, 1);
    private static bool _disposed;

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
            return (false, "The on-device Windows AI language model requires Windows 11.");

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

    /// <summary>Returns the shared model, creating (and preparing, if needed) it on first use.</summary>
    internal static async Task<(LanguageModel? Model, string? Error)> GetModelAsync(CancellationToken cancellationToken)
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
    /// Drops the cached language model to free the memory it holds. The next request recreates it,
    /// so call this when a feature is switched off rather than between requests.
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

    /// <summary>
    /// Runs a single prompt against the shared model, taking care of availability, model creation
    /// and the inference queue. Callers issuing several related requests should take a lease from
    /// <see cref="AcquireInferenceAsync"/> and call <see cref="GenerateAsync"/> themselves, so
    /// their requests are not interleaved with another feature's.
    /// </summary>
    internal static async Task<WinAiGenerationResult> PromptAsync(
        string systemPrompt,
        string prompt,
        float temperature = 0.2f,
        Action<string>? onPartial = null,
        CancellationToken cancellationToken = default)
    {
        (bool available, string? reason) = CheckAvailability();
        if (!available)
            return WinAiGenerationResult.Failed(
                WinAiFailure.Unavailable, reason ?? "Windows AI is not available on this device.");

        try
        {
            (LanguageModel? model, string? error) = await GetModelAsync(cancellationToken);
            if (model is null)
                return WinAiGenerationResult.Failed(
                    WinAiFailure.ModelNotReady, error ?? "The Windows AI language model could not be started.");

            using IDisposable lease = await AcquireInferenceAsync(cancellationToken);
            return await GenerateAsync(model, systemPrompt, prompt, temperature, onPartial, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Language model request failed: {ex.Message}");
            return WinAiGenerationResult.Failed(WinAiFailure.ModelError, $"The language model failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Waits for exclusive use of the shared model. Dispose the returned lease to let the next
    /// caller in.
    /// </summary>
    internal static async Task<IDisposable> AcquireInferenceAsync(CancellationToken cancellationToken)
    {
        await _inferenceLock.WaitAsync(cancellationToken);
        return new InferenceLease();
    }

    private sealed class InferenceLease : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released || _disposed)
                return;

            _released = true;
            _inferenceLock.Release();
        }
    }

    /// <summary>Runs one generation against the shared model. The caller holds the inference lease.</summary>
    /// <param name="onDelta">Receives generated text as it streams in, for live UI updates.</param>
    internal static async Task<WinAiGenerationResult> GenerateAsync(
        LanguageModel model,
        string systemPrompt,
        string prompt,
        float temperature,
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
            return WinAiGenerationResult.Failed(
                WinAiFailure.ModelError, $"The language model could not accept the request: {ex.Message}");
        }

        // Advisory pre-check only. If it cannot answer, send the prompt anyway and let the model
        // report PromptLargerThanContext — treating an unknown length as "too long" would turn
        // every request into a silent no-op.
        try
        {
            ulong usableLength = model.GetUsablePromptLength(context, prompt);
            if (usableLength > 0 && (ulong)prompt.Length > usableLength)
                return WinAiGenerationResult.Failed(
                    WinAiFailure.PromptTooLong, "The text is longer than the language model's context.");
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
                Temperature = temperature,
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
            return WinAiGenerationResult.Failed(
                WinAiFailure.ModelError, $"The language model failed to respond: {ex.Message}");
        }

        switch (result.Status)
        {
            case LanguageModelResponseStatus.Complete:
                break;

            case LanguageModelResponseStatus.PromptLargerThanContext:
                return WinAiGenerationResult.Failed(
                    WinAiFailure.PromptTooLong, "The text is longer than the language model's context.");

            case LanguageModelResponseStatus.BlockedByPolicy:
            case LanguageModelResponseStatus.PromptBlockedByContentModeration:
            case LanguageModelResponseStatus.ResponseBlockedByContentModeration:
                return WinAiGenerationResult.Failed(
                    WinAiFailure.Blocked,
                    $"Windows AI blocked this text ({result.Status}). Content moderation rejected the request.");

            default:
                string detail = result.ExtendedError?.Message ?? result.Status.ToString();
                return WinAiGenerationResult.Failed(
                    WinAiFailure.ModelError, $"The language model returned an error: {detail}");
        }

        string text = result.Text ?? string.Empty;

        return string.IsNullOrWhiteSpace(text)
            ? WinAiGenerationResult.Failed(WinAiFailure.ModelError, "The language model returned an empty response.")
            : WinAiGenerationResult.Ok(text);
    }

    /// <summary>
    /// Light tidy-up of a model response. Deliberately conservative: an earlier translation
    /// implementation tried to detect and strip "instruction echoes" and would silently hand back
    /// the untranslated input whenever the guess misfired. Prompting the model directly removes the
    /// echoes at the source, so all that is left is trimming stray fences and wrapping quotes.
    /// </summary>
    internal static string CleanResponse(string text)
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

    #endregion generation
}
