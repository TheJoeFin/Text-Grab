using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AvalonDock;
using Text_Grab;
using Text_Grab.Models;
using DocumentTextRange = System.Windows.Documents.TextRange;

namespace Tests;

/// <summary>
/// Builds and drives real <see cref="EditTextWindow"/>s in the test process.
/// </summary>
/// <remarks>
/// <para>
/// Windows are constructed but never shown or closed: showing runs <c>Window_Loaded</c> (clipboard
/// monitor, settings restore) and closing writes settings and history. The editors are reached through
/// <c>FindName</c> rather than the generated fields, so the helpers keep working when those fields
/// become private.
/// </para>
/// <para>
/// Windows use the hand-written editor grid unless a test asks for the pane layout
/// (<see cref="UsePaneLayout"/>), whatever the person's <c>EtwPaneLayout</c> setting or the build's
/// default says. Tests never write that setting.
/// </para>
/// </remarks>
internal static class EditTextWindowTestHost
{
    [ModuleInitializer]
    internal static void UseTheHandWrittenGridByDefault() => EditTextWindow.PaneLayoutOverride = false;

    /// <summary>Windows made until the result is disposed render their editor area from a layout (v5 E4a).</summary>
    public static IDisposable UsePaneLayout(bool on = true)
    {
        bool? previous = EditTextWindow.PaneLayoutOverride;
        EditTextWindow.PaneLayoutOverride = on;
        return new Restore(() => EditTextWindow.PaneLayoutOverride = previous);
    }

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
        BuildPaneHost(window);
        FrameworkElement root = (FrameworkElement)window.Content;
        root.Measure(new Size(1000, 700));
        root.Arrange(new Rect(0, 0, 1000, 700));
        root.UpdateLayout();
    }

    /// <summary>The window's DockingManager, when it uses the pane layout.</summary>
    public static DockingManager? PaneHost(EditTextWindow window) => window.FindName("PaneLayoutHost") as DockingManager;

    /// <summary>
    /// AvalonDock builds a DockingManager's controls when it is loaded, and rebuilds them when its
    /// layout changes only if it is loaded, which a window that is never shown never is. This raises
    /// Loaded on the manager alone whenever its controls are missing or were built for another layout.
    /// </summary>
    private static void BuildPaneHost(EditTextWindow window)
    {
        if (PaneHost(window) is DockingManager manager && manager.LayoutRootPanel?.Model != manager.Layout.RootPanel)
            manager.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, manager));
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
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
