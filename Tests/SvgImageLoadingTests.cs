using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using Text_Grab.Utilities;

namespace Tests;

public class SvgImageLoadingTests
{
    private const string BadgeSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"20\"><rect width=\"60\" height=\"20\" fill=\"#ff0000\"/></svg>";

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] GetPixel(BitmapSource bitmap, int x, int y)
    {
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel;
    }

    [WpfFact]
    public async Task LocalSvgFileRendersToFrozenBitmapAtIntrinsicSize()
    {
        string path = Path.Combine(Path.GetTempPath(), $"textgrab-{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, BadgeSvg);
        try
        {
            BitmapSource bitmap = await SvgImageLoader.LoadAsync(new Uri(path));

            Assert.True(bitmap.IsFrozen);
            Assert.Equal(PixelFormats.Pbgra32, bitmap.Format);
            Assert.Equal(120, bitmap.Width, 1);
            Assert.Equal(20, bitmap.Height, 1);
            Assert.Equal(240, bitmap.PixelWidth);
            Assert.Equal(40, bitmap.PixelHeight);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [WpfFact]
    public void RenderedSvgKeepsTransparency()
    {
        BitmapSource bitmap = SvgImageLoader.RenderSvg(Utf8(BadgeSvg));

        byte[] filled = GetPixel(bitmap, 30, 20);
        byte[] empty = GetPixel(bitmap, 200, 20);

        Assert.Equal([0, 0, 255, 255], filled);
        Assert.Equal(0, empty[3]);
    }

    [WpfFact]
    public void ViewBoxOnlySvgUsesViewBoxProportions()
    {
        BitmapSource bitmap = SvgImageLoader.RenderSvg(Utf8("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 50 25\"><rect width=\"50\" height=\"25\"/></svg>"));

        Assert.True(bitmap.PixelWidth > 0);
        Assert.Equal(2d, (double)bitmap.PixelWidth / bitmap.PixelHeight, 2);
    }

    [WpfFact]
    public void SvgWithoutAnySizeStillRendersWithinLimits()
    {
        BitmapSource bitmap = SvgImageLoader.RenderSvg(Utf8("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\"/></svg>"));

        Assert.True(bitmap.PixelWidth > 0);
        Assert.True(bitmap.PixelHeight > 0);
        Assert.True(bitmap.PixelWidth <= SvgImageLoader.MaxRenderedDimension);
    }

    [WpfFact]
    public void HugeSvgIsConstrainedToRenderLimitsAndKeepsAspectRatio()
    {
        BitmapSource bitmap = SvgImageLoader.RenderSvg(Utf8("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100000\" height=\"50000\"><rect width=\"10\" height=\"10\"/></svg>"));

        Assert.True(bitmap.PixelWidth <= SvgImageLoader.MaxRenderedDimension);
        Assert.True(bitmap.PixelHeight <= SvgImageLoader.MaxRenderedDimension);
        Assert.True((long)bitmap.PixelWidth * bitmap.PixelHeight <= SvgImageLoader.MaxRenderedPixels);
        Assert.Equal(2d, (double)bitmap.PixelWidth / bitmap.PixelHeight, 1);
    }

    [WpfFact]
    public void OversizedSvgContentIsRejected()
    {
        byte[] content = new byte[SvgImageLoader.MaxDownloadBytes + 1];

        Assert.Throws<InvalidDataException>(() => SvgImageLoader.RenderSvg(content));
    }

    [WpfTheory]
    [InlineData("")]
    [InlineData("not an svg at all")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\"")]
    [InlineData("<html><body>hello</body></html>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><g></svg>")]
    public void MalformedSvgThrowsInvalidData(string content)
    {
        Assert.Throws<InvalidDataException>(() => SvgImageLoader.RenderSvg(Utf8(content)));
    }

    [WpfFact]
    public void EntityDeclarationsAreNeverExpandedOrResolved()
    {
        string external = "<!DOCTYPE svg [<!ENTITY x SYSTEM \"file:///C:/Windows/win.ini\">]>"
            + "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><text>&x;</text></svg>";
        string bomb = "<!DOCTYPE svg [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\">]>"
            + "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><text>&b;</text></svg>";

        Assert.Throws<InvalidDataException>(() => SvgImageLoader.RenderSvg(Utf8(external)));
        Assert.Throws<InvalidDataException>(() => SvgImageLoader.RenderSvg(Utf8(bomb)));
    }

    [WpfFact]
    public void DeeplyNestedSvgIsRejected()
    {
        string nested = string.Concat(Enumerable.Repeat("<g>", SvgImageLoader.MaxXmlDepth + 10))
            + string.Concat(Enumerable.Repeat("</g>", SvgImageLoader.MaxXmlDepth + 10));
        byte[] content = Utf8($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\">{nested}</svg>");

        Assert.Throws<InvalidDataException>(() => SvgImageLoader.RenderSvg(content));
    }

    [WpfFact]
    public void ExternalReferencesAreStrippedAndLocalReferencesKept()
    {
        const string svg = """
            <?xml-stylesheet href="http://127.0.0.1:1/theme.css"?>
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="40" height="20">
              <style>@import url(http://127.0.0.1:1/font.css); rect { fill: red; }</style>
              <script>alert(1)</script>
              <defs><linearGradient id="g"><stop offset="0" stop-color="red"/></linearGradient><rect id="r" width="5" height="5"/></defs>
              <image id="remote" xlink:href="http://127.0.0.1:1/photo.png" width="10" height="10"/>
              <image id="inline" href="data:image/png;base64,iVBORw0KGgo=" width="10" height="10"/>
              <use id="remoteUse" xlink:href="http://127.0.0.1:1/other.svg#a"/>
              <use id="localUse" xlink:href="#r"/>
              <rect id="remoteFill" width="10" height="10" fill="url(http://127.0.0.1:1/paint.svg#p)"/>
              <rect id="localFill" width="10" height="10" fill="url(#g)"/>
            </svg>
            """;

        XmlDocument document = SvgImageLoader.LoadHardenedXml(Utf8(svg));

        string xml = document.OuterXml;
        Assert.DoesNotContain("127.0.0.1", xml);
        Assert.DoesNotContain("script", xml);
        Assert.DoesNotContain("xml-stylesheet", xml);
        Assert.Contains("id=\"inline\"", xml);
        Assert.Contains("id=\"localUse\"", xml);
        Assert.Contains("url(#g)", xml);
        Assert.DoesNotContain("id=\"remote\"", xml);
        Assert.DoesNotContain("id=\"remoteUse\"", xml);
    }

    [Theory]
    [InlineData("image/svg+xml", true)]
    [InlineData("image/svg+xml; charset=utf-8", true)]
    [InlineData("IMAGE/SVG+XML", true)]
    [InlineData("image/png", false)]
    [InlineData("text/plain", false)]
    [InlineData(null, false)]
    public void ContentTypeIdentifiesSvg(string? contentType, bool expected)
    {
        Assert.Equal(expected, SvgImageLoader.IsSvgContentType(contentType));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>", true)]
    [InlineData("  \r\n<?xml version=\"1.0\"?>\n<!-- badge --><svg/>", true)]
    [InlineData("\uFEFF<svg/>", true)]
    [InlineData("<html><body>nope</body></html>", false)]
    [InlineData("plain text with <svg inside", false)]
    [InlineData("", false)]
    public void ContentInspectionRecognizesSvgMarkup(string content, bool expected)
    {
        Assert.Equal(expected, SvgImageLoader.LooksLikeSvg(Utf8(content)));
    }

    [Fact]
    public void BinaryContentIsNotSvg()
    {
        byte[] png = File.ReadAllBytes(Path.GetFullPath(@"Images\font_sample.png"));

        Assert.False(SvgImageLoader.LooksLikeSvg(png));
    }

    [Theory]
    [InlineData("https://img.shields.io/badge/license-MIT-blue", true)]
    [InlineData("https://img.shields.io/github/stars/TheJoeFin/Text-Grab?style=flat", true)]
    [InlineData("https://example.com/logo.svg?raw=true", true)]
    [InlineData("https://example.com/LOGO.SVG", true)]
    [InlineData("file:///C:/images/logo.svg", true)]
    [InlineData("https://example.com/photo.png", false)]
    [InlineData("https://example.com/photo.JPG?x=1", false)]
    [InlineData("file:///C:/images/photo.png", false)]
    [InlineData("file:///C:/images/photo", false)]
    public void OnlySvgAndUnknownWebFormatsUseTheLoader(string uri, bool expected)
    {
        Assert.Equal(expected, SvgImageLoader.ShouldLoadWithLoader(new Uri(uri)));
    }

    private sealed record ServedResponse(int Status, string? ContentType, byte[] Body, long? DeclaredLength = null);

    private sealed class TestServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
        private readonly Func<string, ServedResponse> _handler;

        public TestServer(Func<string, ServedResponse> handler)
        {
            _handler = handler;
            _listener.Start();
            _ = Task.Run(AcceptLoopAsync);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _ = Task.Run(() => HandleAsync(client));
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    using NetworkStream stream = client.GetStream();
                    using StreamReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
                    string requestLine = await reader.ReadLineAsync(_cts.Token) ?? string.Empty;
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync(_cts.Token)))
                    {
                    }

                    string path = requestLine.Split(' ').ElementAtOrDefault(1) ?? "/";
                    ServedResponse response = _handler(path);
                    StringBuilder headers = new();
                    headers.Append($"HTTP/1.1 {response.Status} Status\r\n");
                    if (response.ContentType is not null)
                        headers.Append($"Content-Type: {response.ContentType}\r\n");
                    headers.Append($"Content-Length: {response.DeclaredLength ?? response.Body.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(headers.ToString()), _cts.Token);
                    await stream.WriteAsync(response.Body, _cts.Token);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException or ObjectDisposedException)
                {
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _cts.Dispose();
        }
    }

    private static (Image Image, Border Host, FlowDocument Document, string Markdown) CreateWebImageDocument(string url, string? altAndSize = null)
    {
        string markdown = altAndSize ?? $"before ![badge]({url}) after";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        Paragraph paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks));
        Span imageSpan = Assert.IsType<Span>(paragraph.Inlines.ElementAt(1));
        Border host = Assert.IsType<Border>(Assert.IsType<InlineUIContainer>(Assert.Single(imageSpan.Inlines)).Child);
        Image image = Assert.IsType<Image>(host.Child);
        return (image, host, document, markdown);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        while (!condition())
            await Task.Delay(25, timeout.Token);
    }

    [WpfTheory]
    [InlineData("image/svg+xml;charset=utf-8")]
    [InlineData("text/plain")]
    [InlineData(null)]
    public async Task ExtensionlessWebBadgeRendersAsynchronouslyAndKeepsMarkdown(string? contentType)
    {
        using TestServer server = new(_ => new ServedResponse(200, contentType, Utf8(BadgeSvg)));
        (Image image, Border host, FlowDocument document, string markdown) = CreateWebImageDocument($"http://127.0.0.1:{server.Port}/badge/license-MIT-blue");
        RichTextBox editor = new() { Document = document };
        int documentChanges = 0;
        editor.TextChanged += (_, _) => documentChanges++;

        Assert.Null(image.Source);
        await WaitUntilAsync(() => image.Source is not null || host.Child is TextBlock);

        Assert.Same(image, host.Child);
        BitmapSource bitmap = Assert.IsAssignableFrom<BitmapSource>(image.Source);
        Assert.True(bitmap.IsFrozen);
        Assert.Equal(120, bitmap.Width, 1);
        Assert.Equal(0, documentChanges);
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfFact]
    public async Task ExtensionlessWebRasterImageStillLoads()
    {
        byte[] png = File.ReadAllBytes(Path.GetFullPath(@"Images\font_sample.png"));
        using TestServer server = new(_ => new ServedResponse(200, "image/png", png));
        (Image image, Border host, _, _) = CreateWebImageDocument($"http://127.0.0.1:{server.Port}/render?id=7");

        await WaitUntilAsync(() => image.Source is not null || host.Child is TextBlock);

        BitmapSource bitmap = Assert.IsAssignableFrom<BitmapSource>(image.Source);
        Assert.True(bitmap.IsFrozen);
        Assert.True(bitmap.PixelWidth > 0);
    }

    [WpfFact]
    public async Task LinkedWebBadgeKeepsLinkAndSizeAttributes()
    {
        using TestServer server = new(_ => new ServedResponse(200, "image/svg+xml", Utf8(BadgeSvg)));
        string badge = $"http://127.0.0.1:{server.Port}/badge/a-b-c";
        string markdown = $"[![badge]({badge})](https://example.com/project)";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        Image image = FindImage(document);
        RichTextBox editor = new() { Document = document, Width = 320, Height = 200 };
        editor.Measure(new Size(320, 200));
        editor.Arrange(new Rect(0, 0, 320, 200));

        await WaitUntilAsync(() => image.Source is not null);
        editor.UpdateLayout();

        Assert.Equal(120, image.ActualWidth, 1);
        Assert.Equal(20, image.ActualHeight, 1);
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    private static Image FindImage(FlowDocument document)
    {
        InlineUIContainer container = document.Blocks.OfType<Paragraph>()
            .SelectMany(paragraph => Flatten(paragraph.Inlines))
            .OfType<InlineUIContainer>()
            .First();
        return Assert.IsType<Image>(Assert.IsType<Border>(container.Child).Child);

        static IEnumerable<Inline> Flatten(InlineCollection inlines)
        {
            foreach (Inline inline in inlines)
            {
                yield return inline;
                if (inline is Span span)
                {
                    foreach (Inline child in Flatten(span.Inlines))
                        yield return child;
                }
            }
        }
    }

    [WpfFact]
    public async Task SizedHtmlImageUsesRequestedWidthForSvg()
    {
        using TestServer server = new(_ => new ServedResponse(200, "image/svg+xml", Utf8(BadgeSvg)));
        string markdown = $"<img src=\"http://127.0.0.1:{server.Port}/badge/x\" alt=\"badge\" width=\"60\" />";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        Image image = FindImage(document);
        RichTextBox editor = new() { Document = document, Width = 320, Height = 200 };
        editor.Measure(new Size(320, 200));
        editor.Arrange(new Rect(0, 0, 320, 200));

        await WaitUntilAsync(() => image.Source is not null);
        editor.UpdateLayout();

        Assert.Equal(60, image.ActualWidth, 1);
        Assert.Equal(10, image.ActualHeight, 1);
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfTheory]
    [InlineData(404, "image/svg+xml", "<svg/>")]
    [InlineData(200, "image/svg+xml", "this is not xml")]
    [InlineData(200, "text/html", "<html><body>Not an image</body></html>")]
    public async Task FailedWebImagesShowTheMarkdownAndATooltip(int status, string contentType, string body)
    {
        using TestServer server = new(_ => new ServedResponse(status, contentType, Utf8(body)));
        string url = $"http://127.0.0.1:{server.Port}/badge/broken";
        (Image _, Border host, FlowDocument document, string markdown) = CreateWebImageDocument(url);
        Span imageSpan = Assert.IsType<Span>(Assert.IsType<Paragraph>(Assert.Single(document.Blocks)).Inlines.ElementAt(1));
        RichTextBox editor = new() { Document = document };
        int documentChanges = 0;
        editor.TextChanged += (_, _) => documentChanges++;

        await WaitUntilAsync(() => host.Child is TextBlock);

        Assert.Equal($"![badge]({url})", Assert.IsType<TextBlock>(host.Child).Text);
        Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(imageSpan.ToolTip)));
        Assert.Equal(0, documentChanges);
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfFact]
    public async Task OversizedWebResponseIsRejectedBeforeItIsBuffered()
    {
        using TestServer server = new(_ => new ServedResponse(200, "image/svg+xml", Utf8(BadgeSvg), SvgImageLoader.MaxDownloadBytes + 1));
        (Image _, Border host, _, _) = CreateWebImageDocument($"http://127.0.0.1:{server.Port}/badge/huge");

        await WaitUntilAsync(() => host.Child is TextBlock);

        Assert.IsType<TextBlock>(host.Child);
    }

    [WpfFact]
    public async Task LocalSvgReferencedFromMarkdownRendersAndPreservesOriginalSource()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"textgrab-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "logo.svg"), BadgeSvg);
        try
        {
            string markdown = "![logo](logo.svg)";
            FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16, Path.Combine(directory, "notes.md"));
            Span span = Assert.IsType<Span>(Assert.Single(Assert.IsType<Paragraph>(Assert.Single(document.Blocks)).Inlines));
            Border host = Assert.IsType<Border>(Assert.IsType<InlineUIContainer>(Assert.Single(span.Inlines)).Child);
            Image image = Assert.IsType<Image>(host.Child);

            await WaitUntilAsync(() => image.Source is not null || host.Child is TextBlock);

            Assert.IsAssignableFrom<BitmapSource>(image.Source);
            Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [WpfFact]
    public async Task MissingLocalSvgShowsMarkdownFallback()
    {
        string markdown = "![gone](missing-file.svg)";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16, Path.Combine(Path.GetTempPath(), "notes.md"));
        Span span = Assert.IsType<Span>(Assert.Single(Assert.IsType<Paragraph>(Assert.Single(document.Blocks)).Inlines));
        Border host = Assert.IsType<Border>(Assert.IsType<InlineUIContainer>(Assert.Single(span.Inlines)).Child);

        await WaitUntilAsync(() => host.Child is TextBlock);

        Assert.Equal(markdown, Assert.IsType<TextBlock>(host.Child).Text);
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }
}
