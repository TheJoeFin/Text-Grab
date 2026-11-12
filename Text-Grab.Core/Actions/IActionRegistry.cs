using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Text_Grab.Core.Actions;

/// <summary>
/// The one list of every action Text Grab can perform. Core fills it from
/// <see cref="CoreActions.RegisterAll"/>, Core.Windows and the app add theirs, and menus, buttons,
/// post-grab actions and workflows read it.
/// </summary>
/// <remarks>IDs are compared ordinally: <c>textgrab.text.trimLines</c> and <c>textgrab.text.TrimLines</c> differ.</remarks>
public interface IActionRegistry
{
    /// <summary>Every registered descriptor, in registration order. A snapshot: later registrations do not change it.</summary>
    IReadOnlyCollection<ActionDescriptor> All { get; }

    /// <summary>Finds the descriptor registered under <paramref name="id"/>.</summary>
    bool TryGet(string id, [MaybeNullWhen(false)] out ActionDescriptor descriptor);

    /// <summary>
    /// The transform registered under <paramref name="id"/>, or null when the ID is unknown or was
    /// registered as a descriptor only.
    /// </summary>
    ITextTransform? GetTransform(string id);

    /// <summary>Registers a transform under its descriptor's ID.</summary>
    /// <exception cref="System.ArgumentException">
    /// The descriptor is invalid, is not a <see cref="ActionKind.TextTransform"/>, or its ID is already registered.
    /// </exception>
    void Register(ITextTransform transform);

    /// <summary>Registers a descriptor whose implementation lives elsewhere, such as an editor command.</summary>
    /// <exception cref="System.ArgumentException">The descriptor is invalid or its ID is already registered.</exception>
    void Register(ActionDescriptor descriptor);
}
