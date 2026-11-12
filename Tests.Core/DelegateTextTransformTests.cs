using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Core.Actions;

namespace Text_Grab.Tests.Core;

public class DelegateTextTransformTests
{
    private static ActionDescriptor Descriptor(ActionKind kind = ActionKind.TextTransform, Type? configType = null)
        => new("textgrab.text.upper", "Upper", "Icon24", ActionCategory.Case, kind, configType);

    [Fact]
    public async Task ApplyAsync_ReturnsTheFunctionsResult()
    {
        DelegateTextTransform transform = new(Descriptor(), static text => text.ToUpperInvariant());

        Assert.Equal("ABC", await transform.ApplyAsync("abc", null, CancellationToken.None));
    }

    [Fact]
    public async Task ApplyAsync_IgnoresConfig()
    {
        DelegateTextTransform transform = new(Descriptor(), static text => text.ToUpperInvariant());
        using JsonDocument config = JsonDocument.Parse("""{"anything": 1}""");

        Assert.Equal("ABC", await transform.ApplyAsync("abc", config.RootElement, CancellationToken.None));
    }

    [Fact]
    public async Task ApplyAsync_CancelledToken_ThrowsWithoutCallingTheFunction()
    {
        bool called = false;
        DelegateTextTransform transform = new(Descriptor(), text => { called = true; return text; });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await transform.ApplyAsync("abc", null, new CancellationToken(canceled: true)));
        Assert.False(called);
    }

    [Fact]
    public async Task ApplyAsync_NullInput_Throws()
    {
        DelegateTextTransform transform = new(Descriptor(), static text => text);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await transform.ApplyAsync(null!, null, CancellationToken.None));
    }

    [Fact]
    public void Constructor_NonTransformKind_Throws()
        => Assert.Throws<ArgumentException>(() => new DelegateTextTransform(Descriptor(ActionKind.EditorCommand), static text => text));

    [Fact]
    public void Constructor_DescriptorWithConfigType_Throws()
        => Assert.Throws<ArgumentException>(() => new DelegateTextTransform(Descriptor(configType: typeof(string)), static text => text));

    // ---- DelegateTextTransform<TConfig> ----

    public sealed record RepeatConfig(int Times, string Joiner = "");

    private static DelegateTextTransform<RepeatConfig> Repeat(RepeatConfig? defaultConfig = null)
        => new(
            Descriptor(configType: typeof(RepeatConfig)),
            static (text, config) => string.Join(config.Joiner, Enumerable.Repeat(text, config.Times)),
            defaultConfig);

    private static async Task<string> ApplyAsync(ITextTransform transform, string input, string? configJson)
    {
        if (configJson is null)
            return await transform.ApplyAsync(input, null, CancellationToken.None);

        using JsonDocument config = JsonDocument.Parse(configJson);
        return await transform.ApplyAsync(input, config.RootElement.Clone(), CancellationToken.None);
    }

    [Theory]
    [InlineData("""{"times": 3}""", "ababab")]
    [InlineData("""{"TIMES": 2, "joiner": "-"}""", "ab-ab")]
    [InlineData("""{"times": 2, "somethingNew": true}""", "abab")]
    public async Task Configured_ReadsTheConfigCaseInsensitivelyAndIgnoresUnknownProperties(string configJson, string expected)
        => Assert.Equal(expected, await ApplyAsync(Repeat(), "ab", configJson));

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    public async Task Configured_NoConfig_UsesTheDefault(string? configJson)
        => Assert.Equal("ab+ab", await ApplyAsync(Repeat(new RepeatConfig(2, "+")), "ab", configJson));

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"joiner": "-"}""")]
    [InlineData("""{"times": "3"}""")]
    [InlineData("""{"times": 3, "joiner": null}""")]
    [InlineData("3")]
    public async Task Configured_MissingOrInvalidConfig_ThrowsArgumentException(string? configJson)
        => await Assert.ThrowsAsync<ArgumentException>(() => ApplyAsync(Repeat(), "ab", configJson));

    [Fact]
    public async Task Configured_CancelledToken_Throws()
        => await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await Repeat(new RepeatConfig(1)).ApplyAsync("ab", null, new CancellationToken(canceled: true)));

    [Fact]
    public void Configured_DescriptorWithADifferentConfigType_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DelegateTextTransform<RepeatConfig>(
            Descriptor(), static (text, _) => text));
        Assert.Throws<ArgumentException>(() => new DelegateTextTransform<RepeatConfig>(
            Descriptor(configType: typeof(string)), static (text, _) => text));
    }

    [Fact]
    public void Configured_NonTransformKind_Throws()
        => Assert.Throws<ArgumentException>(() => new DelegateTextTransform<RepeatConfig>(
            Descriptor(ActionKind.AppCommand, typeof(RepeatConfig)), static (text, _) => text));
}
