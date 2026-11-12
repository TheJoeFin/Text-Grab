using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Core.Actions;

/// <summary>
/// An <see cref="ITextTransform"/> over a synchronous <c>string → string</c> function, so a pure
/// function such as <c>StringMethods.TrimEachLine</c> registers in one line. It takes no config and
/// ignores any it is given.
/// </summary>
public sealed class DelegateTextTransform : ITextTransform
{
    private readonly Func<string, string> _apply;

    /// <exception cref="ArgumentException">
    /// <paramref name="descriptor"/> is not a <see cref="ActionKind.TextTransform"/>, or declares a config type.
    /// </exception>
    public DelegateTextTransform(ActionDescriptor descriptor, Func<string, string> apply)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(apply);

        if (descriptor.Kind != ActionKind.TextTransform)
            throw new ArgumentException($"'{descriptor.Id}' is a {descriptor.Kind}, not a text transform.", nameof(descriptor));

        if (descriptor.ConfigType is not null)
            throw new ArgumentException(
                $"'{descriptor.Id}' declares config type {descriptor.ConfigType.Name}, which this transform would ignore.",
                nameof(descriptor));

        Descriptor = descriptor;
        _apply = apply;
    }

    public ActionDescriptor Descriptor { get; }

    public ValueTask<string> ApplyAsync(string input, JsonElement? config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        ct.ThrowIfCancellationRequested();

        return ValueTask.FromResult(_apply(input));
    }
}
