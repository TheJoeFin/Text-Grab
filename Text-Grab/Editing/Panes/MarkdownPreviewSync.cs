using System;
using System.Collections.Generic;
using System.Windows.Documents;
using Text_Grab.Utilities;

namespace Text_Grab.Editing.Panes;

/// <summary>
/// Maps a position in a rendered preview back to an offset in the Markdown it was rendered from. The
/// forward map (raw to rendered) is <see cref="MarkdownFlowDocumentUtilities.BuildOffsetMap"/>; this is its
/// inverse, built on the same public records, and read-only over them.
/// </summary>
/// <remarks>
/// The map is built the first time a click needs it, not on every render: building it measures the text of
/// every run. Within a run, the offset is interpolated, so a click lands in the right word but not always
/// on the right letter when syntax such as <c>**</c> was stripped from the rendered text.
/// </remarks>
internal sealed class MarkdownPreviewSync
{
    private readonly FlowDocument document;
    private readonly string source;
    private Dictionary<Paragraph, ParagraphMap>? paragraphs;

    public MarkdownPreviewSync(FlowDocument document, string source)
    {
        this.document = document;
        this.source = source;
    }

    private sealed record ParagraphMap(MarkdownFlowDocumentUtilities.MarkdownOffsetMapping Block, List<MarkdownFlowDocumentUtilities.MarkdownOffsetMapping> Runs);

    /// <summary>
    /// The offset in the source that <paramref name="position"/> corresponds to, or null when it is in
    /// something the source map does not cover (a footnote list, say).
    /// </summary>
    public int? RawOffsetAt(TextPointer position)
    {
        Dictionary<Paragraph, ParagraphMap> map = Paragraphs();
        Paragraph? paragraph = position.Paragraph;
        if (paragraph is null || !map.TryGetValue(paragraph, out ParagraphMap? found))
            return null;

        int local = new TextRange(paragraph.ContentStart, position).Text.Length;
        local = Math.Clamp(local, found.Block.RenderedStart, found.Block.RenderedEnd);

        foreach (MarkdownFlowDocumentUtilities.MarkdownOffsetMapping run in found.Runs)
        {
            if (local < run.RenderedStart || local > run.RenderedEnd)
                continue;

            return Clamp(Interpolate(local, run.RenderedStart, run.RenderedEnd, run.RawStart, run.RawEnd));
        }

        return Clamp(Interpolate(local, found.Block.RenderedStart, found.Block.RenderedEnd, found.Block.RawStart, found.Block.RawEnd));
    }

    private static int Interpolate(int local, int renderedStart, int renderedEnd, int rawStart, int rawEnd)
    {
        double t = (local - renderedStart) / (double)Math.Max(1, renderedEnd - renderedStart);
        return rawStart + (int)Math.Round(t * (rawEnd - rawStart));
    }

    private int Clamp(int offset) => Math.Clamp(offset, 0, source.Length);

    private Dictionary<Paragraph, ParagraphMap> Paragraphs()
    {
        if (paragraphs is not null)
            return paragraphs;

        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);
        Dictionary<Paragraph, ParagraphMap> built = new();
        foreach (MarkdownFlowDocumentUtilities.MarkdownOffsetMapping block in map.Blocks)
            built.TryAdd(block.AnchorParagraph, new ParagraphMap(block, []));

        foreach (MarkdownFlowDocumentUtilities.MarkdownOffsetMapping run in map.Runs)
        {
            if (built.TryGetValue(run.AnchorParagraph, out ParagraphMap? owner))
                owner.Runs.Add(run);
        }

        paragraphs = built;
        return built;
    }
}
