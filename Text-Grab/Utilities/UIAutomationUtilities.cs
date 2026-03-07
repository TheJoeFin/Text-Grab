using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using Text_Grab.Models;
using TextPatternRange = System.Windows.Automation.Text.TextPatternRange;
using TextUnit = System.Windows.Automation.Text.TextUnit;

namespace Text_Grab.Utilities;

public static class UIAutomationUtilities
{
    private const int FastMaxDepth = 2;
    private const int BalancedMaxDepth = 6;
    private const int ThoroughMaxDepth = 12;
    private const int MaxPointAncestorDepth = 5;

    private enum AutomationTextSource
    {
        None = 0,
        NameFallback = 1,
        TextPattern = 2,
        ValuePattern = 3,
        PointTextPattern = 4,
    }

    private readonly record struct TextExtractionCandidate(string Text, AutomationTextSource Source, int Depth);

    public static Task<string> GetTextFromPointAsync(Point screenPoint)
    {
        UiAutomationOptions options = GetOptionsFromSettings();
        return Task.Run(() => GetTextFromPoint(screenPoint, options));
    }

    public static Task<string> GetTextFromRegionAsync(Rect screenRect)
    {
        UiAutomationOptions options = GetOptionsFromSettings(screenRect);
        return Task.Run(() => GetTextFromRegion(screenRect, options));
    }

    public static Task<string> GetTextFromWindowAsync(IntPtr windowHandle, Rect? filterBounds = null)
    {
        UiAutomationOptions options = GetOptionsFromSettings(filterBounds);
        return Task.Run(() => GetTextFromWindow(windowHandle, options));
    }

    internal static UiAutomationOptions GetOptionsFromSettings(Rect? filterBounds = null)
    {
        UiAutomationTraversalMode traversalMode = UiAutomationTraversalMode.Balanced;
        Enum.TryParse(AppUtilities.TextGrabSettings.UiAutomationTraversalMode, true, out traversalMode);

        return new UiAutomationOptions(
            traversalMode,
            AppUtilities.TextGrabSettings.UiAutomationIncludeOffscreen,
            AppUtilities.TextGrabSettings.UiAutomationPreferFocusedElement,
            filterBounds);
    }

