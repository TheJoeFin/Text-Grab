using System;

namespace Text_Grab.Core.Actions;

/// <summary>What kind of implementation stands behind an <see cref="ActionDescriptor"/>.</summary>
public enum ActionKind
{
    /// <summary>A pure function of text, implemented by an <see cref="ITextTransform"/>.</summary>
    TextTransform,

    /// <summary>Needs an editor's selection, caret, mode or file state. Implemented outside the registry.</summary>
    EditorCommand,

    /// <summary>Needs app services or opens windows. Implemented outside the registry.</summary>
    AppCommand,
}

/// <summary>Where an action is grouped in menus, ribbons and pickers.</summary>
public enum ActionCategory
{
    Lines,
    Case,
    Cleanup,
    Extract,
    Search,
    Table,
    Calculate,
    Ai,
    Capture,
    File,
    Window,
    Other,
}

/// <summary>
/// One action Text Grab can perform, described as data. The same descriptor backs a menu item, a
/// ribbon or bottom-bar button, a post-grab action and a workflow node.
/// </summary>
/// <param name="Id">
/// Stable identifier of the form <c>textgrab.&lt;area&gt;.&lt;verb&gt;</c>, lower camel case after
/// each dot, for example <c>textgrab.text.trimLines</c>. IDs are persisted in users' layouts,
/// workflows and button settings: once shipped, an ID is never renamed or reused.
/// </param>
/// <param name="DisplayName">The user-facing name, as the existing menus show it.</param>
/// <param name="IconKey">
/// A plain string naming the icon. The app maps it to a <c>Wpf.Ui</c> <c>SymbolRegular</c> member of
/// the same name; Core never references <c>Wpf.Ui</c>.
/// </param>
/// <param name="Category">Where the action is grouped.</param>
/// <param name="Kind">What implements the action.</param>
/// <param name="ConfigType">
/// A record describing the user-settable parameters the action reads from its JSON config, or null
/// when it takes none.
/// </param>
/// <param name="DefaultGesture">
/// The keyboard gesture the action is bound to by default, written like <c>Ctrl+R</c> or
/// <c>Ctrl+Shift+V</c>, or null for none.
/// </param>
/// <param name="RequiresCopilotPlus">True when the action needs a Copilot+ PC (Windows AI).</param>
public sealed record ActionDescriptor(
    string Id,
    string DisplayName,
    string IconKey,
    ActionCategory Category,
    ActionKind Kind,
    Type? ConfigType = null,
    string? DefaultGesture = null,
    bool RequiresCopilotPlus = false);
