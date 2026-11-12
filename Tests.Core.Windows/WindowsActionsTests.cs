using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Core.Actions;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

public class WindowsActionsTests
{
    /// <summary>
    /// Every action ID Core.Windows registers. IDs are persisted in users' layouts, workflows and
    /// button settings, so this list only ever grows: never rename or remove an entry to make this pass.
    /// </summary>
    private static readonly string[] PinnedIds =
    [
        "textgrab.ai.convertToTable",
        "textgrab.ai.meetingNotes",
        "textgrab.ai.rewrite",
        "textgrab.ai.summarize",
        "textgrab.ai.translate",
        "textgrab.ai.translateToSystemLanguage",
    ];

    private static readonly ActionRegistry Registry = CreateRegistry();

    private static ActionRegistry CreateRegistry()
    {
        ActionRegistry registry = new();
        WindowsActions.RegisterAll(registry);
        return registry;
    }

    private static ITextTransform Transform(string id)
        => Registry.GetTransform(id) ?? throw new InvalidOperationException($"{id} is not registered as a transform.");

    private static async Task<string> ApplyAsync(string id, string input, string? configJson = null)
    {
        if (configJson is null)
            return await Transform(id).ApplyAsync(input, null, CancellationToken.None);

        using JsonDocument config = JsonDocument.Parse(configJson);
        return await Transform(id).ApplyAsync(input, config.RootElement.Clone(), CancellationToken.None);
    }

    /// <summary>These tests cover a device without Windows AI. The test host is unpackaged, so that is every run.</summary>
    private static void SkipWhenWindowsAiIsAvailable()
        => Assert.SkipWhen(WinAiLanguageModel.IsAvailable(), "Windows AI is available in this process; these tests cover the unavailable path.");

    [Fact]
    public void RegisterAll_RegistersExactlyThePinnedIds()
    {
        string[] registered = [.. Registry.All.Select(d => d.Id).Order(StringComparer.Ordinal)];

        Assert.Equal(PinnedIds.Order(StringComparer.Ordinal), registered);
    }

    [Fact]
    public void RegisterAll_DoesNotOverlapCoreActions()
    {
        ActionRegistry registry = new();
        CoreActions.RegisterAll(registry);

        WindowsActions.RegisterAll(registry);

        Assert.Equal(registry.All.Count, registry.All.Select(d => d.Id).Distinct().Count());
    }

    [Fact]
    public void RegisterAll_Twice_Throws()
        => Assert.Throws<ArgumentException>(() => WindowsActions.RegisterAll(CreateRegistry()));

    [Fact]
    public void EveryAction_IsAnAiTextTransformThatNeedsACopilotPlusPc()
    {
        foreach (ActionDescriptor descriptor in Registry.All)
        {
            Assert.Equal(ActionKind.TextTransform, descriptor.Kind);
            Assert.Equal(ActionCategory.Ai, descriptor.Category);
            Assert.True(descriptor.RequiresCopilotPlus, descriptor.Id);
        }
    }

    [Theory]
    [InlineData("textgrab.ai.rewrite")]
    [InlineData("textgrab.ai.convertToTable")]
    public void RewriteAndConvertToTable_AreDescriptorsOnly(string id)
    {
        Assert.True(Registry.TryGet(id, out _));
        Assert.Null(Registry.GetTransform(id));
    }

    [Theory]
    [InlineData("textgrab.ai.summarize")]
    [InlineData("textgrab.ai.meetingNotes")]
    [InlineData("textgrab.ai.translate")]
    [InlineData("textgrab.ai.translateToSystemLanguage")]
    public void TheOtherAiActions_HaveTransforms(string id)
        => Assert.NotNull(Registry.GetTransform(id));

    [Fact]
    public void Translate_TakesATranslateConfig()
    {
        Assert.True(Registry.TryGet("textgrab.ai.translate", out ActionDescriptor? descriptor));
        Assert.Equal(typeof(TranslateConfig), descriptor.ConfigType);
    }

    [Theory]
    [InlineData("textgrab.ai.summarize", null)]
    [InlineData("textgrab.ai.meetingNotes", null)]
    [InlineData("textgrab.ai.translate", """{"targetLanguage": "Spanish"}""")]
    public async Task Unavailable_ThrowsInvalidOperationWithTheReason(string id, string? configJson)
    {
        SkipWhenWindowsAiIsAvailable();
        (_, string? reason) = WinAiLanguageModel.CheckAvailability();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ApplyAsync(id, "Hello there, this is a sentence to work on.", configJson));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        Assert.Contains(reason!, ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Translate_BlankText_IsReturnedUnchanged(string input)
        => Assert.Equal(input, await ApplyAsync("textgrab.ai.translate", input, """{"targetLanguage": "Spanish"}"""));

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("""{"targetLanguage": ""}""")]
    [InlineData("""{"targetLanguage": null}""")]
    public async Task Translate_WithoutATargetLanguage_ThrowsArgumentException(string? configJson)
        => await Assert.ThrowsAsync<ArgumentException>(() => ApplyAsync("textgrab.ai.translate", "Hello", configJson));

    [Fact]
    public async Task CancelledToken_ThrowsBeforeCallingWindowsAi()
    {
        foreach (string id in new[] { "textgrab.ai.summarize", "textgrab.ai.meetingNotes", "textgrab.ai.translateToSystemLanguage" })
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await Transform(id).ApplyAsync("Hello", null, new CancellationToken(canceled: true)));
    }
}
