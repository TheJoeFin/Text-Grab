using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Text_Grab.Controls;
using Text_Grab.Core.Editing;
using Text_Grab.Core.Layouts;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Editing.Panes;

/// <summary>
/// The Mermaid pane (v5 E5): the live diagram for the source in the editor beside it. In the Mermaid
/// layout the editor is in Code mode and the whole document is the diagram; in Markdown mode it is the
/// fenced <c>mermaid</c> block (<see cref="MermaidSource"/>).
/// </summary>
/// <remarks>
/// <para>
/// The view holds no logic beyond bindings and the actions on its buttons; the state, the debounce and
/// the cancellation are <see cref="MermaidPaneViewModel"/>. Export, the context menu and the full-screen
/// viewer are the ones Markdown mode's diagrams have (<see cref="MermaidDiagram"/>), run on a copy of the
/// latest finished render. Nothing here is tied to <c>Loaded</c> or <c>Unloaded</c>, because AvalonDock
/// raises both again whenever it re-parents the view.
/// </para>
/// <para>
/// A pane that is not visible draws nothing and catches up when it is shown. The picture and its SVG are
/// let go of on <see cref="Detach"/>.
/// </para>
/// </remarks>
public partial class MermaidPane : UserControl, IEditorPane
{
    public const string PaneId = PaneIds.Mermaid;

    public static readonly PaneDescriptor Definition = new(
        PaneId, "Diagram", "Flowchart24", PaneDock.Right, DefaultSize: 420, MinSize: 220);

    private const string PaletteGroup = "Diagram";
    private const double ZoomStep = 0.2;
    private const double MinZoom = 0.2;

    private static int attachedCount;
    private static int createdCount;

    private readonly MermaidPaneViewModel viewModel;
    private readonly Func<bool> readIsLightTheme;

    // WPF opens a context menu only for an element that already has one when a right-click's
    // ContextMenuOpening starts routing: a menu first set by the handler would open on the next click
    // instead. So while attached the picture always holds a menu, this empty one until a right-click
    // builds the real one (Zoom_ContextMenuOpening).
    private readonly ContextMenu placeholderMenu = new();
    private IEditorSession? session;

    public MermaidPane()
        : this(new MermaidPaneViewModel(), SystemThemeUtility.IsLightTheme)
    {
    }

    internal MermaidPane(MermaidPaneViewModel viewModel, Func<bool> isLightTheme)
    {
        Interlocked.Increment(ref createdCount);
        this.viewModel = viewModel;
        readIsLightTheme = isLightTheme;
        DataContext = viewModel;
        InitializeComponent();
    }

    public PaneDescriptor Descriptor => Definition;

    public FrameworkElement View => this;

    /// <summary>How many Mermaid panes are attached to a window. For tests that check a pane is not made or kept needlessly.</summary>
    internal static int AttachedCount => Volatile.Read(ref attachedCount);

    /// <summary>How many Mermaid panes have been made, attached or not.</summary>
    internal static int CreatedCount => Volatile.Read(ref createdCount);

    /// <summary>
    /// Puts an image on the clipboard. Tests replace it, so they never write the person's clipboard.
    /// </summary>
    internal static Action<BitmapSource> ImageClipboard { get; set; } = CopyToClipboard;

    internal MermaidPaneViewModel ViewModel => viewModel;

    public void Attach(IEditorSession session, JsonElement? config)
    {
        if (this.session is not null)
            throw new InvalidOperationException("The Mermaid pane is already attached.");

        this.session = session;
        Interlocked.Increment(ref attachedCount);

        session.TextChanged += Session_Changed;
        session.ModeChanged += Session_Changed;
        IsVisibleChanged += MermaidPane_IsVisibleChanged;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Zoom.ContextMenu = placeholderMenu;

        viewModel.SetTheme(readIsLightTheme());
        Refresh();
    }

    public void Detach()
    {
        if (session is null)
            return;

        session.TextChanged -= Session_Changed;
        session.ModeChanged -= Session_Changed;
        IsVisibleChanged -= MermaidPane_IsVisibleChanged;
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;

        // The picture and its SVG can be large; a pane that left its window has no reason to keep them.
        Zoom.ContextMenu = null;
        viewModel.Release();
        session = null;
        Interlocked.Decrement(ref attachedCount);
    }

    /// <summary>Nothing: the pane's width and whether it is shown are the host's, and the diagram follows the document.</summary>
    public JsonElement? SaveConfig() => null;

    public IReadOnlyList<PaneCommand> GetPaletteCommands()
    {
        if (session is null || !viewModel.HasDiagram)
            return [];

        return
        [
            new("Open diagram full screen", PaletteGroup, OpenFullScreen),
            new("Export diagram as SVG", PaletteGroup, ExportSvg),
            new("Export diagram as PNG", PaletteGroup, ExportPng),
            new("Copy diagram image", PaletteGroup, CopyImage),
        ];
    }

