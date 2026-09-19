using System;
using System.Collections.Generic;
using System.Linq;
using Text_Grab.Utilities;

namespace Tests;

/// <summary>
/// Tests for the text splitting behind "Summarize as Meeting Notes". The model calls themselves need
/// a Copilot+ device, but the chunking that decides what gets sent to the model does not.
/// </summary>
public class WinAiMeetingNotesTests
{
    private const int Target = 100;

    /// <summary>Text that already fits is sent to the model in one piece.</summary>
    [Fact]
    public void SplitIntoParts_ShortText_ReturnsSinglePart()
    {
        string input = "Standup notes: shipped the OCR fix, starting on the settings page next.";

        List<string> parts = WinAiMeetingNotes.SplitIntoParts(input, Target);

        Assert.Single(parts);
        Assert.Equal(input, parts[0]);
    }

    [Fact]
    public void SplitIntoParts_TextExactlyAtTarget_ReturnsSinglePart()
    {
        string input = new('a', Target);

        List<string> parts = WinAiMeetingNotes.SplitIntoParts(input, Target);

        Assert.Single(parts);
    }

    [Fact]
    public void SplitIntoParts_LongText_EveryPartWithinTarget()
    {
        string input = string.Join(" ", Enumerable.Repeat("discussed the roadmap and agreed on dates", 40));

        List<string> parts = WinAiMeetingNotes.SplitIntoParts(input, Target);

        Assert.True(parts.Count > 1);
        Assert.All(parts, part => Assert.True(part.Length <= Target, $"Part was {part.Length} characters."));
    }

    /// <summary>Splitting must not lose or reorder any of the meeting text.</summary>
    [Fact]
    public void SplitIntoParts_LongText_PreservesAllWords()
    {
        string input = string.Join("\n", Enumerable.Range(0, 60).Select(index => $"Speaker {index}: point number {index}"));

        List<string> parts = WinAiMeetingNotes.SplitIntoParts(input, Target);

        string[] originalWords = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string[] splitWords = string.Join(" ", parts).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(originalWords, splitWords);
    }

    /// <summary>A blank line is the most natural place to break a transcript.</summary>
    [Fact]
    public void SplitIntoParts_ParagraphBreaks_PrefersBlankLines()
    {
        string paragraph = new('a', 60);
        string input = string.Join("\n\n", paragraph, paragraph, paragraph);

        List<string> parts = WinAiMeetingNotes.SplitIntoParts(input, Target);

        Assert.All(parts, part => Assert.Equal(paragraph, part));
    }

    /// <summary>Text with nowhere good to break still terminates, splitting mid-word as a last resort.</summary>
    [Fact]
    public void SplitIntoParts_NoBreakCharacters_StillSplits()
    {
        string input = new('x', Target * 3);

        List<string> parts = WinAiMeetingNotes.SplitIntoParts(input, Target);

        Assert.Equal(3, parts.Count);
        Assert.All(parts, part => Assert.Equal(Target, part.Length));
    }

    [Fact]
    public void SplitIntoParts_EmptyText_ReturnsSinglePart()
    {
        List<string> parts = WinAiMeetingNotes.SplitIntoParts(string.Empty, Target);

        Assert.Single(parts);
        Assert.Equal(string.Empty, parts[0]);
    }
}
