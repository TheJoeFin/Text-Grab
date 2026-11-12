using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Core.Actions;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

/// <summary>
/// The Core transforms that take config: each must give the same result as calling the function it
/// wraps with the same values, and must reject config it cannot use.
/// </summary>
public class CoreConfigTransformTests
{
    private const string Column = "Name\r\nCity\r\nAnn\r\nParis\r\nBob\r\nLima";
    private const string Contact = "Call 555-0182 on 2026-11-09, or 555-0199. Budget 1200 or 3.5k.";

    private static readonly ActionRegistry Registry = CreateRegistry();

    private static ActionRegistry CreateRegistry()
    {
        ActionRegistry registry = new();
        CoreActions.RegisterAll(registry);
        return registry;
    }

    private static async Task<string> ApplyAsync(string id, string input, string? configJson)
    {
        ITextTransform transform = Registry.GetTransform(id)
            ?? throw new InvalidOperationException($"{id} is not registered as a transform.");

        if (configJson is null)
            return await transform.ApplyAsync(input, null, CancellationToken.None);

        using JsonDocument config = JsonDocument.Parse(configJson);
        return await transform.ApplyAsync(input, config.RootElement.Clone(), CancellationToken.None);
    }

    // ---- textgrab.text.cleanUp ----

    [Theory]
    [InlineData("  messy\t\ttext  \r\n\r\n\r\n\r\nαβγ ok​")]
    [InlineData("")]
    public async Task CleanUp_WithoutConfig_MatchesCleanUpTextWithoutLatinCorrection(string input)
        => Assert.Equal(input.CleanUpText(), await ApplyAsync("textgrab.text.cleanUp", input, null));

    [Theory]
    [InlineData("""{"correctToLatin": true}""", true)]
    [InlineData("""{"CorrectToLatin": false}""", false)]
    [InlineData("{}", false)]
    [InlineData("null", false)]
    public async Task CleanUp_WithConfig_MatchesCleanUpText(string configJson, bool correctToLatin)
    {
        const string input = "  αβγ   δ\r\n\r\n\r\nline ";

        Assert.Equal(input.CleanUpText(correctToLatin), await ApplyAsync("textgrab.text.cleanUp", input, configJson));
    }

    // ---- textgrab.text.unstack / unstackGroups ----

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Unstack_MatchesUnstackStrings(int columns)
        => Assert.Equal(
            Column.UnstackStrings(columns),
            await ApplyAsync("textgrab.text.unstack", Column, $$"""{"columns": {{columns}}}"""));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task UnstackGroups_MatchesUnstackGroups(int rows)
        => Assert.Equal(
            Column.UnstackGroups(rows),
            await ApplyAsync("textgrab.text.unstackGroups", Column, $$"""{"rows": {{rows}}}"""));

    [Theory]
    [InlineData("textgrab.text.unstack", null)]
    [InlineData("textgrab.text.unstack", "{}")]
    [InlineData("textgrab.text.unstack", """{"columns": 0}""")]
    [InlineData("textgrab.text.unstack", """{"columns": -2}""")]
    [InlineData("textgrab.text.unstack", """{"columns": "2"}""")]
    [InlineData("textgrab.text.unstack", """{"columns": null}""")]
    [InlineData("textgrab.text.unstack", "[2]")]
    [InlineData("textgrab.text.unstackGroups", null)]
    [InlineData("textgrab.text.unstackGroups", """{"rows": 0}""")]
    public async Task Unstack_WithoutAUsableCount_Throws(string id, string? configJson)
        => await Assert.ThrowsAsync<ArgumentException>(() => ApplyAsync(id, Column, configJson));

    // ---- textgrab.text.applyPattern ----

