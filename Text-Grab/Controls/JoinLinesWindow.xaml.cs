using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Text_Grab.Utilities;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for JoinLinesWindow.xaml
/// </summary>
public partial class JoinLinesWindow : Wpf.Ui.Controls.FluentWindow
{
    private const int PreviewDebounceDelayMs = 250;
    private const int PreviewLeadingSegmentCount = 3;
    private const int PreviewTrailingSegmentCount = 2;
    private const int PreviewLeadingLineCount = 8;
    private const int PreviewTrailingLineCount = 4;
    private const int PreviewMaxCharsPerSegment = 180;
    private const int PreviewMaxCharsOverall = 420;
    private const int PreviewMaxSourceCharsSingleLine = 240;
    private const string PreviewOmittedText = "[...]";

    private readonly DispatcherTimer previewDebounceTimer = new();
    private PreviewSegment[] previewSourceSegments = [];
    private bool previewUsesSampling;

    public static RoutedCommand JoinLinesCmd = new();
    public static RoutedCommand ApplyCmd = new();

    public JoinLinesWindow()
    {
        InitializeComponent();

        previewDebounceTimer.Interval = TimeSpan.FromMilliseconds(PreviewDebounceDelayMs);
        previewDebounceTimer.Tick += PreviewDebounceTimer_Tick;
    }