    internal static WindowSelectionCandidate? FindTargetWindowCandidate(Rect selectionRect, IEnumerable<WindowSelectionCandidate> candidates)
    {
        Point centerPoint = new(selectionRect.X + (selectionRect.Width / 2), selectionRect.Y + (selectionRect.Height / 2));
        WindowSelectionCandidate? centerCandidate = WindowSelectionUtilities.FindWindowAtPoint(candidates, centerPoint);
        if (centerCandidate is not null)
            return centerCandidate;

        return candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Area = GetIntersectionArea(selectionRect, candidate.Bounds)
            })
            .Where(entry => entry.Area > 0)
            .OrderByDescending(entry => entry.Area)
            .Select(entry => entry.Candidate)
            .FirstOrDefault();
    }

    internal static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            text.Split([Environment.NewLine, "\r", "\n"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(line => string.Join(' ', line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))));
    }

    internal static bool TryAddUniqueText(string? text, ISet<string> seenText, List<string> output)
    {
        string normalizedText = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(normalizedText))
            return false;

        if (!seenText.Add(normalizedText))
            return false;

        output.Add(normalizedText);
        return true;
    }

    internal static bool ShouldUseNameFallback(ControlType controlType)
    {
        return controlType != ControlType.Window
            && controlType != ControlType.Pane
            && controlType != ControlType.Group
            && controlType != ControlType.Custom
            && controlType != ControlType.Table
            && controlType != ControlType.List
            && controlType != ControlType.Tree
            && controlType != ControlType.Menu
            && controlType != ControlType.MenuBar
            && controlType != ControlType.ToolBar
            && controlType != ControlType.TitleBar
            && controlType != ControlType.StatusBar
            && controlType != ControlType.ScrollBar
            && controlType != ControlType.Separator
            && controlType != ControlType.ProgressBar
            && controlType != ControlType.Slider
            && controlType != ControlType.Spinner
            && controlType != ControlType.Calendar
            && controlType != ControlType.DataGrid
            && controlType != ControlType.Header
            && controlType != ControlType.Tab;
    }

    internal static IReadOnlyList<Point> GetSamplePoints(Rect selectionRect)
    {
        if (selectionRect == Rect.Empty || selectionRect.Width <= 0 || selectionRect.Height <= 0)
            return [];

        double[] xRatios = selectionRect.Width < 80 ? [0.5] : [0.2, 0.5, 0.8];
        double[] yRatios = selectionRect.Height < 80 ? [0.5] : [0.2, 0.5, 0.8];

        HashSet<string> seen = new(StringComparer.Ordinal);
        List<Point> samplePoints = [];

        foreach (double yRatio in yRatios)
        {
            foreach (double xRatio in xRatios)
            {
                Point point = new(
                    selectionRect.Left + (selectionRect.Width * xRatio),
                    selectionRect.Top + (selectionRect.Height * yRatio));

                string key = $"{Math.Round(point.X, 2)}|{Math.Round(point.Y, 2)}";
                if (seen.Add(key))
                    samplePoints.Add(point);
            }
        }

        return samplePoints;
    }

    internal static IReadOnlyList<Point> GetPointProbePoints(Point screenPoint)
    {
        const double probeOffset = 2.0;

        return
        [
            screenPoint,
            new Point(screenPoint.X - probeOffset, screenPoint.Y),
            new Point(screenPoint.X + probeOffset, screenPoint.Y),
            new Point(screenPoint.X, screenPoint.Y - probeOffset),
            new Point(screenPoint.X, screenPoint.Y + probeOffset),
        ];
    }

    private static string GetTextFromPoint(Point screenPoint, UiAutomationOptions options)
    {
        TextExtractionCandidate? bestCandidate = null;

        foreach (Point probePoint in GetPointProbePoints(screenPoint))
        {
            AutomationElement? element = GetElementAtPoint(probePoint);
            if (element is null)
                continue;

            TextExtractionCandidate? probeCandidate = GetBestPointTextCandidate(element, probePoint, options, TextUnit.Line);
            if (probeCandidate is not null && IsBetterCandidate(probeCandidate.Value, bestCandidate))
            {
                bestCandidate = probeCandidate;

                if (probePoint == screenPoint
                    && probeCandidate.Value.Source == AutomationTextSource.PointTextPattern
                    && probeCandidate.Value.Depth == 0)
                {
                    break;
                }
            }
        }

        return bestCandidate?.Text ?? string.Empty;
    }

    private static string GetTextFromRegion(Rect screenRect, UiAutomationOptions options)
    {
        List<WindowSelectionCandidate> candidates = WindowSelectionUtilities.GetCapturableWindows();
        WindowSelectionCandidate? targetWindow = FindTargetWindowCandidate(screenRect, candidates);
        if (targetWindow is null)
            return string.Empty;

        if (targetWindow.Handle == IntPtr.Zero)
            return string.Empty;

        try
        {
            AutomationElement root = AutomationElement.FromHandle(targetWindow.Handle);
            HashSet<string> seenText = new(StringComparer.CurrentCulture);
            List<string> extractedText = [];

            AppendTextFromSamplePoints(root, screenRect, options, seenText, extractedText);
            AppendTextFromElementTree(root, options, seenText, extractedText);

            return string.Join(Environment.NewLine, extractedText);
        }
        catch (ElementNotAvailableException)
        {
            return string.Empty;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    private static string GetTextFromWindow(IntPtr windowHandle, UiAutomationOptions options)
    {
        if (windowHandle == IntPtr.Zero)
            return string.Empty;

        try
        {
            AutomationElement root = AutomationElement.FromHandle(windowHandle);
            return ExtractTextFromElementTree(root, options);
        }
        catch (ElementNotAvailableException)
        {
            return string.Empty;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    private static string ExtractTextFromElementTree(AutomationElement root, UiAutomationOptions options)
    {
        HashSet<string> seenText = new(StringComparer.CurrentCulture);
        List<string> extractedText = [];
        AppendTextFromElementTree(root, options, seenText, extractedText);
        return string.Join(Environment.NewLine, extractedText);
    }

    private static void AppendTextFromElementTree(
        AutomationElement root,
        UiAutomationOptions options,
        ISet<string> seenText,
        List<string> extractedText)
    {
        if (options.PreferFocusedElement)
            TryExtractFocusedElementText(root, options, seenText, extractedText);

        foreach (AutomationElement element in EnumerateElements(root, options))
        {
            if (ShouldSkipElementText(element, options))
                continue;

            TryAddUniqueText(ExtractTextFromElement(element, options.FilterBounds), seenText, extractedText);
        }
    }

    private static void AppendTextFromSamplePoints(
        AutomationElement root,
        Rect selectionRect,
        UiAutomationOptions options,
        ISet<string> seenText,
        List<string> extractedText)
    {
        foreach (Point samplePoint in GetSamplePoints(selectionRect))
        {
            AutomationElement? element = GetElementAtPoint(samplePoint);
            if (element is null || !IsDescendantOrSelf(root, element))
                continue;

            TryAddUniqueText(
                GetBestPointText(element, samplePoint, options, TextUnit.Line),
                seenText,
                extractedText);
        }
    }

    private static string GetBestPointText(
        AutomationElement element,
        Point screenPoint,
        UiAutomationOptions options,
        TextUnit pointTextUnit)
    {
        return GetBestPointTextCandidate(element, screenPoint, options, pointTextUnit)?.Text ?? string.Empty;
    }

    private static TextExtractionCandidate? GetBestPointTextCandidate(
        AutomationElement element,
        Point screenPoint,
        UiAutomationOptions options,
        TextUnit pointTextUnit)
    {
        TextExtractionCandidate? bestCandidate = null;
        AutomationElement? current = element;

        for (int depth = 0; current is not null && depth <= MaxPointAncestorDepth; depth++)
        {
            if (!ShouldSkipElementText(current, options)
                && TryCreatePointTextCandidate(current, screenPoint, depth, pointTextUnit, out TextExtractionCandidate candidate)
                && IsBetterCandidate(candidate, bestCandidate))
            {
                bestCandidate = candidate;

                if (candidate.Source == AutomationTextSource.PointTextPattern && candidate.Depth == 0)
                    break;
            }

            current = GetParentElement(current);
        }

        return bestCandidate;
    }

    private static bool TryCreatePointTextCandidate(
        AutomationElement element,
        Point screenPoint,
        int depth,
        TextUnit pointTextUnit,
        out TextExtractionCandidate candidate)
    {
        candidate = default;

        if (TryExtractTextPatternTextAtPoint(element, screenPoint, pointTextUnit, out string pointText))
        {
            candidate = new(NormalizeText(pointText), AutomationTextSource.PointTextPattern, depth);
            return true;
        }

        if (TryExtractValuePatternText(element, out string valuePatternText))
        {
            candidate = new(NormalizeText(valuePatternText), AutomationTextSource.ValuePattern, depth);
            return true;
        }

        if (TryExtractTextPatternText(element, null, out string textPatternText))
        {
            candidate = new(NormalizeText(textPatternText), AutomationTextSource.TextPattern, depth);
            return true;
        }

        if (TryExtractNameText(element, out string nameText))
        {
            candidate = new(NormalizeText(nameText), AutomationTextSource.NameFallback, depth);
            return true;
        }

        return false;
    }

    private static bool IsBetterCandidate(TextExtractionCandidate candidate, TextExtractionCandidate? currentBest)
    {
        if (currentBest is null)
            return true;

        if (candidate.Source != currentBest.Value.Source)
            return candidate.Source > currentBest.Value.Source;

        return candidate.Depth < currentBest.Value.Depth;
    }

    private static void TryExtractFocusedElementText(
        AutomationElement root,
        UiAutomationOptions options,
        ISet<string> seenText,
        List<string> extractedText)
    {
        try
        {
            AutomationElement? focusedElement = AutomationElement.FocusedElement;
            if (focusedElement is null || !IsDescendantOrSelf(root, focusedElement))
                return;

            if (!ShouldSkipElementText(focusedElement, options))
                TryAddUniqueText(ExtractTextFromElement(focusedElement, options.FilterBounds), seenText, extractedText);
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static IEnumerable<AutomationElement> EnumerateElements(AutomationElement root, UiAutomationOptions options)
    {
        Queue<(AutomationElement Element, int Depth)> queue = new();
        queue.Enqueue((root, 0));
        TreeWalker walker = options.TraversalMode == UiAutomationTraversalMode.Thorough
            ? TreeWalker.RawViewWalker
            : TreeWalker.ControlViewWalker;
        int maxDepth = GetMaxDepth(options.TraversalMode);

        while (queue.Count > 0)
        {
            (AutomationElement element, int depth) = queue.Dequeue();
            yield return element;

            if (depth >= maxDepth)
                continue;

            AutomationElement? child = null;
            try
            {
                child = walker.GetFirstChild(element);
            }
            catch (ElementNotAvailableException)
            {
            }

            while (child is not null)
            {
                queue.Enqueue((child, depth + 1));

                try
                {
                    child = walker.GetNextSibling(child);
                }
                catch (ElementNotAvailableException)
                {
                    child = null;
                }
            }
        }
    }

    private static bool ShouldSkipElementText(AutomationElement element, UiAutomationOptions options)
    {
        try
        {
            AutomationElement.AutomationElementInformation current = element.Current;

            if (!options.IncludeOffscreen && current.IsOffscreen)
                return true;

            Rect bounds = current.BoundingRectangle;
            if (bounds == Rect.Empty || bounds.Width < 1 || bounds.Height < 1)
                return true;

            if (!current.IsContentElement && !IsTextBearingControlType(current.ControlType))
                return true;

            if (options.FilterBounds is Rect filterBounds && !bounds.IntersectsWith(filterBounds))
                return true;

            return false;
        }
        catch (ElementNotAvailableException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static string ExtractTextFromElement(AutomationElement element, Rect? filterBounds = null)
    {
        if (TryExtractTextPatternText(element, filterBounds, out string textPatternText))
            return textPatternText;

        if (TryExtractValuePatternText(element, out string valuePatternText))
            return valuePatternText;

        if (TryExtractNameText(element, out string nameText))
            return nameText;

        return string.Empty;
    }

    private static bool TryExtractTextPatternTextAtPoint(
        AutomationElement element,
        Point screenPoint,
        TextUnit preferredUnit,
        out string text)
    {
        text = string.Empty;

        try
        {
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out object pattern)
                && pattern is TextPattern textPattern)
            {
                TextPatternRange range = textPattern.RangeFromPoint(screenPoint);
                range.ExpandToEnclosingUnit(preferredUnit);
                text = range.GetText(-1);

                if (!string.IsNullOrWhiteSpace(text))
                    return true;

                if (preferredUnit != TextUnit.Line)
                {
                    range = textPattern.RangeFromPoint(screenPoint);
                    range.ExpandToEnclosingUnit(TextUnit.Line);
                    text = range.GetText(-1);
                    return !string.IsNullOrWhiteSpace(text);
                }
            }
        }
        catch (ArgumentException)
        {
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return false;
    }

    private static bool TryExtractTextPatternText(AutomationElement element, Rect? filterBounds, out string text)
    {
        text = string.Empty;

        try
        {
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out object pattern)
                && pattern is TextPattern textPattern)
            {
                if (filterBounds is Rect bounds)
                    return TryExtractVisibleTextPatternText(textPattern, bounds, out text);

                text = textPattern.DocumentRange.GetText(-1);
                return !string.IsNullOrWhiteSpace(text);
            }
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return false;
    }

    private static bool TryExtractVisibleTextPatternText(TextPattern textPattern, Rect filterBounds, out string text)
    {
        text = string.Empty;

        try
        {
            TextPatternRange[] visibleRanges = textPattern.GetVisibleRanges();
            if (visibleRanges.Length == 0)
                return false;

            HashSet<string> seenText = new(StringComparer.CurrentCulture);
            List<string> extractedText = [];

            foreach (TextPatternRange range in visibleRanges)
            {
                if (!RangeIntersectsBounds(range, filterBounds))
                    continue;

                TryAddUniqueText(range.GetText(-1), seenText, extractedText);
            }

            text = string.Join(Environment.NewLine, extractedText);
            return !string.IsNullOrWhiteSpace(text);
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return false;
    }

    private static bool RangeIntersectsBounds(TextPatternRange range, Rect filterBounds)
    {
        try
        {
            return range.GetBoundingRectangles().Any(textBounds => textBounds != Rect.Empty && textBounds.IntersectsWith(filterBounds));
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryExtractValuePatternText(AutomationElement element, out string text)
    {
        text = string.Empty;

        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern)
                && pattern is ValuePattern valuePattern)
            {
                text = valuePattern.Current.Value;
                return !string.IsNullOrWhiteSpace(text);
            }
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return false;
    }

    private static bool TryExtractNameText(AutomationElement element, out string text)
    {
        text = string.Empty;

        try
        {
            AutomationElement.AutomationElementInformation current = element.Current;
            if (!ShouldUseNameFallback(current.ControlType))
                return false;

            text = current.Name;
            return !string.IsNullOrWhiteSpace(text);
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return false;
    }

    private static bool IsTextBearingControlType(ControlType controlType)
    {
        return controlType == ControlType.Text
            || controlType == ControlType.Edit
            || controlType == ControlType.Document
            || controlType == ControlType.Button
            || controlType == ControlType.CheckBox
            || controlType == ControlType.RadioButton
            || controlType == ControlType.Hyperlink
            || controlType == ControlType.ListItem
            || controlType == ControlType.DataItem
            || controlType == ControlType.TreeItem
            || controlType == ControlType.MenuItem
            || controlType == ControlType.TabItem
            || controlType == ControlType.HeaderItem
            || controlType == ControlType.ComboBox
            || controlType == ControlType.SplitButton;
    }

    private static AutomationElement? GetParentElement(AutomationElement element)
    {
        try
        {
            return TreeWalker.RawViewWalker.GetParent(element);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static AutomationElement? GetElementAtPoint(Point screenPoint)
    {
        try
        {
            return AutomationElement.FromPoint(screenPoint);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsDescendantOrSelf(AutomationElement root, AutomationElement candidate)
    {
        AutomationElement? current = candidate;
        while (current is not null)
        {
            if (current.Equals(root))
                return true;

            current = GetParentElement(current);
        }

        return false;
    }

    private static int GetMaxDepth(UiAutomationTraversalMode traversalMode)
    {
        return traversalMode switch
        {
            UiAutomationTraversalMode.Fast => FastMaxDepth,
            UiAutomationTraversalMode.Thorough => ThoroughMaxDepth,
            _ => BalancedMaxDepth,
        };
    }

    private static double GetIntersectionArea(Rect first, Rect second)
    {
        Rect intersection = Rect.Intersect(first, second);
        if (intersection == Rect.Empty)
            return 0;

        return intersection.Width * intersection.Height;
    }
}
