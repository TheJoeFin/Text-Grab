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

/// <summary>
/// An <see cref="ITextTransform"/> over a synchronous function of the text and a config record. The
/// JSON config is read into <typeparamref name="TConfig"/>, which must be the descriptor's
/// <see cref="ActionDescriptor.ConfigType"/>.
/// </summary>
/// <typeparam name="TConfig">The config record.</typeparam>
public sealed class DelegateTextTransform<TConfig> : ITextTransform where TConfig : class
{
    private readonly Func<string, TConfig, string> _apply;
    private readonly TConfig? _defaultConfig;

    /// <param name="descriptor">A text transform whose config type is <typeparamref name="TConfig"/>.</param>
    /// <param name="apply">The function. It may throw <see cref="ArgumentException"/> for config values it cannot use.</param>
    /// <param name="defaultConfig">Used when no config is given, or null when a config is required.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="descriptor"/> is not a <see cref="ActionKind.TextTransform"/>, or its config type is not <typeparamref name="TConfig"/>.
    /// </exception>
    public DelegateTextTransform(ActionDescriptor descriptor, Func<string, TConfig, string> apply, TConfig? defaultConfig = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(apply);

        if (descriptor.Kind != ActionKind.TextTransform)
            throw new ArgumentException($"'{descriptor.Id}' is a {descriptor.Kind}, not a text transform.", nameof(descriptor));

        if (descriptor.ConfigType != typeof(TConfig))
            throw new ArgumentException(
                $"'{descriptor.Id}' declares config type {descriptor.ConfigType?.Name ?? "none"}, but this transform reads {typeof(TConfig).Name}.",
                nameof(descriptor));

        Descriptor = descriptor;
        _apply = apply;
        _defaultConfig = defaultConfig;
    }

    public ActionDescriptor Descriptor { get; }

    public ValueTask<string> ApplyAsync(string input, JsonElement? config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        ct.ThrowIfCancellationRequested();

        TConfig resolved = ActionConfig.Read(config, _defaultConfig, Descriptor.Id);
        return ValueTask.FromResult(_apply(input, resolved));
    }
}
