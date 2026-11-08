using Mermaider;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using Text_Grab.Utilities;

namespace Text_Grab.Controls;

internal sealed class MermaidDiagram : Grid
{
    private readonly Image image;
    private readonly TextBlock fallback;
    private CancellationTokenSource? rendering;
    private bool isLightTheme = true;

    internal string Source { get; }
    internal Task RenderCompletion { get; private set; }

    internal MermaidDiagram(string source)
    {
        Source = source;
        image = new Image
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            MaxHeight = 480,
            Visibility = Visibility.Collapsed
        };
        fallback = new TextBlock { Text = source, TextWrapping = TextWrapping.Wrap };
        Children.Add(image);
        Children.Add(fallback);
        AutomationProperties.SetName(this, "Mermaid diagram");
        AutomationProperties.SetHelpText(this, source);
        RenderCompletion = RenderAsync();
        Unloaded += (_, _) => rendering?.Cancel();
        Loaded += (_, _) =>
        {
            if (rendering?.IsCancellationRequested == true)
                RenderCompletion = RenderAsync();
        };
    }

    internal void ApplyTheme(bool lightTheme)
    {
        if (isLightTheme == lightTheme)
            return;

        isLightTheme = lightTheme;
        RenderCompletion = RenderAsync();
    }

    private async Task RenderAsync()
    {
        rendering?.Cancel();
        rendering?.Dispose();
        CancellationTokenSource cancellation = new();
        rendering = cancellation;
        try
        {
            BitmapSource bitmap = await MermaidImageRenderer.RenderAsync(Source, isLightTheme, cancellation.Token);
            if (cancellation.IsCancellationRequested)
                return;

            image.Source = bitmap;
            image.Visibility = Visibility.Visible;
            fallback.Visibility = Visibility.Collapsed;
            ToolTip = "Mermaid diagram. Switch to plain text mode to edit the diagram source.";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is MermaidParseException or MermaidSvgException or IOException
            or ArgumentException or InvalidOperationException or FormatException or NotSupportedException
            or OverflowException or RegexMatchTimeoutException or XmlException or OperationCanceledException)
        {
            // Like asynchronous Markdown image loading, failures must stay visible without changing the text tree.
            if (cancellation.IsCancellationRequested)
                return;

            Debug.WriteLine($"Mermaid diagram could not be rendered: {exception.Message}");
            image.Visibility = Visibility.Collapsed;
            fallback.Text = $"Mermaid diagram could not be rendered: {exception.Message}{Environment.NewLine}{Environment.NewLine}{Source}";
            fallback.Visibility = Visibility.Visible;
            ToolTip = exception.Message;
        }
    }
}
