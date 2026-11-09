using Svg;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;

namespace Text_Grab.Utilities;

/// <summary>
/// Loads Markdown image destinations whose format is not known from the URL alone (SVG files and
/// extensionless web images such as shields.io badges). Content is fetched with bounded size, hardened
/// against external resources, rendered off the UI thread, and returned as a frozen WPF bitmap.
/// </summary>
internal static class SvgImageLoader
{
    internal const long MaxDownloadBytes = 5 * 1024 * 1024;
    internal const int MaxRenderedDimension = 4096;
    internal const long MaxRenderedPixels = 8_000_000;
    internal const int MaxXmlDepth = 100;
    internal const int MaxXmlNodes = 200_000;
    private const int RenderOversample = 2;
    private const float FallbackSvgWidth = 300;
    private const float FallbackSvgHeight = 150;
    private const int SniffLength = 2048;

    private static readonly string[] RasterExtensions =
    [
        ".png", ".jpg", ".jpeg", ".jpe", ".jfif", ".gif", ".bmp", ".dib", ".tif", ".tiff",
        ".ico", ".wdp", ".jxr", ".heic", ".heif", ".webp", ".avif"
    ];

    private static readonly Regex CssUrlRegex = new(@"url\(\s*(?<q>['""]?)\s*(?<target>[^)'""]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CssImportRegex = new(@"@import", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HttpClient Client = CreateClient();

    static SvgImageLoader()
    {
        // These are process-wide Svg.NET switches; nothing in Text Grab needs the library to resolve anything external.
        SvgDocument.ResolveExternalImages = ExternalType.None;
        SvgDocument.ResolveExternalElements = ExternalType.None;
        SvgDocument.ResolveExternalXmlEntites = ExternalType.None;
        SvgDocument.DisableDtdProcessing = true;
    }

    private static HttpClient CreateClient()
    {
        HttpClientHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.All,
            MaxAutomaticRedirections = 5,
        };
        HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Text-Grab");
        client.DefaultRequestHeaders.Accept.ParseAdd("image/svg+xml");
        client.DefaultRequestHeaders.Accept.ParseAdd("image/*;q=0.9");
        client.DefaultRequestHeaders.Accept.ParseAdd("*/*;q=0.5");
        return client;
    }

