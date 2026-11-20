using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Text_Grab.Core.Editing;
using Text_Grab.Core.Layouts;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Editing.Panes;

/// <summary>
/// The Markdown preview pane (v5 E5): the rendered document for the Markdown in the editor beside it. In the
/// Markdown preview layout the editor is in Code mode with Markdown highlighting; the preview is read-only.
/// </summary>
/// <remarks>
/// <para>
/// The view holds no logic beyond bindings and its input handlers; the state, the debounce and the
/// last-good-document rule are <see cref="MarkdownPreviewViewModel"/>. The rendering is Markdown mode's
/// (<see cref="MarkdownFlowDocumentUtilities"/>), used read-only. While the editor itself is in Markdown mode
/// the pane shows a notice instead, so the same document is never shown twice. Nothing here is tied to
/// <c>Loaded</c> or <c>Unloaded</c>, because AvalonDock raises both again whenever it re-parents the view.
/// </para>
/// <para>
/// Scrolling the source scrolls the preview in proportion. A click in the preview, or Enter, moves the
/// caret in the source to the matching place (<see cref="MarkdownPreviewSync"/>). A pane that is not visible
/// renders nothing and catches up when shown; the document is let go of on <see cref="Detach"/>.
/// </para>
/// </remarks>
public partial class MarkdownPreviewPane : UserControl, IEditorPane
{
    public const string PaneId = PaneIds.MarkdownPreview;

    public static readonly PaneDescriptor Definition = new(
        PaneId, "Preview", "DocumentText24", PaneDock.Right, DefaultSize: 420, MinSize: 220);

    private const string PaletteGroup = "Preview";

    private static int attachedCount;
    private static int createdCount;

    private readonly MarkdownPreviewViewModel viewModel;
    private readonly Func<bool> readIsLightTheme;
    private IEditorSession? session;
    private MarkdownPreviewSync? sync;
    private int lineCount = 1;
    private bool suppressScrollSync;
    private bool followUpPending;

    public MarkdownPreviewPane()
        : this(null, SystemThemeUtility.IsLightTheme)
    {
    }

