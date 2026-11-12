using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Threading;

namespace Text_Grab.Core.Actions;

/// <summary>
/// The default <see cref="IActionRegistry"/>. Safe to read and register from any thread.
/// </summary>
public sealed partial class ActionRegistry : IActionRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (ActionDescriptor Descriptor, ITextTransform? Transform)> _entries = new(StringComparer.Ordinal);
    private readonly List<ActionDescriptor> _order = [];
    private ReadOnlyCollection<ActionDescriptor> _all = ReadOnlyCollection<ActionDescriptor>.Empty;

    public IReadOnlyCollection<ActionDescriptor> All
    {
        get
        {
            lock (_gate)
                return _all;
        }
    }

    public bool TryGet(string id, [MaybeNullWhen(false)] out ActionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(id);

        lock (_gate)
        {
            if (_entries.TryGetValue(id, out (ActionDescriptor Descriptor, ITextTransform? Transform) entry))
            {
                descriptor = entry.Descriptor;
                return true;
            }
        }

        descriptor = null;
        return false;
    }

    public ITextTransform? GetTransform(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        lock (_gate)
            return _entries.TryGetValue(id, out (ActionDescriptor Descriptor, ITextTransform? Transform) entry)
                ? entry.Transform
                : null;
    }

    public void Register(ITextTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        ActionDescriptor descriptor = transform.Descriptor
            ?? throw new ArgumentException("The transform has no descriptor.", nameof(transform));

        if (descriptor.Kind != ActionKind.TextTransform)
            throw new ArgumentException(
                $"'{descriptor.Id}' is registered as a transform but its kind is {descriptor.Kind}.", nameof(transform));

        Add(descriptor, transform);
    }

    public void Register(ActionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Add(descriptor, null);
    }

    /// <summary>
    /// True when <paramref name="id"/> follows the scheme <c>textgrab.&lt;area&gt;.&lt;verb&gt;</c>:
    /// exactly three dot-separated parts, each lower camel case.
    /// </summary>
    internal static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    private void Add(ActionDescriptor descriptor, ITextTransform? transform)
    {
        if (!IsValidId(descriptor.Id))
            throw new ArgumentException(
                $"'{descriptor.Id}' is not a valid action ID. IDs look like textgrab.<area>.<verb>, in lower camel case.",
                nameof(descriptor));

        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            throw new ArgumentException($"'{descriptor.Id}' has no display name.", nameof(descriptor));

        if (descriptor.IconKey is null)
            throw new ArgumentException($"'{descriptor.Id}' has a null icon key.", nameof(descriptor));

        lock (_gate)
        {
            if (!_entries.TryAdd(descriptor.Id, (descriptor, transform)))
                throw new ArgumentException($"An action with the ID '{descriptor.Id}' is already registered.", nameof(descriptor));

            _order.Add(descriptor);
            _all = new ReadOnlyCollection<ActionDescriptor>([.. _order]);
        }
    }

    // \z, not $: $ also matches before a trailing newline.
    [GeneratedRegex(@"^textgrab\.[a-z][a-zA-Z0-9]*\.[a-z][a-zA-Z0-9]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
