using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Core.Actions;

namespace Text_Grab.Tests.Core;

public class ActionRegistryTests
{
    private sealed class FakeTransform(ActionDescriptor descriptor) : ITextTransform
    {
        public ActionDescriptor Descriptor { get; } = descriptor;

        public ValueTask<string> ApplyAsync(string input, JsonElement? config, CancellationToken ct)
            => ValueTask.FromResult(input);
    }

    private static ActionDescriptor Transform(string id)
        => new(id, "Name", "Icon24", ActionCategory.Other, ActionKind.TextTransform);

    private static ActionDescriptor Command(string id)
        => new(id, "Name", "Icon24", ActionCategory.Other, ActionKind.EditorCommand);

    [Fact]
    public void Register_Transform_IsFoundByIdWithItsImplementation()
    {
        ActionRegistry registry = new();
        FakeTransform transform = new(Transform("textgrab.text.trimLines"));

        registry.Register(transform);

        Assert.True(registry.TryGet("textgrab.text.trimLines", out ActionDescriptor? descriptor));
        Assert.Same(transform.Descriptor, descriptor);
        Assert.Same(transform, registry.GetTransform("textgrab.text.trimLines"));
        Assert.Equal([transform.Descriptor], registry.All);
    }

    [Fact]
    public void Register_Descriptor_IsFoundByIdWithoutAnImplementation()
    {
        ActionRegistry registry = new();
        ActionDescriptor command = Command("textgrab.selection.isolate");

        registry.Register(command);

        Assert.True(registry.TryGet("textgrab.selection.isolate", out ActionDescriptor? descriptor));
        Assert.Same(command, descriptor);
        Assert.Null(registry.GetTransform("textgrab.selection.isolate"));
    }

    [Fact]
    public void Register_TransformKindDescriptorOnly_HasNoImplementation()
    {
        // Used for actions whose service cannot be wrapped yet, such as Windows AI rewrite.
        ActionRegistry registry = new();

        registry.Register(Transform("textgrab.ai.rewrite"));

        Assert.True(registry.TryGet("textgrab.ai.rewrite", out _));
        Assert.Null(registry.GetTransform("textgrab.ai.rewrite"));
    }

    [Fact]
    public void Lookup_UnknownId_FindsNothing()
    {
        ActionRegistry registry = new();
        registry.Register(new FakeTransform(Transform("textgrab.text.trimLines")));

        Assert.False(registry.TryGet("textgrab.text.missing", out ActionDescriptor? descriptor));
        Assert.Null(descriptor);
        Assert.Null(registry.GetTransform("textgrab.text.missing"));
    }

    [Fact]
    public void Lookup_IsCaseSensitive()
    {
        ActionRegistry registry = new();
        registry.Register(new FakeTransform(Transform("textgrab.text.trimLines")));

        Assert.False(registry.TryGet("textgrab.text.TRIMLINES", out _));
        Assert.Null(registry.GetTransform("TEXTGRAB.text.trimLines"));
    }

    [Fact]
    public void Register_DuplicateTransformId_ThrowsAndKeepsTheFirst()
    {
        ActionRegistry registry = new();
        FakeTransform first = new(Transform("textgrab.text.trimLines"));
        registry.Register(first);

        Assert.Throws<ArgumentException>(() => registry.Register(new FakeTransform(Transform("textgrab.text.trimLines"))));

        Assert.Same(first, registry.GetTransform("textgrab.text.trimLines"));
        Assert.Single(registry.All);
    }

    [Fact]
    public void Register_DescriptorWithTheIdOfATransform_Throws()
    {
        ActionRegistry registry = new();
        registry.Register(new FakeTransform(Transform("textgrab.text.trimLines")));

        Assert.Throws<ArgumentException>(() => registry.Register(Command("textgrab.text.trimLines")));
    }

    [Fact]
    public void Register_DuplicateDescriptorId_Throws()
    {
        ActionRegistry registry = new();
        registry.Register(Command("textgrab.selection.isolate"));

        Assert.Throws<ArgumentException>(() => registry.Register(Command("textgrab.selection.isolate")));
    }

    [Fact]
    public void Register_TransformWhoseKindIsNotTextTransform_Throws()
    {
        ActionRegistry registry = new();

        Assert.Throws<ArgumentException>(() => registry.Register(new FakeTransform(Command("textgrab.text.trimLines"))));
        Assert.Empty(registry.All);
    }

    [Theory]
    [InlineData("")]
    [InlineData("trimLines")]
    [InlineData("textgrab.trimLines")]
    [InlineData("textgrab.text.trim.lines")]
    [InlineData("textgrab.text.TrimLines")]
    [InlineData("textgrab.Text.trimLines")]
    [InlineData("TextGrab.text.trimLines")]
    [InlineData("other.text.trimLines")]
    [InlineData("textgrab.text.trim lines")]
    [InlineData("textgrab.text.trim-lines")]
    [InlineData("textgrab.text.")]
    [InlineData("textgrab.text.trimLines\n")]
    public void Register_InvalidId_Throws(string id)
    {
        ActionRegistry registry = new();

        Assert.Throws<ArgumentException>(() => registry.Register(Command(id)));
        Assert.Throws<ArgumentException>(() => registry.Register(new FakeTransform(Transform(id))));
        Assert.Empty(registry.All);
    }

    [Theory]
    [InlineData("textgrab.text.trimLines")]
    [InlineData("textgrab.ai.summarize")]
    [InlineData("textgrab.text.tryMakeNumbers2")]
    public void IsValidId_AcceptsTheScheme(string id)
    {
        Assert.True(ActionRegistry.IsValidId(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Register_BlankDisplayName_Throws(string displayName)
    {
        ActionRegistry registry = new();

        Assert.Throws<ArgumentException>(() => registry.Register(
            new ActionDescriptor("textgrab.text.trimLines", displayName, "Icon24", ActionCategory.Lines, ActionKind.TextTransform)));
    }

    [Fact]
    public void Register_Null_Throws()
    {
        ActionRegistry registry = new();

        Assert.Throws<ArgumentNullException>(() => registry.Register((ITextTransform)null!));
        Assert.Throws<ArgumentNullException>(() => registry.Register((ActionDescriptor)null!));
    }

    [Fact]
    public void All_IsInRegistrationOrderAndIsASnapshot()
    {
        ActionRegistry registry = new();
        registry.Register(new FakeTransform(Transform("textgrab.text.zeta")));
        registry.Register(Command("textgrab.selection.alpha"));

        IReadOnlyCollection<ActionDescriptor> before = registry.All;
        registry.Register(new FakeTransform(Transform("textgrab.text.middle")));

        Assert.Equal(["textgrab.text.zeta", "textgrab.selection.alpha"], before.Select(d => d.Id));
        Assert.Equal(
            ["textgrab.text.zeta", "textgrab.selection.alpha", "textgrab.text.middle"],
            registry.All.Select(d => d.Id));
    }

    [Fact]
    public void Register_FromManyThreads_KeepsEveryAction()
    {
        ActionRegistry registry = new();

        Parallel.For(0, 200, i => registry.Register(new FakeTransform(Transform($"textgrab.text.action{i}"))));

        Assert.Equal(200, registry.All.Count);
        for (int i = 0; i < 200; i++)
            Assert.NotNull(registry.GetTransform($"textgrab.text.action{i}"));
    }
}
