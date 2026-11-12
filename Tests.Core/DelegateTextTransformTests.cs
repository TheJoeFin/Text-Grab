using System;
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
}
