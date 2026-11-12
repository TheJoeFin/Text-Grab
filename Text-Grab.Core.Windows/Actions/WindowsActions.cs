using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Utilities;

namespace Text_Grab.Core.Actions;

/// <summary>Config for <c>textgrab.ai.translate</c>.</summary>
/// <param name="TargetLanguage">
/// The language to translate into, by English name, as the Edit Text Window's Translate menu names
/// it: "English", "Spanish", "Chinese (Simplified)" and so on.
/// </param>
public sealed record TranslateConfig(string TargetLanguage);

/// <summary>
/// Registers the actions Text-Grab.Core.Windows implements: the on-device Windows AI text actions.
/// All of them need a Copilot+ PC; on any other device the transforms throw
/// <see cref="InvalidOperationException"/> saying why.
/// </summary>
/// <remarks>
/// Rewrite and Convert to Table are registered as descriptors only. Their services report failure
/// by returning <c>"ERROR: …"</c> as the text, which a transform cannot tell apart from a real
/// result, and <see cref="ITextTransform"/> must not return an error as a result.
/// </remarks>
public static class WindowsActions
{
    /// <summary>Registers every Core.Windows action. Call once per registry; a second call throws on the first duplicate ID.</summary>
    public static void RegisterAll(IActionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(new WindowsAiTextTransform(
            Ai("textgrab.ai.summarize", "Summarize Paragraph", "BotSparkle24"),
            SummarizeAsync));

        registry.Register(new WindowsAiTextTransform(
            Ai("textgrab.ai.meetingNotes", "Summarize as Meeting Notes", "NotepadPerson20"),
            MeetingNotesAsync));

        registry.Register(Ai("textgrab.ai.rewrite", "Rewrite", "BotSparkle24"));

        registry.Register(Ai("textgrab.ai.convertToTable", "Convert to Table", "BotSparkle24"));

        registry.Register(new WindowsAiTextTransform(
            Ai("textgrab.ai.translate", "Translate to...", "Translate24", typeof(TranslateConfig)),
            TranslateWithConfigAsync));

        registry.Register(new WindowsAiTextTransform(
            Ai("textgrab.ai.translateToSystemLanguage", "Translate to System Language", "Translate24"),
            TranslateToSystemLanguageAsync));
    }

    private static ActionDescriptor Ai(string id, string displayName, string iconKey, Type? configType = null)
        => new(id, displayName, iconKey, ActionCategory.Ai, ActionKind.TextTransform, configType, RequiresCopilotPlus: true);

    private static async Task<string> SummarizeAsync(string text, JsonElement? config, CancellationToken ct)
    {
        // SummarizeParagraph takes no token, so cancellation is only honored before it starts.
        WinAiGenerationResult result = await WindowsAiUtilities.SummarizeParagraph(text);

        return result.Text ?? throw Failed(result.Message, "The text could not be summarized.");
    }

    private static async Task<string> MeetingNotesAsync(string text, JsonElement? config, CancellationToken ct)
    {
        WinAiGenerationResult result = await WinAiMeetingNotes.SummarizeAsync(text, onProgress: null, ct);

        return result.Text ?? throw Failed(result.Message, "The text could not be written up as meeting notes.");
    }

    private static Task<string> TranslateWithConfigAsync(string text, JsonElement? config, CancellationToken ct)
    {
        TranslateConfig translate = ActionConfig.Read<TranslateConfig>(config, null, "textgrab.ai.translate");

        if (string.IsNullOrWhiteSpace(translate.TargetLanguage))
            throw new ArgumentException("'textgrab.ai.translate' needs a target language.");

        return TranslateAsync(text, translate.TargetLanguage, ct);
    }

    private static Task<string> TranslateToSystemLanguageAsync(string text, JsonElement? config, CancellationToken ct)
        => TranslateAsync(text, LanguageUtilities.GetSystemLanguageForTranslation(), ct);

    private static async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken ct)
    {
        TranslationResult result = await WinAiTranslator.TranslateAsync(text, targetLanguage, onPartial: null, ct);

        // NotNeeded means the text already reads as the target language. Its Text is the input,
        // which is the right result, so it is not a failure here.
        if (result.Succeeded || result.Failure is TranslationFailure.NotNeeded)
            return result.Text;

        throw Failed(result.Message, "The text could not be translated.");
    }

    private static InvalidOperationException Failed(string? message, string fallback)
        => new(string.IsNullOrWhiteSpace(message) ? fallback : message);

    /// <summary>An <see cref="ITextTransform"/> over an asynchronous Windows AI call.</summary>
    private sealed class WindowsAiTextTransform(
        ActionDescriptor descriptor,
        Func<string, JsonElement?, CancellationToken, Task<string>> apply) : ITextTransform
    {
        public ActionDescriptor Descriptor { get; } = descriptor;

        public async ValueTask<string> ApplyAsync(string input, JsonElement? config, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(input);
            ct.ThrowIfCancellationRequested();

            return await apply(input, config, ct);
        }
    }
}
