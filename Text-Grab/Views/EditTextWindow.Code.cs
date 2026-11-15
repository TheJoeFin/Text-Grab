using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Text_Grab.Core.Editing;
using Text_Grab.Editing;
using Text_Grab.Models;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Text_Grab;

/// <summary>
/// Code mode: the text in an AvalonEdit editor with syntax highlighting and line numbers.
/// </summary>
/// <remarks>
/// <para>
/// The raw text box stays the document, as in every mode. The code editor holds the same characters, so
/// the two are kept in step exactly: an edit typed in the code editor is applied to the text box as the
/// same replacement (<see cref="EditorSession.ApplyEditFromCodeEditor"/>), and a change made to the text
/// box by anything else (transforms, Find and Replace, grabs, transcription) is pushed into the code
/// editor as the smallest replacement (<see cref="CodeSurface.SetText"/>).
/// </para>
/// <para>
/// The selection is mirrored both ways too, so every command that reads <c>Session.Selection</c> works on
/// what the person selected in the code editor, and every command that sets it shows its result there.
/// </para>
/// </remarks>
public partial class EditTextWindow
{
    /// <summary>The code editor's highlighting language (<see cref="CodeLanguage.Name"/>); null for plain text.</summary>
    private string? codeLanguageName;

    /// <summary>
    /// True once a language has been chosen for this window, by the person, a file's extension, history or
    /// detection. Until then, entering code mode detects one.
    /// </summary>
    private bool isCodeLanguageSet;

    private bool isMirroringCodeSelection;
    private CodeSurface? codeSurface;

    private CodeSurface CodeSurfaceImpl => codeSurface ??= new CodeSurface(CodeEditorControl);

    /// <summary>The code editor's highlighting language; null for plain text.</summary>
    internal string? CodeLanguageName => codeLanguageName;

    private void InitializeCodeEditor()
    {
        CodeEditorControl.Document.Changed += CodeEditorDocument_Changed;
        CodeEditorControl.TextArea.SelectionChanged += CodeEditor_SelectionChanged;
        CodeEditorControl.TextArea.Caret.PositionChanged += CodeEditor_SelectionChanged;
        CodeEditorControl.PreviewMouseWheel += HandlePreviewMouseWheel;
        PopulateCodeLanguageMenu(CodeLanguageMenuItem);
    }

    private const string ToggleCaseGesture = "Shift + F3";

    /// <summary>
    /// The Toggle Case shortcut as the menus show it: none while the code editor's find bar is open,
    /// because Shift+F3 then goes to the previous match, as the find bar's own button says.
    /// </summary>
    /// <remarks>
    /// The find bar keeps the key while it is open: Shift+F3 for the previous match is what every code
    /// editor does, and taking it for Toggle Case would change the text of someone stepping back through
    /// matches. With the find bar closed, its Find Previous binding passes the key on to Toggle Case.
    /// </remarks>
    internal string ToggleCaseGestureText
        => editorMode == EtwEditorMode.Code && !CodeEditorControl.Search.IsClosed ? string.Empty : ToggleCaseGesture;

