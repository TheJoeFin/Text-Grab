using Microsoft.Windows.AI.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Utilities;

/// <summary>
/// Turns a long stretch of text — a transcript, a wall of captured chat, scratch notes — into
/// meeting notes: what was discussed, what was decided, and what happens next.
///
/// Windows AI has no meeting-notes skill, so like translation and regex extraction this prompts the
/// shared <see cref="WinAiLanguageModel"/> directly with a purpose-built system prompt rather than
/// bending TextSummarizer or TextRewriter into the job.
///
/// The wrinkle is length: meeting text routinely runs past Phi Silica's context, and summarizing
/// half a transcript twice does not make notes. So a long input is handled map-then-reduce — each
/// part is reduced to plain bullets, and those bullets are written up as one set of notes in a final
/// pass. Short input skips straight to that final pass, which is the common case.
/// </summary>
internal static class WinAiMeetingNotes
{
    /// <summary>Notes should follow the text, with just enough room to phrase an action item.</summary>
    private const float Temperature = 0.3f;

    /// <summary>
    /// Characters per part in the first pass. Deliberately well inside the model's context so the
    /// common case is one request per part with no splitting.
    /// </summary>
    private const int PartChars = 2000;

    /// <summary>Never split a part more than this many times chasing a context that will not fit.</summary>
    private const int MaxSplitDepth = 3;

    /// <summary>The shape of the finished notes, shared by the two prompts that produce them.</summary>
    private const string NotesFormat =
        "Write the notes in Markdown with exactly these sections, in this order:\n" +
        "## Summary — two or three sentences on what the meeting was about.\n" +
        "## Topics Discussed — one bullet per topic, each with the points made about it.\n" +
        "## Decisions — one bullet per decision reached. Write 'None recorded' if there were none.\n" +
        "## Next Steps — one bullet per action item, written as '- [ ] Owner — action (due date)', " +
        "leaving out the owner or the date when the text does not give one. " +
        "Write 'None recorded' if there were none.\n" +
        "Use only what is in the text: never invent attendees, decisions, owners or dates. " +
        "Keep names, numbers and dates exactly as they appear. " +
        "Reply with the notes only: no preamble, no commentary, and never repeat these instructions.";

    private const string NotesSystemPrompt =
        "You are a meeting notes writer. The user sends the transcript or raw notes from a meeting. " +
        NotesFormat;

    private const string PartSystemPrompt =
        "You are taking notes on one part of a longer meeting. The user sends that part of the " +
        "transcript. List what was discussed, and anything that was decided or assigned, as short " +
        "Markdown bullets — one point per bullet, keeping names, numbers and dates exactly as they " +
        "appear. Do not add headings, do not summarize the meeting as a whole, and do not invent " +
        "anything that is not in this part. Reply with the bullets only.";

    private const string MergeSystemPrompt =
        "You are a meeting notes writer. The user sends rough bullets taken from consecutive parts " +
        "of one meeting, in order. Combine them into a single set of notes, merging points that " +
        "repeat. " + NotesFormat;

