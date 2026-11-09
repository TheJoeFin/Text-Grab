using Mermaider;
using Mermaider.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using RenderOptions = Mermaider.Models.RenderOptions;

namespace Text_Grab.Utilities;

internal static class MermaidImageRenderer
{
    private static readonly SemaphoreSlim RenderSlots = new(2);
    private static readonly Regex VariableDeclaration = new(@"(?<name>--[\w-]+)\s*:\s*(?<value>[^;{}]+);?", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    // Long diagrams are shown scaled down to the window width (and zoomed in the viewer), so render them
    // with more pixels than an ordinary image: 8192 px on a side, 16 MP (~64 MB) at most. Oversampling stays
    // at 2x so the many ordinary-sized diagrams in a document keep a small memory footprint.
    private const int MaxBitmapDimension = 8192;
    private const long MaxBitmapPixels = 16_000_000;
    private const int PreferredOversample = 2;

    internal static async Task<BitmapSource> RenderAsync(string source, bool isLightTheme, CancellationToken cancellationToken = default)
        => (await RenderWithSvgAsync(source, isLightTheme, cancellationToken).ConfigureAwait(false)).Bitmap;

    /// <summary>
    /// Renders the diagram and returns both the bitmap and the portable SVG text (CSS variables resolved)
    /// so the diagram can be exported without rendering it a second time.
    /// </summary>
    internal static async Task<(BitmapSource Bitmap, string Svg)> RenderWithSvgAsync(string source, bool isLightTheme, CancellationToken cancellationToken = default)
    {
        await RenderSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using MemoryStream output = new();
            await MermaidRenderer.RenderSvgAsync(source, output, new RenderOptions
            {
                Bg = isLightTheme ? "#f5f5f5" : "#202024",
                Fg = isLightTheme ? "#202024" : "#eeeeee",
                Font = "Segoe UI",
                MonoFont = "Consolas",
                FontSize = "16px",
                Elevation = 0,
                SanitizeMode = SanitizeMode.Block,
                Limits = new ResourceLimits
                {
                    MaxInputLength = 50_000,
                    MaxLines = 1_000,
                    MaxElements = 500,
                    MaxNodesAfterLayout = 2_000,
                    MaxRecursionDepth = 32,
                    MaxOutputLength = 2 * 1024 * 1024,
                    RenderDeadline = TimeSpan.FromSeconds(3)
                }
            }, cancellationToken).ConfigureAwait(false);

            return await Task.Run(() =>
            {
                byte[] prepared = PrepareSvg(output.ToArray());
                return (SvgImageLoader.RenderSvg(prepared, MaxBitmapDimension, MaxBitmapPixels, PreferredOversample), Encoding.UTF8.GetString(prepared));
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            RenderSlots.Release();
        }
    }

    // Mermaider emits modern CSS variables and color-mix(); Svg.NET needs concrete SVG paint values.
    internal static byte[] PrepareSvg(byte[] content)
    {
        XmlDocument document = SvgImageLoader.LoadHardenedXml(content);
        Dictionary<string, string> variables = new(StringComparer.Ordinal);
        XmlElement root = document.DocumentElement ?? throw new InvalidDataException("The Mermaid renderer returned no SVG.");
        XmlElement[] styles = document.GetElementsByTagName("style").Cast<XmlElement>().ToArray();
        foreach (XmlElement style in styles)
            CollectVariables(style.InnerText);
        CollectVariables(root.GetAttribute("style"));

        foreach (XmlElement element in document.GetElementsByTagName("*"))
        {
            foreach (XmlAttribute attribute in element.Attributes)
            {
                if (attribute.LocalName == "style")
                    attribute.Value = Resolve(VariableDeclaration.Replace(attribute.Value, string.Empty), variables);
                else if (attribute.LocalName is "fill" or "stroke" or "stop-color" or "color" or "font-size")
                    attribute.Value = Resolve(attribute.Value, variables);
            }
        }

        foreach (XmlElement style in styles)
            style.InnerText = Resolve(VariableDeclaration.Replace(style.InnerText, string.Empty), variables);

        // Svg.NET ignores paint-order, so draw text halos separately before the foreground glyphs.
        foreach (XmlElement text in document.GetElementsByTagName("text").Cast<XmlElement>().ToArray())
        {
            string[] paintOrder = text.GetAttribute("paint-order").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (paintOrder.Length == 0 || paintOrder[0] != "stroke")
                continue;

            XmlElement halo = (XmlElement)text.CloneNode(deep: true);
            halo.RemoveAttribute("id");
            halo.RemoveAttribute("paint-order");
            halo.SetAttribute("fill", "none");
            foreach (XmlElement span in halo.GetElementsByTagName("tspan"))
            {
                span.RemoveAttribute("id");
                span.SetAttribute("fill", "none");
            }

            text.RemoveAttribute("paint-order");
            text.SetAttribute("stroke", "none");
            foreach (XmlElement span in text.GetElementsByTagName("tspan"))
                span.SetAttribute("stroke", "none");
            text.ParentNode!.InsertBefore(halo, text);
        }

        return Encoding.UTF8.GetBytes(document.OuterXml);

        void CollectVariables(string css)
        {
            foreach (Match match in VariableDeclaration.Matches(css))
                variables[match.Groups["name"].Value] = match.Groups["value"].Value.Trim();
        }
    }

    private static string Resolve(string value, IReadOnlyDictionary<string, string> variables, int depth = 0)
    {
        if (depth > 32)
            throw new InvalidDataException("The Mermaid SVG contains excessively nested CSS values.");

        StringBuilder resolved = new();
        while (true)
        {
            int start = value.IndexOf("var(", StringComparison.Ordinal);
            int mixStart = value.IndexOf("color-mix(", StringComparison.Ordinal);
            if (start < 0 || mixStart >= 0 && mixStart < start)
                start = mixStart;
            if (start < 0)
                return resolved.Append(value).ToString();

            int open = value.IndexOf('(', start);
            int end = open + 1;
            int nesting = 1;
            for (; end < value.Length && nesting > 0; end++)
            {
                if (value[end] == '(') nesting++;
                if (value[end] == ')') nesting--;
            }
            if (nesting != 0)
                throw new InvalidDataException("The Mermaid SVG contains an unbalanced CSS value.");

            string[] arguments = SplitArguments(value[(open + 1)..(end - 1)]);
            string replacement;
            if (value.AsSpan(start).StartsWith("var(", StringComparison.Ordinal))
            {
                string variable = variables.TryGetValue(arguments[0], out string? defined) ? defined
                    : arguments.Length == 2 ? arguments[1]
                    : throw new InvalidDataException($"The Mermaid SVG references an undefined CSS variable: {arguments[0]}.");
                replacement = Resolve(variable, variables, depth + 1);
            }
            else
            {
                if (arguments.Length != 3 || arguments[0] != "in srgb")
                    throw new InvalidDataException("The Mermaid SVG contains an unsupported color mix.");
                replacement = MixColors(Resolve(arguments[1], variables, depth + 1), Resolve(arguments[2], variables, depth + 1));
            }

            resolved.Append(value.AsSpan(0, start)).Append(replacement);
            value = value[end..];
        }
    }

    private static string[] SplitArguments(string value)
    {
        List<string> arguments = [];
        int nesting = 0;
        int start = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '(') nesting++;
            if (value[i] == ')') nesting--;
            if (value[i] == ',' && nesting == 0)
            {
                arguments.Add(value[start..i].Trim());
                start = i + 1;
            }
        }
        arguments.Add(value[start..].Trim());
        return arguments.ToArray();
    }

