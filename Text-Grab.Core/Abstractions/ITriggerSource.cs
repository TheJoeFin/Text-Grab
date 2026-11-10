using System;

namespace Text_Grab.Core.Abstractions;

/// <summary>What started a run.</summary>
public enum TriggerKind
{
    Manual,
    ClipboardChanged,
    FileCreated,
    FileChanged,
    Hotkey,
}

/// <summary>
/// What a trigger saw. <see cref="Text"/> carries clipboard text; <see cref="FilePath"/> carries
/// the absolute path of a created or changed file.
/// </summary>
public sealed record TriggerPayload(TriggerKind Kind, string? Text = null, string? FilePath = null);

/// <summary>
/// Something that can start a run: the clipboard changing, a file appearing in a folder, a hotkey.
/// Sources know nothing about what runs; they only raise <see cref="Triggered"/>.
///
/// <see cref="Triggered"/> may be raised on a background thread. Handlers that touch UI must
/// marshal. A file trigger can arrive while the writer still holds the file open, so open it with
/// retry rather than once.
/// </summary>
public interface ITriggerSource : IDisposable
{
    event EventHandler<TriggerPayload>? Triggered;

    /// <summary>Starts raising <see cref="Triggered"/>. Calling it again while started does nothing.</summary>
    void Start();

    /// <summary>Stops raising <see cref="Triggered"/>, including for anything already pending.</summary>
    void Stop();
}
