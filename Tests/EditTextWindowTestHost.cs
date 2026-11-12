using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Text_Grab;
using Text_Grab.Models;
using DocumentTextRange = System.Windows.Documents.TextRange;

namespace Tests;

/// <summary>
/// Builds and drives real <see cref="EditTextWindow"/>s in the test process.
/// </summary>
/// <remarks>
/// Windows are constructed but never shown or closed: showing runs <c>Window_Loaded</c> (clipboard
/// monitor, settings restore) and closing writes settings and history. The editors are reached through
/// <c>FindName</c> rather than the generated fields, so the helpers keep working when those fields
/// become private.
/// </remarks>
internal static class EditTextWindowTestHost
{
    public static EditTextWindow CreateWindow(string text)
    {
        EditTextWindow window = new();
        LayOut(window);
        RawTextBox(window).Text = text;
        DoEvents();
        LayOut(window);
        return window;
    }

    /// <summary>
    /// Measures and arranges the window's content at a fixed size. Without a layout pass, a text box
    /// records no undo units and the data grid generates no rows, so neither behaves as it does on screen.
    /// </summary>
    public static void LayOut(EditTextWindow window)
    {
        FrameworkElement root = (FrameworkElement)window.Content;
        root.Measure(new Size(1000, 700));
        root.Arrange(new Rect(0, 0, 1000, 700));
        root.UpdateLayout();
    }

    public static void SwitchMode(EditTextWindow window, EtwEditorMode mode)
    {
        string menuItemName = mode switch
        {
            EtwEditorMode.Markdown => "MarkdownModeMenuItem",
            EtwEditorMode.Spreadsheet => "SpreadsheetModeMenuItem",
            EtwEditorMode.Code => "CodeModeMenuItem",
            _ => "RawTextModeMenuItem",
        };
        MenuItem menuItem = (MenuItem)window.FindName(menuItemName);
        menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, menuItem));
        DoEvents();
        LayOut(window);
    }

    /// <summary>Adds the command bindings <c>Window_Loaded</c> adds, without the rest of Loaded.</summary>
    public static void AddLoadedCommandBindings(EditTextWindow window)
        => typeof(EditTextWindow)
            .GetMethod("SetupRoutedCommands", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);

    public static void DoEvents()
        => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

    public static TextBox RawTextBox(EditTextWindow window) => (TextBox)window.FindName("PassedTextControl");

    public static string RawText(EditTextWindow window) => RawTextBox(window).Text;

    public static RichTextBox MarkdownEditor(EditTextWindow window) => (RichTextBox)window.FindName("MarkdownEditorControl");

    public static string RenderedMarkdown(EditTextWindow window)
    {
        RichTextBox editor = MarkdownEditor(window);
        return new DocumentTextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
    }

    public static DataGrid Grid(EditTextWindow window) => (DataGrid)window.FindName("SpreadsheetDataGrid");

    public static Text_Grab.Controls.CodeEditor CodeEditor(EditTextWindow window) => (Text_Grab.Controls.CodeEditor)window.FindName("CodeEditorControl");
}
