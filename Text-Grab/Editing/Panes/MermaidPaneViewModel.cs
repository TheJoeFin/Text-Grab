using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Text_Grab.Utilities;

namespace Text_Grab.Editing.Panes;

/// <summary>Renders Mermaid source to a bitmap and its SVG text. <see cref="MermaidImageRenderer.RenderWithSvgAsync"/> in the app.</summary>
internal delegate Task<(BitmapSource Bitmap, string Svg)> MermaidRenderer(string source, bool isLightTheme, CancellationToken cancellationToken);

/// <summary>
/// The Mermaid pane's state: the diagram last drawn, the error from the latest attempt, and the
/// debounced, cancellable rendering that keeps them current as the source changes.
/// </summary>
/// <remarks>
/// <para>
/// A change to the source waits <see cref="DebounceDelay"/> for the next one, and starting a render
/// cancels whichever one is still running, so the picture follows the last thing typed. When a render
/// fails, the last good diagram stays and <see cref="ErrorMessage"/> says why; the next good render
/// clears it. A hidden pane draws nothing (<see cref="SetActive"/>): it remembers what it was asked for
/// and catches up when shown.
/// </para>
/// <para>
/// The renderer and the delay are injected, so tests drive it without drawing anything or waiting.
/// Everything runs on the UI thread.
/// </para>
/// </remarks>
internal sealed partial class MermaidPaneViewModel : ObservableObject
{
    /// <summary>How long the source must stay unchanged before it is drawn.</summary>
    public static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    public const string EmptySourceText = "Type a Mermaid diagram in the editor and it is drawn here.";
    public const string NoBlockText = "Put the caret in a mermaid code block to draw it here.";
    public const string NothingDrawnText = "Nothing is drawn yet.";

    private readonly MermaidRenderer renderer;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private CancellationTokenSource? work;
    private string? wantedSource;
    private string? emptyReason = EmptySourceText;
    private string? renderedSvg;
    private bool renderedIsLight;
    private bool isActive = true;
    private int renderCalls;

    public MermaidPaneViewModel(MermaidRenderer? renderer = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.renderer = renderer ?? MermaidImageRenderer.RenderWithSvgAsync;
        this.delay = delay ?? ((time, cancellationToken) => Task.Delay(time, cancellationToken));
    }

    // Fields, not partial properties: see RecentItemsViewModel. MVVMTK0045 is about WinRT marshalling.
#pragma warning disable MVVMTK0045
    /// <summary>The diagram last drawn successfully; kept while a later render fails.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiagram), nameof(PlaceholderText))]
    private BitmapSource? diagramImage;

    /// <summary>The source <see cref="DiagramImage"/> was drawn from, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RenderedSourceSummary))]
    private string? renderedSource;

    /// <summary>Why the latest render failed, or null when it did not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(PlaceholderText))]
    private string? errorMessage;

    /// <summary>Whether a render is under way (the delay before it does not count).</summary>
    [ObservableProperty]
    private bool isRendering;
#pragma warning restore MVVMTK0045

    public bool HasDiagram => DiagramImage is not null;

    public bool HasError => ErrorMessage is not null;

    /// <summary>Whether the light theme's colors are in <see cref="DiagramImage"/>.</summary>
    public bool IsLightTheme { get; private set; } = true;

    /// <summary>The SVG text of <see cref="DiagramImage"/>, for export.</summary>
    public string? RenderedSvg => renderedSvg;

    /// <summary>The theme <see cref="DiagramImage"/> was drawn in.</summary>
    public bool RenderedIsLightTheme => renderedIsLight;

    /// <summary>How many renders have started. For tests.</summary>
    public int RenderCalls => Volatile.Read(ref renderCalls);

    /// <summary>The source the pane was last asked to show, drawn or not. Null means there is nothing to draw.</summary>
    public string? WantedSource => wantedSource;

    /// <summary>What to say where the diagram goes when there is none.</summary>
    public string PlaceholderText
        => DiagramImage is not null ? string.Empty : ErrorMessage is not null ? NothingDrawnText : emptyReason ?? string.Empty;