    /// <summary>
    /// Writes <paramref name="text"/> up as meeting notes. The notes are in
    /// <see cref="WinAiGenerationResult.Text"/>; on failure that is null and
    /// <see cref="WinAiGenerationResult.Message"/> says why.
    /// </summary>
    /// <param name="onProgress">
    /// Optional callback describing the stage in progress, for a loading label. It is raised on a
    /// background thread; marshal to the UI thread before touching controls.
    /// </param>
    internal static async Task<WinAiGenerationResult> SummarizeAsync(
        string text,
        Action<string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return WinAiGenerationResult.Failed(WinAiFailure.ModelError, "There was no text to turn into meeting notes.");

        (bool available, string? reason) = WinAiLanguageModel.CheckAvailability();
        if (!available)
            return WinAiGenerationResult.Failed(
                WinAiFailure.Unavailable, reason ?? "Windows AI is not available on this device.");

        try
        {
            (bool ready, string? error) = await WinAiLanguageModel.EnsureModelAsync(cancellationToken);
            if (!ready)
                return WinAiGenerationResult.Failed(
                    WinAiFailure.ModelNotReady, error ?? "The Windows AI language model could not be started.");

            // One lease for the whole write-up: a set of notes is many requests, and letting another
            // feature interleave with them on the single-threaded model would stall both.
            using (await WinAiLanguageModel.AcquireInferenceAsync(cancellationToken))
            {
                List<string> parts = SplitIntoParts(text, PartChars);

                // Short enough to write up in one go, which is most captures.
                if (parts.Count == 1)
                {
                    WinAiGenerationResult single = await WinAiLanguageModel.GenerateAsync(
                        NotesSystemPrompt, parts[0], Temperature, null, cancellationToken);

                    if (single.Text is not null)
                        return Finish(single.Text);

                    if (single.Failure is not WinAiFailure.PromptTooLong)
                        return single;

                    // The text fit the character budget but not the model's context, so go through
                    // the map-then-reduce path with smaller parts.
                    parts = SplitIntoParts(text, PartChars / 2);
                    if (parts.Count == 1)
                        return single;
                }

                List<string> bullets = [];
                WinAiGenerationResult lastFailure = default;

                for (int index = 0; index < parts.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    onProgress?.Invoke($"Reading part {index + 1} of {parts.Count}...");

                    WinAiGenerationResult part = await SummarizePartAsync(parts[index], 0, cancellationToken);

                    if (part.Text is null)
                    {
                        // One unreadable part should not lose the rest of the meeting.
                        Debug.WriteLine($"Meeting notes part {index + 1} failed ({part.Failure}): {part.Message}");
                        lastFailure = part;
                        continue;
                    }

                    bullets.Add(part.Text.Trim());
                }

                if (bullets.Count == 0)
                    return lastFailure.Message is not null
                        ? lastFailure
                        : WinAiGenerationResult.Failed(WinAiFailure.ModelError, "The language model returned no notes.");

                onProgress?.Invoke("Writing up the notes...");

                WinAiGenerationResult merged = await MergeAsync([.. bullets], cancellationToken);

                return merged.Text is null ? merged : Finish(merged.Text);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Meeting notes exception: {ex.Message}");
            return WinAiGenerationResult.Failed(WinAiFailure.ModelError, $"Meeting notes failed: {ex.Message}");
        }
    }

    private static WinAiGenerationResult Finish(string text)
    {
        string cleaned = WinAiLanguageModel.CleanResponse(text);

        return string.IsNullOrWhiteSpace(cleaned)
            ? WinAiGenerationResult.Failed(WinAiFailure.ModelError, "The notes came back empty.")
            : WinAiGenerationResult.Ok(cleaned);
    }

    /// <summary>
    /// Reduces one part of the text to bullets, halving it and recursing when — and only when — the
    /// model reports the prompt does not fit the context.
    /// </summary>
    private static async Task<WinAiGenerationResult> SummarizePartAsync(
        string part,
        int depth,
        CancellationToken cancellationToken)
    {
        WinAiGenerationResult outcome = await WinAiLanguageModel.GenerateAsync(
            PartSystemPrompt, part, Temperature, null, cancellationToken);

        if (outcome.Text is not null || outcome.Failure is not WinAiFailure.PromptTooLong || depth >= MaxSplitDepth)
            return outcome;

        string[] halves = SplitInHalf(part);
        if (halves.Length < 2)
            return outcome;

        StringBuilder combined = new();
        foreach (string half in halves)
        {
            WinAiGenerationResult piece = await SummarizePartAsync(half, depth + 1, cancellationToken);
            if (piece.Text is null)
                return piece;

            combined.AppendLine(piece.Text.Trim());
        }

        return WinAiGenerationResult.Ok(combined.ToString());
    }

    /// <summary>
    /// Writes the per-part bullets up as one set of notes. When they do not all fit at once, each
    /// half is written up separately and the two write-ups are merged.
    /// </summary>
    private static async Task<WinAiGenerationResult> MergeAsync(
        string[] sections,
        CancellationToken cancellationToken)
    {
        string joined = string.Join("\n\n", sections);

        WinAiGenerationResult merged = await WinAiLanguageModel.GenerateAsync(
            MergeSystemPrompt, joined, Temperature, null, cancellationToken);

        if (merged.Text is not null || merged.Failure is not WinAiFailure.PromptTooLong)
            return merged;

        // Two sections that still will not fit together cannot be merged by splitting again, so
        // hand back the sections themselves rather than nothing at all.
        if (sections.Length < 3)
            return WinAiGenerationResult.Ok(joined);

        int middle = sections.Length / 2;

        WinAiGenerationResult first = await MergeAsync(sections[..middle], cancellationToken);
        if (first.Text is null)
            return first;

        WinAiGenerationResult second = await MergeAsync(sections[middle..], cancellationToken);
        if (second.Text is null)
            return second;

        return await MergeAsync([first.Text, second.Text], cancellationToken);
    }

    /// <summary>
    /// Splits text into parts of roughly <paramref name="targetChars"/>, breaking at a blank line
    /// where possible and at a line break otherwise, so a part rarely stops mid-thought.
    /// </summary>
    internal static List<string> SplitIntoParts(string text, int targetChars)
    {
        if (text.Length <= targetChars)
            return [text];

        List<string> parts = [];
        int start = 0;

        while (start < text.Length)
        {
            if (text.Length - start <= targetChars)
            {
                parts.Add(text[start..]);
                break;
            }

            int limit = start + targetChars;

            // Prefer a paragraph break, then any line break, then a space, and only cut mid-word
            // when the text offers nothing else.
            int breakAt = text.LastIndexOf("\n\n", limit, targetChars, StringComparison.Ordinal);
            if (breakAt <= start)
                breakAt = text.LastIndexOf('\n', limit, targetChars);
            if (breakAt <= start)
                breakAt = text.LastIndexOf(' ', limit, targetChars);
            if (breakAt <= start)
                breakAt = limit;

            parts.Add(text[start..breakAt]);
            start = breakAt;

            while (start < text.Length && (text[start] == '\n' || text[start] == '\r' || text[start] == ' '))
                start++;
        }

        return parts;
    }

    private static string[] SplitInHalf(string text)
    {
        if (text.Length < 200)
            return [text];

        int middle = text.Length / 2;
        int splitAt = text.LastIndexOf('\n', middle);

        if (splitAt <= 0)
            splitAt = text.IndexOf('\n', middle);

        if (splitAt <= 0)
        {
            splitAt = text.LastIndexOf(' ', middle);
            if (splitAt <= 0)
                return [text];
        }

        return [text[..(splitAt + 1)], text[(splitAt + 1)..]];
    }
}