    private void CodeContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        foreach (object item in menu.Items)
        {
            if (item is MenuItem menuItem && menuItem.Command == ToggleCaseCmd)
                menuItem.InputGestureText = ToggleCaseGestureText;
        }
    }

    private void ShowWhitespaceMenuItem_Click(object sender, RoutedEventArgs e)
        => CodeEditorControl.ShowWhitespace = ShowWhitespaceMenuItem.IsChecked;

    internal void EnterCodeMode() => SetEditorMode(EtwEditorMode.Code);

    private void EnterCodeMode_Click(object sender, RoutedEventArgs e) => EnterCodeMode();

    /// <summary>
    /// <see cref="SetEditorMode"/>'s half of entering code mode, before the editor is shown: fills the
    /// editor from the text and picks a language if none has been chosen.
    /// </summary>
    /// <param name="deferTextLoad">
    /// Set by <see cref="OpenPath"/>, which loads the file's text and sets its language itself.
    /// </param>
    private void PrepareCodeEditor(bool deferTextLoad)
    {
        CodeEditorControl.WordWrap = WrapTextMenuItem.IsChecked;

        if (deferTextLoad)
            return;

        // The text first, so the folding scan that setting a language starts reads this text.
        LoadCodeEditorFromText();

        if (!isCodeLanguageSet)
            SetCodeLanguage(DetectCodeLanguage());

        // Fold the text now rather than after the typing delay, so the markers are there on entry.
        // Skipped if setting the language just did it.
        CodeEditorControl.UpdateFoldings();
    }

    /// <summary>
    /// Fills the code editor from the text as a fresh document (<see cref="CodeSurface.LoadText"/>) and
    /// shows the text box's selection in it. Used on entering code mode and by <see cref="OpenPath"/>.
    /// </summary>
    /// <remarks>
    /// The code editor is not kept up to date outside code mode, so it may hold anything from an earlier
    /// visit, or nothing. Its undo history from that visit does not apply to the text it now holds.
    /// </remarks>
    private void LoadCodeEditorFromText()
    {
        CodeSurfaceImpl.LoadText(Session.Text);
        MirrorTextSelectionToCodeEditor();
    }

    /// <summary>
    /// Sets the code editor's highlighting. <paramref name="languageName"/> is a
    /// <see cref="CodeLanguage.Name"/>, or null for plain text.
    /// </summary>
    internal void SetCodeLanguage(string? languageName)
    {
        IHighlightingDefinition? definition = string.IsNullOrWhiteSpace(languageName)
            ? null
            : HighlightingManager.Instance.GetDefinition(languageName);

        codeLanguageName = definition?.Name;
        isCodeLanguageSet = true;
        CodeEditorControl.SyntaxHighlighting = definition;
        CodeLanguageButtonText.Text = CodeLanguages.GetDisplayName(codeLanguageName);
        UpdateCodeLanguageMenuChecks(CodeLanguageMenuItem);

        if (editorMode == EtwEditorMode.Code)
            UpdateLineAndColumnText();
    }

    /// <summary>
    /// Takes up the language <paramref name="history"/> was saved with, "" being plain text. An item
    /// saved with none leaves the language as it is.
    /// </summary>
    private void RestoreCodeLanguage(HistoryInfo history)
    {
        if (history.CodeLanguage is not null)
            SetCodeLanguage(history.CodeLanguage);
    }

    /// <summary>The language for the opened file's extension, or else the one the text looks like.</summary>
    private string? DetectCodeLanguage()
    {
        if (CodeLanguages.FindByExtension(Path.GetExtension(OpenedFilePath ?? string.Empty)) is CodeLanguage fromFile)
            return fromFile.Name;

        return CodeLanguageDetector.Detect(Session.Text);
    }

    private void CodeEditorDocument_Changed(object? sender, DocumentChangeEventArgs e)
    {
        if (CodeSurfaceImpl.IsApplyingText)
            return;

        // The text area has already moved its caret and selection for this change: it has listened to the
        // document since the editor was made, so it hears of the change first. (It raises PositionChanged
        // only once the update is done.) So the selection read here is the one to show in the text box.
        SessionImpl.ApplyEditFromCodeEditor(
            new TextEdit(new TextSpan(e.Offset, e.RemovalLength), e.InsertedText.Text),
            CodeSurfaceImpl.Selection);
    }

    private void CodeEditor_SelectionChanged(object? sender, EventArgs e)
    {
        if (editorMode != EtwEditorMode.Code || CodeSurfaceImpl.IsApplyingText)
            return;

        MirrorCodeSelectionToText();
        UpdateLineAndColumnText();
    }

    /// <summary>Called by the text box's SelectionChanged while code mode is showing.</summary>
    private void OnTextSelectionChangedInCodeMode()
    {
        if (!SessionImpl.IsSyncingTextFromCode)
            MirrorTextSelectionToCodeEditor();

        UpdateLineAndColumnText();
    }

    /// <summary>Called by the text box's TextChanged while code mode is showing.</summary>
    private void OnTextChangedInCodeMode()
    {
        SessionImpl.SyncEditorFromText(EtwEditorMode.Code);
        MirrorTextSelectionToCodeEditor();
    }

    private void MirrorCodeSelectionToText()
    {
        if (isMirroringCodeSelection)
            return;

        isMirroringCodeSelection = true;
        try
        {
            Session.Selection = CodeSurfaceImpl.Selection.ClampTo(Session.Text.Length);
        }
        finally
        {
            isMirroringCodeSelection = false;
        }
    }

    private void MirrorTextSelectionToCodeEditor()
    {
        if (isMirroringCodeSelection)
            return;

        isMirroringCodeSelection = true;
        try
        {
            CodeSurfaceImpl.Selection = Session.Selection;
        }
        finally
        {
            isMirroringCodeSelection = false;
        }
    }

    private void UpdateCodeLineAndColumnText()
    {
        TextDocument document = CodeEditorControl.Document;
        TextSpan selection = CodeSurfaceImpl.Selection;
        string language = CodeLanguages.GetDisplayName(codeLanguageName);
        TextLocation start = document.GetLocation(selection.Start);

        if (selection.Length < 1)
        {
            BottomBarText.Text = $"{language}, Ln {start.Line}, Col {start.Column - 1}";
            HideSelectionSpecificUi();
            return;
        }

        TextLocation end = document.GetLocation(selection.End);
        int selectedLines = end.Line - start.Line;
        BottomBarText.Text = selectedLines > 0
            ? $"{language}, Ln {start.Line}:{end.Line}, Col {start.Column - 1}:{end.Column - 1}, Len {selection.Length}, Lines {selectedLines + 1}"
            : $"{language}, Ln {start.Line}, Col {start.Column - 1}:{end.Column - 1}, Len {selection.Length}";

        UpdateSelectionSpecificUI();
    }

    private void CodeLanguageButton_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu menu = new()
        {
            PlacementTarget = CodeLanguageButton,
            Placement = PlacementMode.Top,
        };
        PopulateCodeLanguageMenu(menu);
        UpdateCodeLanguageMenuChecks(menu);
        menu.IsOpen = true;
    }

    /// <summary>Fills <paramref name="menu"/> with "Detect Automatically", "Plain Text" and every language.</summary>
    private void PopulateCodeLanguageMenu(ItemsControl menu)
    {
        menu.Items.Clear();

        MenuItem detect = new() { Header = "_Detect Automatically" };
        detect.Click += (_, _) =>
        {
            SetCodeLanguage(DetectCodeLanguage());
            SetEditorMode(EtwEditorMode.Code);
        };
        menu.Items.Add(detect);
        menu.Items.Add(new Separator());

        menu.Items.Add(CreateCodeLanguageMenuItem(null));
        foreach (CodeLanguage language in CodeLanguages.All)
            menu.Items.Add(CreateCodeLanguageMenuItem(language.Name));
    }

    private MenuItem CreateCodeLanguageMenuItem(string? languageName)
    {
        MenuItem item = new()
        {
            Header = CodeLanguages.GetDisplayName(languageName),
            IsCheckable = true,
            Tag = languageName ?? string.Empty,
        };
        item.Click += (_, _) =>
        {
            SetCodeLanguage(languageName);
            SetEditorMode(EtwEditorMode.Code);
        };
        return item;
    }

    private void UpdateCodeLanguageMenuChecks(ItemsControl menu)
    {
        string current = codeLanguageName ?? string.Empty;
        foreach (object item in menu.Items)
        {
            if (item is MenuItem { Tag: string languageName } menuItem)
                menuItem.IsChecked = string.Equals(languageName, current, StringComparison.Ordinal);
        }
    }
}
