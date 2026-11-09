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

    private static readonly Regex SvgNumber = new(@"-?\d+(?:\.\d+)?", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

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
        ReorderSankeyLinks(document);
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

    private sealed record SankeyNode(double X, double Y, double Width, double Height)
    {
        internal double CenterY => Y + Height / 2;
    }

    private sealed class SankeyLink
    {
        internal required XmlElement Element;
        internal required double SourceX, TargetX, SourceTop, SourceBottom, TargetTop, TargetBottom;
        internal int Source = -1, Target = -1, Index;
        internal double NewSourceTop, NewTargetTop;
    }

    /// <summary>
    /// Mermaider stacks each node's ribbons in the opposite order from where the neighbouring nodes sit,
    /// so ribbons cross even when the nodes themselves are ordered sensibly. Re-stack every node's
    /// outgoing ribbons by target position and incoming ribbons by source position (what d3-sankey and
    /// mermaid.js do), leaving the node layout and everything else untouched.
    /// </summary>
    internal static void ReorderSankeyLinks(XmlDocument document)
    {
        List<SankeyNode> nodes = [];
        foreach (XmlElement group in document.GetElementsByTagName("g").Cast<XmlElement>()
            .Where(g => g.GetAttribute("class") == "sankey-node"))
        {
            XmlElement? rect = group.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.LocalName == "rect");
            if (rect is null
                || !TryGetDouble(rect, "x", out double x) || !TryGetDouble(rect, "y", out double y)
                || !TryGetDouble(rect, "width", out double width) || !TryGetDouble(rect, "height", out double height))
                return;
            nodes.Add(new SankeyNode(x, y, width, height));
        }

        if (nodes.Count == 0)
            return;

        List<SankeyLink> links = [];
        foreach (XmlElement path in document.GetElementsByTagName("path").Cast<XmlElement>()
            .Where(p => p.GetAttribute("class") == "sankey-link"))
        {
            MatchCollection numbers = SvgNumber.Matches(path.GetAttribute("d"));
            if (numbers.Count != 16)
                return;

            double[] n = numbers.Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
            SankeyLink link = new()
            {
                Element = path,
                Index = links.Count,
                SourceX = n[0], SourceTop = n[1], SourceBottom = n[15],
                TargetX = n[6], TargetTop = n[7], TargetBottom = n[9],
            };
            link.Source = FindNode(link.SourceX, (link.SourceTop + link.SourceBottom) / 2, atRightEdge: true);
            link.Target = FindNode(link.TargetX, (link.TargetTop + link.TargetBottom) / 2, atRightEdge: false);
            if (link.Source < 0 || link.Target < 0)
                return;
            links.Add(link);
        }

        for (int node = 0; node < nodes.Count; node++)
        {
            SankeyLink[] outgoing = links.Where(l => l.Source == node)
                .OrderBy(l => nodes[l.Target].CenterY).ThenBy(l => l.Index).ToArray();
            if (outgoing.Length > 0)
            {
                double top = outgoing.Min(l => l.SourceTop);
                foreach (SankeyLink link in outgoing)
                {
                    link.NewSourceTop = top;
                    top += link.SourceBottom - link.SourceTop;
                }
            }

            SankeyLink[] incoming = links.Where(l => l.Target == node)
                .OrderBy(l => nodes[l.Source].CenterY).ThenBy(l => l.Index).ToArray();
            if (incoming.Length > 0)
            {
                double top = incoming.Min(l => l.TargetTop);
                foreach (SankeyLink link in incoming)
                {
                    link.NewTargetTop = top;
                    top += link.TargetBottom - link.TargetTop;
                }
            }
        }

        foreach (SankeyLink link in links)
        {
            double sourceBottom = link.NewSourceTop + (link.SourceBottom - link.SourceTop);
            double targetBottom = link.NewTargetTop + (link.TargetBottom - link.TargetTop);
            double middle = (link.SourceX + link.TargetX) / 2;
            string F(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
            link.Element.SetAttribute("d",
                $"M {F(link.SourceX)} {F(link.NewSourceTop)} C {F(middle)} {F(link.NewSourceTop)} {F(middle)} {F(link.NewTargetTop)} {F(link.TargetX)} {F(link.NewTargetTop)} " +
                $"L {F(link.TargetX)} {F(targetBottom)} C {F(middle)} {F(targetBottom)} {F(middle)} {F(sourceBottom)} {F(link.SourceX)} {F(sourceBottom)} Z");
        }

        int FindNode(double x, double centerY, bool atRightEdge)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                SankeyNode node = nodes[i];
                double edge = atRightEdge ? node.X + node.Width : node.X;
                if (Math.Abs(edge - x) < 0.5 && centerY >= node.Y - 0.5 && centerY <= node.Y + node.Height + 0.5)
                    return i;
            }
            return -1;
        }

        static bool TryGetDouble(XmlElement element, string name, out double value) =>
            double.TryParse(element.GetAttribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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
