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

    /// <summary>
    /// The connection to the out-of-process Windows AI runtime dropped, which surfaces as
    /// "The RPC server is unavailable". Recoverable by restarting the model.
    /// </summary>
    Disconnected,

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
///   4. Recovery from a dropped connection to the Windows AI runtime: the model runs out of
///      process, so when that process is recycled, updated or crashes, every object this process
///      holds becomes a dead proxy and keeps failing with "The RPC server is unavailable" until
///      it is thrown away and remade. That is done here rather than by restarting Text-Grab.
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
    /// <remarks>
    /// Private on purpose: a caller that held on to the returned model would keep using it after a
    /// dropped connection forced a restart. Features call <see cref="EnsureModelAsync"/> to check
    /// the model can be started, then <see cref="GenerateAsync"/>, which always uses the current one.
    /// </remarks>
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
    /// Makes sure the shared model exists and is ready, without handing it out. Features call this
    /// up front so "the model could not be started" is reported before any work begins.
    /// </summary>
    internal static async Task<(bool Ready, string? Error)> EnsureModelAsync(CancellationToken cancellationToken)
    {
        (LanguageModel? model, string? error) = await GetModelAsync(cancellationToken);
        return (model is not null, error);
    }

    /// <summary>
    /// HRESULTs that mean this process is holding a proxy to a Windows AI runtime that is no longer
    /// there. The model runs out of process, so a service restart, a model update or a crash leaves
    /// every object created before it permanently dead: the next call comes back as "The RPC server
    /// is unavailable" (0x800706BA) and every call after it does too, until new objects are made.
    /// </summary>
    private static readonly int[] _connectionLostHResults =
    [
        unchecked((int)0x800706BA), // RPC_S_SERVER_UNAVAILABLE — "The RPC server is unavailable."
        unchecked((int)0x800706BB), // RPC_S_SERVER_TOO_BUSY
        unchecked((int)0x800706BE), // RPC_S_CALL_FAILED
        unchecked((int)0x800706BF), // RPC_S_CALL_FAILED_DNE
        unchecked((int)0x800706B5), // RPC_S_UNKNOWN_IF
        unchecked((int)0x80010108), // RPC_E_DISCONNECTED — the object invoked has disconnected
        unchecked((int)0x80010105), // RPC_E_SERVERFAULT
        unchecked((int)0x800401FD), // CO_E_OBJNOTCONNECTED
    ];

    /// <summary>Whether <paramref name="exception"/> means the Windows AI runtime went away.</summary>
    internal static bool IsConnectionLost(Exception? exception)
    {
        for (Exception? ex = exception; ex is not null; ex = ex.InnerException)
            if (Array.IndexOf(_connectionLostHResults, ex.HResult) >= 0)
                return true;

        return false;
    }

    /// <summary>
    /// Throws away the shared model, and any cached reason it could not be unlocked, so the next
    /// request builds a fresh connection to the Windows AI runtime. <see cref="GenerateAsync"/> and
    /// <see cref="RunWithModelAsync"/> do this by themselves when a request finds the runtime gone;
    /// call it directly to offer the user a manual "try again" without restarting Text-Grab.
    /// </summary>
    internal static async Task RestartModelAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        await _modelLock.WaitAsync(cancellationToken);
        try
        {
            _languageModel?.Dispose();
        }
        catch (Exception ex)
        {
            // Disposing a dead proxy can throw; the reference is dropped either way.
            Debug.WriteLine($"Disposing the language model failed: {ex.Message}");
        }
        finally
        {
            _languageModel = null;
            _modelLock.Release();
        }

        // A failure to unlock the Limited Access Feature is cached for the life of the process, so
        // forget it too: a transient failure there would otherwise fail the retry before it starts.
        LimitedAccessFeatureUtilities.ResetUnlockCache();
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
            using IDisposable lease = await AcquireInferenceAsync(cancellationToken);
            return await GenerateAsync(systemPrompt, prompt, temperature, onPartial, cancellationToken);
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

    /// <summary>
    /// Runs one generation against the shared model. The caller holds the inference lease.
    ///
    /// When the request fails because the connection to the Windows AI runtime dropped — the
    /// service was restarted, updated or recycled while Text-Grab held a proxy to it, reported as
    /// "The RPC server is unavailable" — the model is thrown away, remade, and the request is tried
    /// once more. Without that, every later request fails on the same dead proxy and the feature
    /// only comes back when Text-Grab is restarted.
    /// </summary>
    /// <param name="onDelta">Receives generated text as it streams in, for live UI updates.</param>
    internal static async Task<WinAiGenerationResult> GenerateAsync(
        string systemPrompt,
        string prompt,
        float temperature,
        Action<string>? onDelta,
        CancellationToken cancellationToken)
    {
        (LanguageModel? model, string? error) = await GetModelAsync(cancellationToken);
        if (model is null)
            return WinAiGenerationResult.Failed(
                WinAiFailure.ModelNotReady, error ?? "The Windows AI language model could not be started.");

        // Records whether the failed attempt already pushed text at the UI, so the retry does not
        // stream a second copy of the response into it.
        bool alreadyStreamed = false;
        Action<string>? firstDelta = onDelta is null
            ? null
            : delta => { alreadyStreamed = true; onDelta(delta); };

        WinAiGenerationResult result =
            await GenerateOnceAsync(model, systemPrompt, prompt, temperature, firstDelta, cancellationToken);

        if (result.Failure is not WinAiFailure.Disconnected)
            return result;

        Debug.WriteLine($"Windows AI connection lost, restarting the language model: {result.Message}");
        await RestartModelAsync(cancellationToken);

        (model, error) = await GetModelAsync(cancellationToken);
        if (model is null)
            return WinAiGenerationResult.Failed(
                WinAiFailure.Disconnected,
                $"Windows AI stopped responding and could not be restarted: {error ?? result.Message}");

        WinAiGenerationResult retry = await GenerateOnceAsync(
            model, systemPrompt, prompt, temperature, alreadyStreamed ? null : onDelta, cancellationToken);

        return retry.Failure is WinAiFailure.Disconnected
            ? WinAiGenerationResult.Failed(
                WinAiFailure.Disconnected,
                "Windows AI stopped responding, and restarting the model did not bring it back. " +
                "Try again in a moment, or restart Text-Grab.")
            : retry;
    }

    /// <summary>
    /// Runs <paramref name="work"/> against the shared model, for the WinAppSDK text skills
    /// (summarize, rewrite, text-to-table) which take a <see cref="LanguageModel"/> of their own
    /// instead of going through <see cref="GenerateAsync"/>. Takes the inference lease, and
    /// restarts the model and tries once more when the Windows AI runtime connection has dropped.
    /// </summary>
    /// <returns>The skill's result, or null and a message saying why there is none.</returns>
    internal static async Task<(T? Value, string? Error)> RunWithModelAsync<T>(
        Func<LanguageModel, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default) where T : class
    {
        (bool available, string? reason) = CheckAvailability();
        if (!available)
            return (null, reason ?? "Windows AI is not available on this device.");

        try
        {
            using IDisposable lease = await AcquireInferenceAsync(cancellationToken);

            (LanguageModel? model, string? error) = await GetModelAsync(cancellationToken);
            if (model is null)
                return (null, error ?? "The Windows AI language model could not be started.");

            try
            {
                return (await work(model, cancellationToken), null);
            }
            catch (Exception ex) when (IsConnectionLost(ex))
            {
                Debug.WriteLine($"Windows AI connection lost, restarting the language model: {ex.Message}");
                await RestartModelAsync(cancellationToken);

                (model, error) = await GetModelAsync(cancellationToken);
                if (model is null)
                    return (null, $"Windows AI stopped responding and could not be restarted: {error ?? ex.Message}");

                return (await work(model, cancellationToken), null);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Language model request failed: {ex.Message}");

            return (null, IsConnectionLost(ex)
                ? "Windows AI stopped responding, and restarting the model did not bring it back. " +
                  "Try again in a moment, or restart Text-Grab."
                : ex.Message);
        }
    }

    /// <summary>One attempt at a generation, with no recovery of its own.</summary>
    private static async Task<WinAiGenerationResult> GenerateOnceAsync(
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
                Classify(ex), $"The language model could not accept the request: {ex.Message}");
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
                Classify(ex), $"The language model failed to respond: {ex.Message}");
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
                Exception? extendedError = result.ExtendedError;
                string detail = extendedError?.Message ?? result.Status.ToString();
                return WinAiGenerationResult.Failed(
                    Classify(extendedError), $"The language model returned an error: {detail}");
        }

        string text = result.Text ?? string.Empty;

        return string.IsNullOrWhiteSpace(text)
            ? WinAiGenerationResult.Failed(WinAiFailure.ModelError, "The language model returned an empty response.")
            : WinAiGenerationResult.Ok(text);
    }

    /// <summary>A lost connection is worth retrying after a restart; anything else is not.</summary>
    private static WinAiFailure Classify(Exception? exception) =>
        IsConnectionLost(exception) ? WinAiFailure.Disconnected : WinAiFailure.ModelError;

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