    internal static bool IsSvgPath(Uri uri) =>
        uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when WPF's BitmapImage cannot be trusted to handle the destination: SVG files, and web URLs
    /// whose path does not carry a known raster extension (the content type decides).
    /// </summary>
    internal static bool ShouldLoadWithLoader(Uri uri)
    {
        if (IsSvgPath(uri))
            return true;

        if (uri.IsFile)
            return false;

        string extension = Path.GetExtension(uri.AbsolutePath);
        return !RasterExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    internal static bool IsSvgContentType(string? contentType) =>
        contentType is not null && contentType.AsSpan().TrimStart().StartsWith("image/svg", StringComparison.OrdinalIgnoreCase);

    internal static bool LooksLikeSvg(ReadOnlySpan<byte> content)
    {
        ReadOnlySpan<byte> head = content[..Math.Min(content.Length, SniffLength)];
        string text = Encoding.UTF8.GetString(head).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (text.Length == 0 || text[0] != '<')
            return false;

        return text.Contains("<svg", StringComparison.Ordinal);
    }

    public static async Task<BitmapSource> LoadAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        byte[] content;
        string? contentType = null;
        if (uri.IsFile)
        {
            content = await ReadFileAsync(uri.LocalPath, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            (content, contentType) = await DownloadAsync(uri, cancellationToken).ConfigureAwait(false);
        }

        bool isSvg = IsSvgPath(uri) || IsSvgContentType(contentType) || LooksLikeSvg(content);
        return await Task.Run(() => isSvg ? RenderSvg(content) : DecodeRaster(content), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        return await ReadBoundedAsync(stream, stream.Length, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(byte[] Content, string? ContentType)> DownloadAsync(Uri uri, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"The server responded with {(int)response.StatusCode} {response.ReasonPhrase}.");

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] content = await ReadBoundedAsync(stream, response.Content.Headers.ContentLength, cancellationToken).ConfigureAwait(false);
        return (content, response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, long? declaredLength, CancellationToken cancellationToken)
    {
        if (declaredLength > MaxDownloadBytes)
            throw new InvalidDataException($"The image is larger than the {MaxDownloadBytes / (1024 * 1024)} MB limit.");

        using MemoryStream buffer = new();
        byte[] chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxDownloadBytes)
                throw new InvalidDataException($"The image is larger than the {MaxDownloadBytes / (1024 * 1024)} MB limit.");

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    internal static BitmapSource DecodeRaster(byte[] content)
    {
        try
        {
            using MemoryStream stream = new(content, writable: false);
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception exception) when (exception is NotSupportedException or FileFormatException or InvalidOperationException
            or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            throw new InvalidDataException("The response is not a supported image or SVG.", exception);
        }
    }

    /// <summary>
    /// Renders SVG content to a frozen, premultiplied-alpha bitmap. The result keeps the SVG's intrinsic
    /// size in device-independent units; it is supersampled when the size limits allow.
    /// </summary>
    internal static BitmapSource RenderSvg(byte[] content) =>
        RenderSvg(content, MaxRenderedDimension, MaxRenderedPixels, RenderOversample);

    /// <summary>
    /// Renders with caller-chosen limits. Diagrams are far larger than typical images yet are shown scaled
    /// down to the window, so they ask for more pixels to stay crisp when shrunk or zoomed.
    /// </summary>
    internal static BitmapSource RenderSvg(byte[] content, int maxDimension, long maxPixels, int preferredOversample)
    {
        if (content.LongLength > MaxDownloadBytes)
            throw new InvalidDataException($"The SVG is larger than the {MaxDownloadBytes / (1024 * 1024)} MB limit.");

        try
        {
            XmlDocument xml = LoadHardenedXml(content);
            SvgDocument svg = SvgDocument.Open(xml);
            (double dipWidth, double dipHeight) = GetBoundedSize(svg, maxDimension, maxPixels);
            int oversample = 1;
            for (int candidate = preferredOversample; candidate > 1; candidate--)
            {
                if (dipWidth * candidate <= maxDimension
                    && dipHeight * candidate <= maxDimension
                    && dipWidth * dipHeight * candidate * candidate <= maxPixels)
                {
                    oversample = candidate;
                    break;
                }
            }
            int pixelWidth = Math.Max(1, (int)Math.Round(dipWidth * oversample));
            int pixelHeight = Math.Max(1, (int)Math.Round(dipHeight * oversample));

            using Bitmap rendered = svg.Draw(pixelWidth, pixelHeight);
            return ToFrozenBitmapSource(rendered, 96d * oversample);
        }
        catch (Exception exception) when (exception is XmlException or SvgException or InvalidOperationException
            or ArgumentException or FormatException or NotSupportedException or OverflowException
            or System.Runtime.InteropServices.ExternalException or OutOfMemoryException)
        {
            throw new InvalidDataException($"The SVG could not be rendered: {exception.Message}", exception);
        }
    }

    private static (double Width, double Height) GetBoundedSize(SvgDocument svg, int maxDimension, long maxPixels)
    {
        float width;
        float height;
        try
        {
            SizeF size = svg.GetDimensions();
            width = size.Width;
            height = size.Height;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NullReferenceException)
        {
            width = height = 0;
        }

        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
        {
            width = FallbackSvgWidth;
            height = FallbackSvgHeight;
        }

        double scale = Math.Min(1d, Math.Min(
            maxDimension / (double)Math.Max(width, height),
            Math.Sqrt(maxPixels / ((double)width * height))));
        return (Math.Max(1d, width * scale), Math.Max(1d, height * scale));
    }

    private static BitmapSource ToFrozenBitmapSource(Bitmap bitmap, double dpi)
    {
        Rectangle area = new(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            BitmapSource source = BitmapSource.Create(bitmap.Width, bitmap.Height, dpi, dpi, PixelFormats.Pbgra32,
                null, data.Scan0, Math.Abs(data.Stride) * bitmap.Height, Math.Abs(data.Stride));
            source.Freeze();
            return source;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    /// <summary>
    /// Parses SVG text with DTDs ignored and no resolver, rejects pathological documents, and strips
    /// anything that would make the renderer touch another file or URL.
    /// </summary>
    internal static XmlDocument LoadHardenedXml(byte[] content)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            MaxCharactersInDocument = MaxDownloadBytes,
            CloseInput = false,
        };

        using (MemoryStream scanStream = new(content, writable: false))
        using (XmlReader scanner = XmlReader.Create(scanStream, settings))
        {
            int nodes = 0;
            while (scanner.Read())
            {
                if (++nodes > MaxXmlNodes)
                    throw new InvalidDataException("The SVG contains too many nodes.");
                if (scanner.Depth > MaxXmlDepth)
                    throw new InvalidDataException("The SVG is nested too deeply.");
            }
        }

        XmlDocument document = new() { XmlResolver = null };
        using (MemoryStream stream = new(content, writable: false))
        using (XmlReader reader = XmlReader.Create(stream, settings))
        {
            document.Load(reader);
        }

        if (document.DocumentElement is not { LocalName: "svg" })
            throw new InvalidDataException("The content is not an SVG document.");

        SanitizeExternalReferences(document);
        return document;
    }

    private static void SanitizeExternalReferences(XmlDocument document)
    {
        foreach (XmlProcessingInstruction instruction in document.SelectNodes("//processing-instruction()")!.Cast<XmlProcessingInstruction>().ToList())
            instruction.ParentNode?.RemoveChild(instruction);

        foreach (XmlElement element in document.SelectNodes("//*")!.Cast<XmlElement>().ToList())
        {
            if (element.LocalName is "script" or "foreignObject")
            {
                element.ParentNode?.RemoveChild(element);
                continue;
            }

            if (element.LocalName == "style" && HasExternalCss(element.InnerText))
            {
                element.ParentNode?.RemoveChild(element);
                continue;
            }

            bool isImage = element.LocalName == "image";
            foreach (XmlAttribute attribute in element.Attributes.Cast<XmlAttribute>().ToList())
            {
                if (attribute.LocalName == "href")
                {
                    if (IsLocalReference(attribute.Value, allowDataImage: isImage))
                        continue;

                    if (isImage || element.LocalName == "use")
                    {
                        element.ParentNode?.RemoveChild(element);
                        break;
                    }

                    element.Attributes.Remove(attribute);
                }
                else if (HasExternalCss(attribute.Value))
                {
                    element.Attributes.Remove(attribute);
                }
            }
        }
    }

    private static bool IsLocalReference(string value, bool allowDataImage)
    {
        string trimmed = value.Trim();
        return trimmed.StartsWith('#')
            || (allowDataImage && trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasExternalCss(string css)
    {
        if (CssImportRegex.IsMatch(css))
            return true;

        return CssUrlRegex.Matches(css).Any(match => !IsLocalReference(match.Groups["target"].Value, allowDataImage: true));
    }
}