    /// <summary>The system theme may have changed while the window was inactive; the diagram is drawn in its colors.</summary>
    public void OnWindowActivated()
    {
        if (session is null)
            return;

        viewModel.SetTheme(readIsLightTheme());
        Refresh();
    }

    /// <summary>Reads the document again and draws what it says, now or after the typing delay.</summary>
    internal void Refresh()
    {
        if (session is null)
            return;

        string text = session.Text;
        string? source = MermaidSource.ForDocument(text, session.Mode, session.Selection.Start);

        // The rendered Markdown editor cannot say where its caret is in the raw text, so the session's
        // selection there is stale and usually outside every block. Draw the first one rather than nothing.
        if (source is null && session.Mode == EtwEditorMode.Markdown)
            source = MermaidSource.FindFirstBlock(text)?.Source;

        viewModel.SetSource(source);
    }

    /// <summary>A hidden pane draws nothing; a shown one draws what it was last asked for. Called when the view's visibility changes.</summary>
    public void OnVisibilityChanged(bool visible) => viewModel.SetActive(visible);

    internal void OpenFullScreen() => CreateDiagram()?.OpenViewer(Window.GetWindow(this));

    internal void ExportSvg() => CreateDiagram()?.Save(MermaidSaveFormat.Svg);

    internal void ExportPng() => CreateDiagram()?.Save(MermaidSaveFormat.Png);

    internal void CopyImage()
    {
        if (viewModel.DiagramImage is BitmapSource image)
            ImageClipboard(image);
    }

    internal void ZoomIn() => Zoom.SetScale(Zoom.GetScale() + ZoomStep);

    internal void ZoomOut() => Zoom.SetScale(Math.Max(MinZoom, Zoom.GetScale() - ZoomStep));

    internal void ResetZoom() => Zoom.Reset();

    /// <summary>The right-click menu for the picture: the one Markdown mode's diagrams have, plus Copy image. Null when nothing is drawn.</summary>
    internal ContextMenu? CreateContextMenu()
    {
        if (CreateDiagram() is not MermaidDiagram diagram)
            return null;

        ContextMenu menu = diagram.CreateContextMenu(Window.GetWindow(this));
        MenuItem copy = new() { Header = "Copy image" };
        copy.Click += (_, _) => CopyImage();
        menu.Items.Insert(1, copy);
        return menu;
    }

    /// <summary>Gives the picture the menu for what is drawn now. False, leaving the empty placeholder, when nothing is.</summary>
    internal bool UpdateContextMenu()
    {
        ContextMenu? menu = CreateContextMenu();
        Zoom.ContextMenu = menu ?? placeholderMenu;
        return menu is not null;
    }

    /// <summary>
    /// A diagram carrying the latest finished render, which is never put on screen: it is what
    /// <see cref="MermaidDiagram.Save"/>, its menu and its full-screen viewer work on.
    /// </summary>
    private MermaidDiagram? CreateDiagram()
        => viewModel.DiagramImage is BitmapSource image && viewModel.RenderedSource is string source && viewModel.RenderedSvg is string svg
            ? new MermaidDiagram(source, image, svg, viewModel.RenderedIsLightTheme)
            : null;

    private static void CopyToClipboard(BitmapSource image)
    {
        try
        {
            Clipboard.SetImage(image);
        }
        catch (COMException)
        {
            // The clipboard is busy; there is nothing useful to do.
        }
    }

    private void Session_Changed(object? sender, EventArgs e) => Refresh();

    private void MermaidPane_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        => OnVisibilityChanged(e.NewValue is true);

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A new picture, or none: let go of the menu built for the old one, which holds its picture and
        // SVG, unless it is open. The placeholder takes its place so the next right-click still opens.
        if (e.PropertyName == nameof(MermaidPaneViewModel.DiagramImage)
            && (viewModel.DiagramImage is null || Zoom.ContextMenu is not { IsOpen: true }))
        {
            Zoom.ContextMenu = placeholderMenu;
        }
    }

    private void Zoom_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!UpdateContextMenu())
            e.Handled = true;
    }

    private void OpenFullScreen_Click(object sender, RoutedEventArgs e) => OpenFullScreen();

    private void ExportSvg_Click(object sender, RoutedEventArgs e) => ExportSvg();

    private void ExportPng_Click(object sender, RoutedEventArgs e) => ExportPng();

    private void CopyImage_Click(object sender, RoutedEventArgs e) => CopyImage();

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomIn();

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomOut();

    private void ResetZoom_Click(object sender, RoutedEventArgs e) => ResetZoom();
}