    [Theory]
    [InlineData("""{"pattern": "\\d{3}-\\d{4}"}""", @"\d{3}-\d{4}", "all", ", ")]
    [InlineData("""{"pattern": "\\d{3}-\\d{4}", "mode": "last"}""", @"\d{3}-\d{4}", "last", ", ")]
    [InlineData("""{"Pattern": "\\d+", "Mode": "1,3", "Separator": " | "}""", @"\d+", "1,3", " | ")]
    [InlineData("""{"pattern": "nomatch"}""", "nomatch", "all", ", ")]
    [InlineData("""{"pattern": "([unclosed"}""", "([unclosed", "all", ", ")]
    public async Task ApplyPattern_MatchesPatternExecutor(string configJson, string pattern, string mode, string separator)
    {
        PatternItem item = new(new StoredRegex { Pattern = pattern });

        Assert.Equal(
            PatternExecutor.Apply(item, Contact, mode, separator),
            await ApplyAsync("textgrab.text.applyPattern", Contact, configJson));
    }

    [Fact]
    public async Task ApplyPattern_ExtractsTheMatches()
    {
        Assert.Equal("555-0182, 555-0199", await ApplyAsync("textgrab.text.applyPattern", Contact, """{"pattern": "\\d{3}-\\d{4}"}"""));
        Assert.Equal("555-0199", await ApplyAsync("textgrab.text.applyPattern", Contact, """{"pattern": "\\d{3}-\\d{4}", "mode": "2"}"""));
    }

    [Fact]
    public async Task ApplyRecognizer_ExtractsTheMatches()
    {
        string result = await ApplyAsync("textgrab.text.applyRecognizer", "two emails: ann@example.com and bob@example.org", """{"recognizerId": "email", "output": "matchedText"}""");

        Assert.Equal("ann@example.com, bob@example.org", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("""{"pattern": ""}""")]
    [InlineData("""{"pattern": null}""")]
    public async Task ApplyPattern_WithoutAPattern_Throws(string? configJson)
        => await Assert.ThrowsAsync<ArgumentException>(() => ApplyAsync("textgrab.text.applyPattern", Contact, configJson));

    // ---- textgrab.text.applyRecognizer ----

    [Theory]
    [InlineData("""{"recognizerId": "number"}""", "number", "all", ", ", RecognizerOutputKind.ResolvedValue)]
    [InlineData("""{"recognizerId": "datetime", "mode": "first"}""", "datetime", "first", ", ", RecognizerOutputKind.ResolvedValue)]
    [InlineData("""{"recognizerId": "phonenumber", "output": "MatchedText", "separator": "; "}""", "phonenumber", "all", "; ", RecognizerOutputKind.MatchedText)]
    [InlineData("""{"recognizerId": "NUMBER", "output": 1}""", "number", "all", ", ", RecognizerOutputKind.MatchedText)]
    public async Task ApplyRecognizer_MatchesRecognizerExecutor(
        string configJson, string recognizerId, string mode, string separator, RecognizerOutputKind output)
    {
        BuiltInRecognizer recognizer = BuiltInRecognizer.GetById(recognizerId)!;

        Assert.Equal(
            RecognizerExecutor.ApplyRecognizer(recognizer, Contact, mode, separator, output),
            await ApplyAsync("textgrab.text.applyRecognizer", Contact, configJson));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("""{"recognizerId": "not-a-recognizer"}""")]
    [InlineData("""{"recognizerId": "number", "output": "Sideways"}""")]
    public async Task ApplyRecognizer_WithoutAUsableRecognizer_Throws(string? configJson)
        => await Assert.ThrowsAsync<ArgumentException>(() => ApplyAsync("textgrab.text.applyRecognizer", Contact, configJson));

    // ---- descriptors ----

    [Theory]
    [InlineData("textgrab.text.cleanUp", typeof(CleanUpTextConfig))]
    [InlineData("textgrab.text.unstack", typeof(UnstackConfig))]
    [InlineData("textgrab.text.unstackGroups", typeof(UnstackGroupsConfig))]
    [InlineData("textgrab.text.applyPattern", typeof(ApplyPatternConfig))]
    [InlineData("textgrab.text.applyRecognizer", typeof(ApplyRecognizerConfig))]
    public void ConfigTransforms_DeclareTheirConfigType(string id, Type configType)
    {
        Assert.True(Registry.TryGet(id, out ActionDescriptor? descriptor));
        Assert.Equal(configType, descriptor.ConfigType);
        Assert.Equal(ActionKind.TextTransform, descriptor.Kind);
        Assert.Same(descriptor, Registry.GetTransform(id)!.Descriptor);
    }
}
