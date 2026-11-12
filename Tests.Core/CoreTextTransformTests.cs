using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Core.Actions;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

/// <summary>
/// Each registered Core transform must call through to the <see cref="StringMethods"/> function it
/// wraps: the same input gives the same output as calling the function directly.
/// </summary>
public class CoreTextTransformTests
{
    private const string Messy = "  alpha  \r\n\r\nbeta\r\n  alpha  \r\ngamma one\r\n\t\r\n";
    private const string GuidLike = "2f8a8c1o-9b3e-4d1a-8c2i-0e6a\r\n-5b7d3f1c9a20";
    private const string Mixed = "Hello World 0123 lO5S zb";
    private const string Reserved = "a file: name? with *stars* / and \\ \"quotes\"";

    private static readonly ActionRegistry Registry = CreateRegistry();

    private static ActionRegistry CreateRegistry()
    {
        ActionRegistry registry = new();
        CoreActions.RegisterAll(registry);
        return registry;
    }

    private static async Task<string> ApplyAsync(string id, string input, JsonElement? config = null)
    {
        ITextTransform transform = Registry.GetTransform(id)
            ?? throw new InvalidOperationException($"{id} is not registered as a transform.");

        return await transform.ApplyAsync(input, config, CancellationToken.None);
    }

    [Theory]
    [InlineData(Messy)]
    [InlineData("")]
    [InlineData("no newline")]
    public async Task TrimLines_MatchesTrimEachLine(string input)
        => Assert.Equal(input.TrimEachLine(), await ApplyAsync("textgrab.text.trimLines", input));

    [Theory]
    [InlineData(Messy)]
    [InlineData("one\ntwo\rthree")]
    [InlineData("already one line")]
    public async Task MakeSingleLine_MatchesMakeStringSingleLine(string input)
        => Assert.Equal(input.MakeStringSingleLine(), await ApplyAsync("textgrab.text.makeSingleLine", input));

    [Theory]
    [InlineData(Messy)]
    [InlineData("a\r\nb\r\na\r\nb")]
    public async Task RemoveDuplicateLines_MatchesRemoveDuplicateLines(string input)
        => Assert.Equal(input.RemoveDuplicateLines(), await ApplyAsync("textgrab.text.removeDuplicateLines", input));

    [Theory]
    [InlineData("one\r\ntwo\r\nthree\r\nfour\r\nfive")]
    [InlineData("one\r\ntwo\r\nthree\r\n")]
    public async Task ShuffleLines_KeepsTheSameLinesAsShuffleLines(string input)
    {
        // ShuffleLines draws from Random.Shared, so compare what it keeps rather than the order.
        string expected = input.ShuffleLines();
        string actual = await ApplyAsync("textgrab.text.shuffleLines", input);

        Assert.Equal(Lines(expected), Lines(actual));
        Assert.Equal(expected.EndsWith(Environment.NewLine), actual.EndsWith(Environment.NewLine));

        static string[] Lines(string text)
            => [.. text.Split(Environment.NewLine).Order(StringComparer.Ordinal)];
    }

    [Theory]
    [InlineData("single line")]
    [InlineData("")]
    public async Task ShuffleLines_LeavesSingleLineTextAlone(string input)
        => Assert.Equal(input.ShuffleLines(), await ApplyAsync("textgrab.text.shuffleLines", input));

    [Theory]
    [InlineData(Mixed)]
    [InlineData("SOS log 1O")]
    public async Task TryMakeNumbers_MatchesTryFixToNumbers(string input)
        => Assert.Equal(input.TryFixToNumbers(), await ApplyAsync("textgrab.text.tryMakeNumbers", input));

    [Theory]
    [InlineData(Mixed)]
    [InlineData("h3ll0 w0r1d")]
    public async Task TryMakeLetters_MatchesTryFixToLetters(string input)
        => Assert.Equal(input.TryFixToLetters(), await ApplyAsync("textgrab.text.tryMakeLetters", input));

    [Theory]
    [InlineData(GuidLike)]
    [InlineData("no guid here")]
    public async Task CorrectGuidErrors_MatchesCorrectCommonGuidErrors(string input)
        => Assert.Equal(input.CorrectCommonGuidErrors(), await ApplyAsync("textgrab.text.correctGuidErrors", input));

    [Theory]
    [InlineData(Reserved)]
    [InlineData("plain")]
    public async Task ReplaceReservedCharacters_MatchesReplaceReservedCharacters(string input)
        => Assert.Equal(input.ReplaceReservedCharacters(), await ApplyAsync("textgrab.text.replaceReservedCharacters", input));

    [Fact]
    public void ConfigFreeTransforms_DeclareNoConfigType()
    {
        string[] configFree =
        [
            "textgrab.text.trimLines",
            "textgrab.text.makeSingleLine",
            "textgrab.text.removeDuplicateLines",
            "textgrab.text.shuffleLines",
            "textgrab.text.tryMakeNumbers",
            "textgrab.text.tryMakeLetters",
            "textgrab.text.correctGuidErrors",
            "textgrab.text.replaceReservedCharacters",
        ];

        foreach (string id in configFree)
        {
            Assert.True(Registry.TryGet(id, out ActionDescriptor? descriptor), id);
            Assert.Null(descriptor.ConfigType);
            Assert.Equal(ActionKind.TextTransform, descriptor.Kind);
            Assert.False(descriptor.RequiresCopilotPlus);
        }
    }

    [Fact]
    public void ReplaceReservedCharacters_KeepsTheWindowsCtrlRGesture()
    {
        Assert.True(Registry.TryGet("textgrab.text.replaceReservedCharacters", out ActionDescriptor? descriptor));
        Assert.Equal("Ctrl+R", descriptor.DefaultGesture);
    }

    [Fact]
    public void EveryCoreActionIsAnImplementedTransform()
    {
        foreach (ActionDescriptor descriptor in Registry.All)
        {
            Assert.Equal(ActionKind.TextTransform, descriptor.Kind);
            Assert.NotNull(Registry.GetTransform(descriptor.Id));
        }
    }
}