    /// <summary>The start of <see cref="RenderedSource"/>, short enough to give to UI Automation as the picture's help text.</summary>
    public string RenderedSourceSummary
        => RenderedSource is null ? string.Empty : RenderedSource.Length <= 400 ? RenderedSource : RenderedSource[..400];

    /// <summary>
    /// Asks for <paramref name="source"/> to be drawn once it has been still for <see cref="DebounceDelay"/>.
    /// Null means there is nothing to draw (a Markdown caret outside every block), and so does blank text;
    /// either clears the picture at once.
    /// </summary>
    public void SetSource(string? source)
    {
        wantedSource = source;
        if (isActive)
            Start(debounce: true);
    }

    /// <summary>Draws in the other theme, at once, when there is a picture to redraw.</summary>
    public void SetTheme(bool lightTheme)
    {
        if (IsLightTheme == lightTheme)
            return;

        IsLightTheme = lightTheme;
        if (isActive && wantedSource is not null)
            Start(debounce: false);
    }

    /// <summary>
    /// A hidden pane draws nothing: its picture would not be seen. Showing it draws what it was last asked for.
    /// </summary>
    public void SetActive(bool active)
    {
        if (isActive == active)
            return;

        isActive = active;
        if (active)
            Start(debounce: false);
        else
            CancelWork();
    }

    /// <summary>Stops any render and lets go of the picture and its SVG: the pane left its window.</summary>
    public void Release()
    {
        CancelWork();
        wantedSource = null;
        renderedSvg = null;
        DiagramImage = null;
        RenderedSource = null;
        ErrorMessage = null;
        emptyReason = EmptySourceText;
        OnPropertyChanged(nameof(PlaceholderText));
    }

    private void Start(bool debounce)
    {
        CancelWork();

        string? source = wantedSource;
        if (source is null || string.IsNullOrWhiteSpace(source))
        {
            // Nothing to draw is not an error, and the old picture would be of something else.
            renderedSvg = null;
            DiagramImage = null;
            RenderedSource = null;
            ErrorMessage = null;
            emptyReason = source is null ? NoBlockText : EmptySourceText;
            OnPropertyChanged(nameof(PlaceholderText));
            return;
        }

        // Back to what is already drawn, such as after fixing a typo that had broken the diagram.
        if (DiagramImage is not null && source == RenderedSource && renderedIsLight == IsLightTheme)
        {
            ErrorMessage = null;
            return;
        }

        CancellationTokenSource cancellation = new();
        work = cancellation;
        _ = RunAsync(source, IsLightTheme, debounce, cancellation);
    }

    private async Task RunAsync(string source, bool lightTheme, bool debounce, CancellationTokenSource cancellation)
    {
        CancellationToken token = cancellation.Token;
        try
        {
            if (debounce)
                await delay(DebounceDelay, token);

            token.ThrowIfCancellationRequested();
            Interlocked.Increment(ref renderCalls);
            IsRendering = true;
            (BitmapSource bitmap, string svg) = await renderer(source, lightTheme, token);
            if (token.IsCancellationRequested)
                return;

            renderedSvg = svg;
            renderedIsLight = lightTheme;
            DiagramImage = bitmap;
            RenderedSource = source;
            ErrorMessage = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The renderer is third-party code with its own exception types and limits, and any failure
            // in it is "this source does not draw". The last good diagram stays.
            if (token.IsCancellationRequested)
                return;

            ErrorMessage = Describe(exception);
        }
        finally
        {
            // A render that was replaced or cancelled was already cleaned up by CancelWork.
            if (ReferenceEquals(work, cancellation))
            {
                work = null;
                cancellation.Dispose();
                IsRendering = false;
            }
        }
    }

    private static string Describe(Exception exception)
    {
        string message = exception.Message.Trim();
        int newline = message.IndexOfAny(['\r', '\n']);
        if (newline > 0)
            message = message[..newline];

        return message.Length > 0 ? message : "The diagram could not be drawn.";
    }

    private void CancelWork()
    {
        if (work is null)
            return;

        CancellationTokenSource previous = work;
        work = null;
        previous.Cancel();
        previous.Dispose();
        IsRendering = false;
    }
}
