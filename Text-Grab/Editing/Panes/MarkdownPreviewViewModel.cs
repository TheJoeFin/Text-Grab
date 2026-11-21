using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using Text_Grab.Utilities;

namespace Text_Grab.Editing.Panes;

/// <summary>A rendered Markdown document and the source it was built from.</summary>
internal sealed record MarkdownPreviewRender(FlowDocument Document, string Source);

/// <summary>
/// Builds the preview for <paramref name="source"/>. <see cref="FlowDocument"/> belongs to the UI thread, so
/// this runs there, synchronously, after the typing delay.
/// </summary>
internal delegate MarkdownPreviewRender MarkdownPreviewRenderer(string source, bool isLightTheme);

/// <summary>
/// The Markdown preview pane's state: the document last rendered, the error from the latest attempt, and the
/// debounced rendering that keeps them current as the source changes.
/// </summary>
/// <remarks>
/// <para>
/// A change to the source waits <see cref="DebounceDelay"/> for the next one; starting a render supersedes
/// any pending one, so the preview follows the last thing typed. If building the document throws, the last
/// good document stays and <see cref="ErrorMessage"/> says why. A hidden pane renders nothing
/// (<see cref="SetActive"/>): it remembers what it was asked for and catches up when shown.
/// </para>
/// <para>The renderer and the delay are injected, so tests drive it without waiting. Everything runs on the UI thread.</para>
/// </remarks>
internal sealed partial class MarkdownPreviewViewModel : ObservableObject
{
    /// <summary>How long the source must stay unchanged before it is rendered.</summary>
    public static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    public const string EmptySourceText = "Type Markdown in the editor and it is rendered here.";
    public const string MarkdownModeText = "The editor is showing the rendered Markdown itself. Switch it to Code mode to see the source and this preview side by side.";
    public const string NothingRenderedText = "Nothing is rendered yet.";

    private const int SummaryLength = 400;

    private readonly MarkdownPreviewRenderer renderer;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private CancellationTokenSource? work;
    private string? wantedSource;
    private string? emptyReason = EmptySourceText;
    private bool renderedIsLight;
    private bool isActive = true;
    private int renderCalls;

    public MarkdownPreviewViewModel(MarkdownPreviewRenderer renderer, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.renderer = renderer;
        this.delay = delay ?? ((time, cancellationToken) => Task.Delay(time, cancellationToken));
    }

    /// <summary>Raised after a new document was rendered and is the one to show.</summary>
    public event EventHandler? DocumentRendered;

    /// <summary>Raised when the document was let go of: nothing is rendered any more.</summary>
    public event EventHandler? DocumentCleared;

#pragma warning disable MVVMTK0045 // Fields, not partial properties: see RecentItemsViewModel.
    /// <summary>The document last rendered successfully; kept while a later render fails.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocument), nameof(PlaceholderText))]
    private FlowDocument? document;

    /// <summary>The source <see cref="Document"/> was rendered from, or null.</summary>
    [ObservableProperty]
    private string? renderedSource;

    /// <summary>The start of the rendered text, short enough to give to UI Automation as help text.</summary>
    [ObservableProperty]
    private string renderedTextSummary = string.Empty;

    /// <summary>Why the latest render failed, or null when it did not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;
#pragma warning restore MVVMTK0045

    public bool HasDocument => Document is not null;

    public bool HasError => ErrorMessage is not null;

    /// <summary>Whether the light theme's colors are in <see cref="Document"/>.</summary>
    public bool IsLightTheme { get; private set; } = true;

    /// <summary>The theme <see cref="Document"/> was rendered in.</summary>
    public bool RenderedIsLightTheme => renderedIsLight;

    /// <summary>How many renders have run. For tests.</summary>
    public int RenderCalls => Volatile.Read(ref renderCalls);

    /// <summary>The source the pane was last asked to show, rendered or not. Null means nothing to render.</summary>
    public string? WantedSource => wantedSource;

    /// <summary>What to say where the preview goes when there is none.</summary>
    public string PlaceholderText => Document is not null ? string.Empty : emptyReason ?? string.Empty;

    /// <summary>
    /// Asks for <paramref name="source"/> to be rendered once it has been still for <see cref="DebounceDelay"/>.
    /// Null, or blank text, clears the preview at once and shows <paramref name="notice"/> (or a default) in its place.
    /// </summary>
    public void SetSource(string? source, string? notice = null)
    {
        wantedSource = source;
        emptyReason = notice;
        if (isActive)
            Start(debounce: true);
        else
            OnPropertyChanged(nameof(PlaceholderText));
    }

    /// <summary>Renders in the other theme, at once, when there is a document to redo.</summary>
    public void SetTheme(bool lightTheme)
    {
        if (IsLightTheme == lightTheme)
            return;

        IsLightTheme = lightTheme;
        if (isActive && !string.IsNullOrWhiteSpace(wantedSource))
            Start(debounce: false);
    }

    /// <summary>A hidden pane renders nothing: it would not be seen. Showing it renders what it was last asked for.</summary>
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

    /// <summary>Stops any render and lets go of the document: the pane left its window.</summary>
    public void Release()
    {
        CancelWork();
        wantedSource = null;
        Clear(EmptySourceText);
    }

    private void Clear(string? reason)
    {
        bool had = Document is not null;
        RenderedSource = null;
        RenderedTextSummary = string.Empty;
        ErrorMessage = null;
        emptyReason = reason;
        Document = null;
        OnPropertyChanged(nameof(PlaceholderText));
        if (had)
            DocumentCleared?.Invoke(this, EventArgs.Empty);
    }

    private void Start(bool debounce)
    {
        CancelWork();

        string? source = wantedSource;
        if (string.IsNullOrWhiteSpace(source))
        {
            // Nothing to render is not an error, and the old document would be of something else.
            Clear(emptyReason ?? EmptySourceText);
            return;
        }

        // Back to what is already shown, such as after fixing what had broken the render.
        if (Document is not null && source == RenderedSource && renderedIsLight == IsLightTheme)
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
            MarkdownPreviewRender render = renderer(source, lightTheme);
            if (token.IsCancellationRequested)
                return;

            renderedIsLight = lightTheme;
            RenderedSource = render.Source;
            RenderedTextSummary = Summarize(render.Document);
            ErrorMessage = null;
            Document = render.Document;
            DocumentRendered?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The Markdown pipeline is third-party code. Any failure in it means "this does not render",
            // and the last good document stays.
            if (!token.IsCancellationRequested)
                ErrorMessage = Describe(exception);
        }
        finally
        {
            if (ReferenceEquals(work, cancellation))
            {
                work = null;
                cancellation.Dispose();
            }
        }
    }

    private static string Summarize(FlowDocument document)
    {
        string text = MarkdownFlowDocumentUtilities.GetDocumentPlainText(document);
        return text.Length <= SummaryLength ? text : text[..SummaryLength];
    }

    private static string Describe(Exception exception)
    {
        string message = exception.Message.Trim();
        int newline = message.IndexOfAny(['\r', '\n']);
        if (newline > 0)
            message = message[..newline];

        return message.Length > 0 ? message : "The Markdown could not be rendered.";
    }

    private void CancelWork()
    {
        if (work is null)
            return;

        CancellationTokenSource previous = work;
        work = null;
        previous.Cancel();
        previous.Dispose();
    }
}