    private void JoinLines_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = Owner is EditTextWindow;
    }

    private void JoinLines_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ApplyJoinLines();
        Close();
    }

    private void Apply_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ApplyJoinLines();
    }

    private void ApplyJoinLines()
    {
        if (Owner is not EditTextWindow etwOwner)
            return;

        etwOwner.JoinLinesInEditTextWindow(
            JoiningTextTextBox.Text,
            TrimLineBeforeJoiningToggle.IsChecked is true,
            TextAtBeginningTextBox.Text,
            TextAtEndTextBox.Text);
    }

    private void PreviewDebounceTimer_Tick(object? sender, EventArgs e)
    {
        previewDebounceTimer.Stop();
        UpdatePreview();
    }

    private void PreviewInputChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        previewDebounceTimer.Stop();
        previewDebounceTimer.Start();
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner is EditTextWindow etwOwner)
        {
            (previewSourceSegments, previewUsesSampling) = BuildPreviewSegments(etwOwner.GetSelectedOrAllTextSegmentsForPreview());
            UpdatePreview();
        }

        JoiningTextTextBox.Focus();
        JoiningTextTextBox.SelectAll();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        previewDebounceTimer.Stop();
        previewSourceSegments = [];
        PreviewTextBox.Clear();
    }

    private void UpdatePreview()
    {
        bool previewWasTruncated = false;
        string previewText = BuildPreviewText(ref previewWasTruncated);

        if (!string.Equals(PreviewTextBox.Text, previewText, StringComparison.Ordinal))
            PreviewTextBox.Text = previewText;

        PreviewHeaderTextBlock.Text = previewUsesSampling || previewWasTruncated ? "Preview (sampled)" : "Preview";
    }

    private string BuildPreviewText(ref bool previewWasTruncated)
    {
        if (previewSourceSegments.Length == 0)
            return string.Empty;

        StringBuilder previewBuilder = new(PreviewMaxCharsOverall + 64);

        for (int i = 0; i < previewSourceSegments.Length; i++)
        {
            if (i > 0)
                previewBuilder.Append(Environment.NewLine);

            PreviewSegment previewSegment = previewSourceSegments[i];
            if (previewSegment.IsPlaceholder)
            {
                previewBuilder.Append(previewSegment.Text);
                continue;
            }

            string transformedSegment = previewSegment.Text.JoinLines(
                JoiningTextTextBox.Text,
                TrimLineBeforeJoiningToggle.IsChecked is true,
                TextAtBeginningTextBox.Text,
                TextAtEndTextBox.Text);

            previewBuilder.Append(TruncateMiddle(transformedSegment, PreviewMaxCharsPerSegment, ref previewWasTruncated));
        }

        return TruncateMiddle(previewBuilder.ToString(), PreviewMaxCharsOverall, ref previewWasTruncated);
    }

    private static (PreviewSegment[] Segments, bool UsesSampling) BuildPreviewSegments(IEnumerable<string> sourceSegments)
    {
        List<PreviewSegment> leadingSegments = [];
        Queue<PreviewSegment> trailingSegments = new();
        int totalSegmentCount = 0;
        bool usesSampling = false;

        foreach (string sourceSegment in sourceSegments)
        {
            string previewSegmentText = SampleSegmentText(sourceSegment, out bool segmentWasSampled);
            PreviewSegment previewSegment = new(previewSegmentText);

            if (totalSegmentCount < PreviewLeadingSegmentCount)
                leadingSegments.Add(previewSegment);

            if (PreviewTrailingSegmentCount > 0)
            {
                if (trailingSegments.Count == PreviewTrailingSegmentCount)
                    trailingSegments.Dequeue();

                trailingSegments.Enqueue(previewSegment);
            }

            usesSampling |= segmentWasSampled;
            totalSegmentCount++;
        }

        if (totalSegmentCount <= PreviewLeadingSegmentCount + PreviewTrailingSegmentCount)
        {
            PreviewSegment[] trailingArray = [.. trailingSegments];
            int overlapCount = Math.Max(0, leadingSegments.Count + trailingArray.Length - totalSegmentCount);

            PreviewSegment[] segmentsWithoutOverlap =
                overlapCount == 0 ? trailingArray : trailingArray[overlapCount..];

            return ([.. leadingSegments, .. segmentsWithoutOverlap], usesSampling);
        }

        usesSampling = true;
        return ([.. leadingSegments, new PreviewSegment(PreviewOmittedText, true), .. trailingSegments], usesSampling);
    }

    private static string SampleSegmentText(string sourceText, out bool segmentWasSampled)
    {
        if (string.IsNullOrEmpty(sourceText))
        {
            segmentWasSampled = false;
            return sourceText;
        }

        List<(int Start, int Length)> leadingLineRanges = [];
        Queue<(int Start, int Length)> trailingLineRanges = new();
        int totalLineCount = 0;
        int index = 0;

        while (index < sourceText.Length)
        {
            int lineStart = index;

            while (index < sourceText.Length
                && sourceText[index] != '\r'
                && sourceText[index] != '\n')
            {
                index++;
            }

            int lineLength = index - lineStart;

            if (totalLineCount < PreviewLeadingLineCount)
                leadingLineRanges.Add((lineStart, lineLength));

            if (PreviewTrailingLineCount > 0)
            {
                if (trailingLineRanges.Count == PreviewTrailingLineCount)
                    trailingLineRanges.Dequeue();

                trailingLineRanges.Enqueue((lineStart, lineLength));
            }

            totalLineCount++;

            if (index >= sourceText.Length)
                break;

            if (sourceText[index] == '\r'
                && index + 1 < sourceText.Length
                && sourceText[index + 1] == '\n')
            {
                index += 2;
            }
            else
            {
                index++;
            }
        }

        if (totalLineCount <= 1)
        {
            segmentWasSampled = false;
            string truncatedSingleLine = TruncateMiddle(sourceText, PreviewMaxSourceCharsSingleLine, ref segmentWasSampled);
            return truncatedSingleLine;
        }

        if (totalLineCount <= PreviewLeadingLineCount + PreviewTrailingLineCount)
        {
            segmentWasSampled = false;
            return sourceText;
        }

        segmentWasSampled = true;
        StringBuilder sampledTextBuilder = new();
        AppendLineRanges(sampledTextBuilder, sourceText, leadingLineRanges);
        sampledTextBuilder.Append(Environment.NewLine);
        sampledTextBuilder.Append(PreviewOmittedText);

        foreach ((int start, int length) in trailingLineRanges)
        {
            sampledTextBuilder.Append(Environment.NewLine);
            sampledTextBuilder.Append(sourceText, start, length);
        }

        return sampledTextBuilder.ToString();
    }

    private static void AppendLineRanges(StringBuilder builder, string sourceText, IEnumerable<(int Start, int Length)> lineRanges)
    {
        bool isFirstLine = true;

        foreach ((int start, int length) in lineRanges)
        {
            if (!isFirstLine)
                builder.Append(Environment.NewLine);

            builder.Append(sourceText, start, length);
            isFirstLine = false;
        }
    }

    private static string TruncateMiddle(string text, int maxLength, ref bool wasTruncated)
    {
        if (text.Length <= maxLength)
            return text;

        wasTruncated = true;

        int remainingLength = maxLength - PreviewOmittedText.Length;
        int prefixLength = remainingLength / 2;
        int suffixLength = remainingLength - prefixLength;

        StringBuilder truncatedBuilder = new(maxLength);
        truncatedBuilder.Append(text, 0, prefixLength);
        truncatedBuilder.Append(PreviewOmittedText);
        truncatedBuilder.Append(text, text.Length - suffixLength, suffixLength);
        return truncatedBuilder.ToString();
    }

    private readonly record struct PreviewSegment(string Text, bool IsPlaceholder = false);
}