    private static string MixColors(string first, string second)
    {
        (Color firstColor, double? firstWeight) = ParseColor(first);
        (Color secondColor, double? secondWeight) = ParseColor(second);
        double weight = firstWeight ?? (secondWeight.HasValue ? 1 - secondWeight.Value : 0.5);
        double otherWeight = secondWeight ?? 1 - weight;
        double total = weight + otherWeight;
        if (!double.IsFinite(total) || total <= 0 || weight < 0 || otherWeight < 0 || weight > 1 || otherWeight > 1)
            throw new InvalidDataException("The Mermaid SVG contains an invalid color mix.");
        weight /= total;
        otherWeight /= total;
        byte Mix(byte a, byte b) => (byte)Math.Round(a * weight + b * otherWeight);
        return $"#{Mix(firstColor.R, secondColor.R):x2}{Mix(firstColor.G, secondColor.G):x2}{Mix(firstColor.B, secondColor.B):x2}";
    }

    private static (Color Color, double? Weight) ParseColor(string value)
    {
        int space = value.LastIndexOf(' ');
        double? weight = null;
        if (space >= 0 && value.EndsWith('%'))
        {
            weight = double.Parse(value.AsSpan(space + 1, value.Length - space - 2), CultureInfo.InvariantCulture) / 100;
            value = value[..space];
        }
        return ((Color)ColorConverter.ConvertFromString(value), weight);
    }
}
