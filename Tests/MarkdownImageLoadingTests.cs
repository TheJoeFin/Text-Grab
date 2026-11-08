using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Utilities;

namespace Tests;

public class MarkdownImageLoadingTests
{
    [WpfFact]
    public void MissingImageFallbackRemainsEditable()
    {
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument("![missing](missing.png)", new FontFamily("Segoe UI"), 16);
        Paragraph paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks));
        Span span = Assert.IsType<Span>(Assert.Single(paragraph.Inlines));
        Run fallback = Assert.IsType<Run>(Assert.Single(span.Inlines));
        fallback.Text = "![corrected](images/photo.png)";

        Assert.Equal(fallback.Text, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfTheory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task WebImagesLoadAsynchronouslyAndPreserveMarkdownOnSuccessOrFailure(bool success, bool html)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        TaskCompletionSource releaseResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] imageBytes = File.ReadAllBytes(Path.GetFullPath(@"Images\font_sample.png"));

        async Task ServeImageAsync()
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(timeout.Token);
            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token)))
            {
            }

            await releaseResponse.Task.WaitAsync(timeout.Token);
            string headers = success
                ? $"HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nContent-Length: {imageBytes.Length}\r\nConnection: close\r\n\r\n"
                : "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), timeout.Token);
            if (success)
                await stream.WriteAsync(imageBytes, timeout.Token);
        }

        Task server = ServeImageAsync();
        try
        {
            string imageSource = html ? $"<img src=\"http://127.0.0.1:{port}/sample.png\" alt=\"web sample\" width=\"128\" />"
                : $"![web sample](http://127.0.0.1:{port}/sample.png)";
            string markdown = $"before {imageSource} after";
            FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
            RichTextBox editor = new() { Document = document };
            int documentChanges = 0;
            editor.TextChanged += (_, _) => documentChanges++;
            Paragraph paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks));
            Span imageSpan = Assert.IsType<Span>(paragraph.Inlines.ElementAt(1));
            Border imageHost = Assert.IsType<Border>(Assert.IsType<InlineUIContainer>(Assert.Single(imageSpan.Inlines)).Child);
            Image image = Assert.IsType<Image>(imageHost.Child);
            BitmapImage bitmap = Assert.IsType<BitmapImage>(image.Source);
            Assert.True(bitmap.IsDownloading);
            Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));

            TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            bitmap.DownloadCompleted += (_, _) => completed.TrySetResult();
            bitmap.DownloadFailed += (_, _) => completed.TrySetResult();
            bitmap.DecodeFailed += (_, _) => completed.TrySetResult();
            releaseResponse.SetResult();
            await server.WaitAsync(timeout.Token);
            await completed.Task.WaitAsync(timeout.Token);

            if (success)
            {
                Assert.Same(image, imageHost.Child);
                Assert.False(bitmap.IsDownloading);
                Assert.True(bitmap.PixelWidth > 0);
                Assert.True(bitmap.PixelHeight > 0);
            }
            else
            {
                Assert.Equal(imageSource, Assert.IsType<TextBlock>(imageHost.Child).Text);
                Assert.IsType<string>(imageSpan.ToolTip);
            }

            Assert.Equal(0, documentChanges);
            Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
        }
        finally
        {
            releaseResponse.TrySetResult();
            timeout.Cancel();
            listener.Stop();
        }
    }

    [WpfFact]
    public void ImagesShrinkToFitTheEditorAndKeepTheirAspectRatio()
    {
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument("![sample](font_sample.png)", new FontFamily("Segoe UI"), 16,
            Path.GetFullPath(@"Images\notes.md"));
        Span span = Assert.IsType<Span>(Assert.Single(Assert.IsType<Paragraph>(Assert.Single(document.Blocks)).Inlines));
        Border imageHost = Assert.IsType<Border>(Assert.IsType<InlineUIContainer>(Assert.Single(span.Inlines)).Child);
        Image image = Assert.IsType<Image>(imageHost.Child);
        RichTextBox editor = new() { Document = document, Width = 320, Height = 600 };
        editor.Measure(new Size(320, 600));
        editor.Arrange(new Rect(0, 0, 320, 600));
        editor.UpdateLayout();

        Assert.Equal(224, image.MaxWidth);
        Assert.True(image.ActualWidth <= 224);
        Assert.True(image.ActualHeight <= 480);
        Assert.True(image.ActualWidth > 0);
        BitmapSource bitmap = Assert.IsAssignableFrom<BitmapSource>(image.Source);
        Assert.Equal(bitmap.Width / bitmap.Height, image.ActualWidth / image.ActualHeight, 2);
    }
}