    internal MarkdownPreviewPane(MarkdownPreviewViewModel? viewModel, Func<bool> isLightTheme)
    {
        Interlocked.Increment(ref createdCount);
        this.viewModel = viewModel ?? new MarkdownPreviewViewModel(Render);
        readIsLightTheme = isLightTheme;
        DataContext = this.viewModel;
        InitializeComponent();
        Preview.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(Preview_RequestNavigate));
        this.viewModel.DocumentRendered += ViewModel_DocumentRendered;
        this.viewModel.DocumentCleared += ViewModel_DocumentCleared;
    }

    public PaneDescriptor Descriptor => Definition;

    public FrameworkElement View => this;

    /// <summary>How many Markdown preview panes are attached to a window. For tests that check a pane is not made or kept needlessly.</summary>
    internal static int AttachedCount => Volatile.Read(ref attachedCount);

    /// <summary>How many Markdown preview panes have been made, attached or not.</summary>
    internal static int CreatedCount => Volatile.Read(ref createdCount);

    internal MarkdownPreviewViewModel ViewModel => viewModel;

    /// <summary>The path of the file the document came from, so that relative images resolve. Set by the window.</summary>
    internal Func<string?>? FilePathProvider { get; set; }

    internal RichTextBox PreviewBox => Preview;

    public void Attach(IEditorSession session, JsonElement? config)
    {
        if (this.session is not null)
            throw new InvalidOperationException("The Markdown preview pane is already attached.");

        this.session = session;
        Interlocked.Increment(ref attachedCount);

        session.TextChanged += Session_Changed;
        session.ModeChanged += Session_Changed;
        session.ViewportChanged += Session_ViewportChanged;
        IsVisibleChanged += MarkdownPreviewPane_IsVisibleChanged;

        viewModel.SetTheme(readIsLightTheme());
        Refresh();
    }

    public void Detach()
    {
        if (session is null)
            return;

        session.TextChanged -= Session_Changed;
        session.ModeChanged -= Session_Changed;
        session.ViewportChanged -= Session_ViewportChanged;
        IsVisibleChanged -= MarkdownPreviewPane_IsVisibleChanged;

        // The rendered document can be large (images, diagrams); a pane that left its window has no reason to keep it.
        viewModel.Release();
        sync = null;
        session = null;
        Interlocked.Decrement(ref attachedCount);
    }

    /// <summary>Nothing: the pane's width and whether it is shown are the host's, and the preview follows the document.</summary>
    public JsonElement? SaveConfig() => null;

    public IReadOnlyList<PaneCommand> GetPaletteCommands()
    {
        if (session is null || !viewModel.HasDocument)
            return [];

        return [new("Go to the preview's caret in the source", PaletteGroup, () => GoToSource(Preview.CaretPosition))];
    }

    /// <summary>The system theme may have changed while the window was inactive; the document is drawn in its colors.</summary>
    public void OnWindowActivated()
    {
        if (session is null)
            return;

        viewModel.SetTheme(readIsLightTheme());
        Refresh();
    }

    /// <summary>Reads the document again and renders what it says, now or after the typing delay.</summary>
    internal void Refresh()
    {
        if (session is null)
            return;

        if (session.Mode == EtwEditorMode.Markdown)
        {
            viewModel.SetSource(null, MarkdownPreviewViewModel.MarkdownModeText);
            return;
        }

        string text = session.Text;
        lineCount = CountLines(text);
        viewModel.SetSource(text);
    }

    /// <summary>A hidden pane renders nothing; a shown one renders what it was last asked for. Called when the view's visibility changes.</summary>
    internal void OnVisibilityChanged(bool visible) => viewModel.SetActive(visible);

    /// <summary>
    /// Moves the caret in the source to where <paramref name="position"/> is in the preview. False when the
    /// position is in something the source map does not cover.
    /// </summary>
    internal bool GoToSource(TextPointer? position)
    {
        if (session is null || position is null || sync?.RawOffsetAt(position) is not int offset)
            return false;

        // Showing the caret scrolls the source, which must not scroll the preview back.
        suppressScrollSync = true;
        _ = Dispatcher.BeginInvoke(() => suppressScrollSync = false, DispatcherPriority.ApplicationIdle);
        session.SelectInEditor(new TextSpan(offset, 0));
        return true;
    }

    /// <summary>Scrolls the preview to the same fraction of its height that the source is scrolled to.</summary>
    internal void SyncScroll()
    {
        if (session is null || suppressScrollSync || !viewModel.HasDocument)
            return;

        double fraction = lineCount <= 1 ? 0 : Math.Clamp(session.FirstVisibleLine / (lineCount - 1), 0, 1);
        double scrollable = Math.Max(0, Preview.ExtentHeight - Preview.ViewportHeight);
        Preview.ScrollToVerticalOffset(fraction * scrollable);
    }

    private MarkdownPreviewRender Render(string source, bool isLightTheme)
    {
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(
            source, Preview.FontFamily, Preview.FontSize, FilePathProvider?.Invoke());
        MarkdownFlowDocumentUtilities.ApplyTheme(document, this, isLightTheme);
        DisableTaskListBoxes(document);
        return new MarkdownPreviewRender(document, source);
    }

    // A task-list box would toggle in the preview and never reach the source.
    private static void DisableTaskListBoxes(DependencyObject parent)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is InlineUIContainer { Child: CheckBox box })
            {
                box.IsHitTestVisible = false;
                box.Focusable = false;
            }
            else if (child is DependencyObject element)
            {
                DisableTaskListBoxes(element);
            }
        }
    }

    private static int CountLines(string text)
    {
        int lines = 1;
        foreach (char c in text)
        {
            if (c == '\n')
                lines++;
        }

        return lines;
    }

    private void ViewModel_DocumentRendered(object? sender, EventArgs e)
    {
        Preview.Document = viewModel.Document!;
        sync = new MarkdownPreviewSync(viewModel.Document!, viewModel.RenderedSource ?? string.Empty);

        // A new document starts at the top; table columns need the viewport's width, which is known
        // after layout. Both are redone then, as Markdown mode does.
        ScheduleFollowUp();
    }

    private void ViewModel_DocumentCleared(object? sender, EventArgs e)
    {
        sync = null;
        Preview.Document = new FlowDocument();
    }

    private void ScheduleFollowUp()
    {
        if (followUpPending)
            return;

        followUpPending = true;
        _ = Dispatcher.BeginInvoke(() =>
        {
            followUpPending = false;
            FitTables();
            SyncScroll();
        }, DispatcherPriority.Background);
    }

    private void FitTables()
    {
        if (!viewModel.HasDocument || Preview.ViewportWidth <= 0)
            return;

        MarkdownFlowDocumentUtilities.FitTableColumns(Preview.Document, Preview.ViewportWidth, VisualTreeHelper.GetDpi(Preview).PixelsPerDip);
    }

    private void Session_Changed(object? sender, EventArgs e) => Refresh();

    private void Session_ViewportChanged(object? sender, EventArgs e) => SyncScroll();

    private void MarkdownPreviewPane_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        => OnVisibilityChanged(e.NewValue is true);

    private void Preview_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (viewModel.HasDocument)
            ScheduleFollowUp();
    }

    private void Preview_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        _ = ExternalLinkLauncher.OpenAsync(e.Uri.OriginalString);
    }

    private void Preview_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // A drag that selected text is for copying, not for moving the caret.
        if (!Preview.Selection.IsEmpty)
            return;

        TextPointer? position = Preview.GetPositionFromPoint(e.GetPosition(Preview), snapToText: true);
        if (position is null || IsInLink(position))
            return;

        GoToSource(position);
    }

    private void Preview_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (GoToSource(Preview.CaretPosition))
            e.Handled = true;
    }

    private static bool IsInLink(TextPointer position)
    {
        for (DependencyObject? element = position.Parent as DependencyObject; element is not null; element = LogicalTreeHelper.GetParent(element))
        {
            if (element is Hyperlink)
                return true;
        }

        return false;
    }
}
