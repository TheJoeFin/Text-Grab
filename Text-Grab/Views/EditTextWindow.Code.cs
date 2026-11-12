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

    private void EnterCodeMode_Click(object sender, RoutedEventArgs e) => SetEditorMode(EtwEditorMode.Code);

    /// <summary>
    /// <see cref="SetEditorMode"/>'s half of entering code mode, before the editor is shown: picks a
    /// language if none has been chosen and fills the editor from the text.
    /// </summary>
    private void PrepareCodeEditor(bool deferTextLoad)
    {
        if (!isCodeLanguageSet)
            SetCodeLanguage(DetectCodeLanguage());

        CodeEditorControl.WordWrap = WrapTextMenuItem.IsChecked;

        if (deferTextLoad)
            return;

        SessionImpl.SyncEditorFromText(EtwEditorMode.Code);
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

        SessionImpl.ApplyEditFromCodeEditor(new TextEdit(new TextSpan(e.Offset, e.RemovalLength), e.InsertedText.Text));
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
            TextSpan selection = CodeSurfaceImpl.Selection;
            int length = Session.Text.Length;
            int start = Math.Clamp(selection.Start, 0, length);
            Session.Selection = new TextSpan(start, Math.Clamp(selection.Length, 0, length - start));
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
