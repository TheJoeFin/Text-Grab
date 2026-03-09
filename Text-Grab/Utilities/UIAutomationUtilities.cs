using System;
using System.Collections.Generic;
using System.Globalization;
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
    private readonly record struct WindowPointCandidate(TextExtractionCandidate Candidate, double Area);
    private readonly record struct OverlayCandidate(UiAutomationOverlayItem Item, AutomationTextSource Source, int Depth);

    public static Task<string> GetTextFromPointAsync(Point screenPoint)
        => GetTextFromPointAsync(screenPoint, null);

    public static Task<string> GetTextFromPointAsync(Point screenPoint, IReadOnlyCollection<IntPtr>? excludedHandles)
    {
        UiAutomationOptions options = GetOptionsFromSettings();
        return Task.Run(() => GetTextFromPoint(screenPoint, options, excludedHandles));
    }

    public static Task<string> GetTextFromRegionAsync(Rect screenRect)
        => GetTextFromRegionAsync(screenRect, null);

    public static Task<string> GetTextFromRegionAsync(Rect screenRect, IReadOnlyCollection<IntPtr>? excludedHandles)
    {
        UiAutomationOptions options = GetOptionsFromSettings(screenRect);
        return Task.Run(() => GetTextFromRegion(screenRect, options, excludedHandles));
    }

    public static Task<string> GetTextFromWindowAsync(IntPtr windowHandle, Rect? filterBounds = null)
    {
        UiAutomationOptions options = GetOptionsFromSettings(filterBounds);
        return Task.Run(() => GetTextFromWindow(windowHandle, options));
    }

    public static Task<UiAutomationOverlaySnapshot?> GetOverlaySnapshotFromRegionAsync(Rect screenRect)
        => GetOverlaySnapshotFromRegionAsync(screenRect, null);

    public static Task<UiAutomationOverlaySnapshot?> GetOverlaySnapshotFromRegionAsync(Rect screenRect, IReadOnlyCollection<IntPtr>? excludedHandles)
    {
        UiAutomationOptions options = GetOptionsFromSettings(screenRect);
        return Task.Run(() => GetOverlaySnapshotFromRegion(screenRect, options, excludedHandles));
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

    internal static WindowSelectionCandidate? FindPointTargetWindowCandidate(Point screenPoint, IReadOnlyCollection<IntPtr>? excludedHandles)
    {
        List<WindowSelectionCandidate> candidates = WindowSelectionUtilities.GetCapturableWindows(excludedHandles);
        WindowSelectionCandidate? directCandidate = WindowSelectionUtilities.FindWindowAtPoint(candidates, screenPoint);
        if (directCandidate is not null)
            return directCandidate;

        Rect searchRect = new(screenPoint.X - 1, screenPoint.Y - 1, 2, 2);
        return FindTargetWindowCandidate(searchRect, candidates);
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
        return controlType == ControlType.Text
            || controlType == ControlType.Hyperlink
            || controlType == ControlType.ListItem
            || controlType == ControlType.DataItem
            || controlType == ControlType.TreeItem
            || controlType == ControlType.MenuItem
            || controlType == ControlType.TabItem
            || controlType == ControlType.HeaderItem;
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

    internal static bool TryClipBounds(Rect bounds, Rect? filterBounds, out Rect clippedBounds)
    {
        clippedBounds = bounds;

        if (bounds == Rect.Empty || bounds.Width < 1 || bounds.Height < 1)
            return false;

        if (filterBounds is Rect clipBounds)
        {
            clippedBounds = Rect.Intersect(bounds, clipBounds);
            if (clippedBounds == Rect.Empty || clippedBounds.Width < 1 || clippedBounds.Height < 1)
                return false;
        }

        return true;
    }

    internal static string BuildOverlayDedupKey(UiAutomationOverlayItem item)
    {
        return string.Join(
            '|',
            NormalizeText(item.Text),
            Math.Round(item.ScreenBounds.X, 1).ToString(CultureInfo.InvariantCulture),
            Math.Round(item.ScreenBounds.Y, 1).ToString(CultureInfo.InvariantCulture),
            Math.Round(item.ScreenBounds.Width, 1).ToString(CultureInfo.InvariantCulture),
            Math.Round(item.ScreenBounds.Height, 1).ToString(CultureInfo.InvariantCulture));
    }

    internal static bool TryAddUniqueOverlayItem(UiAutomationOverlayItem item, ISet<string> seenItems, List<UiAutomationOverlayItem> output)
    {
        if (string.IsNullOrWhiteSpace(NormalizeText(item.Text)))
            return false;

        string dedupKey = BuildOverlayDedupKey(item);
        if (!seenItems.Add(dedupKey))
            return false;

        output.Add(item);
        return true;
    }

    internal static IReadOnlyList<UiAutomationOverlayItem> SortOverlayItems(IEnumerable<UiAutomationOverlayItem> items)
    {
        return
        [
            .. items.OrderBy(item => Math.Round(item.ScreenBounds.Top, 1))
                .ThenBy(item => Math.Round(item.ScreenBounds.Left, 1))
                .ThenBy(item => item.Text, StringComparer.CurrentCulture)
        ];
    }

    private static string GetTextFromPoint(Point screenPoint, UiAutomationOptions options, IReadOnlyCollection<IntPtr>? excludedHandles)
    {
        if (excludedHandles is not null && excludedHandles.Count > 0)
        {
            string excludedWindowText = GetTextFromPointInUnderlyingWindow(screenPoint, options, excludedHandles);
            if (!string.IsNullOrWhiteSpace(excludedWindowText))
                return excludedWindowText;
        }

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

    private static string GetTextFromPointInUnderlyingWindow(
        Point screenPoint,
        UiAutomationOptions options,
        IReadOnlyCollection<IntPtr> excludedHandles)
    {
        WindowSelectionCandidate? targetWindow = FindPointTargetWindowCandidate(screenPoint, excludedHandles);
        if (targetWindow is null || targetWindow.Handle == IntPtr.Zero)
            return string.Empty;

        try
        {
            AutomationElement root = AutomationElement.FromHandle(targetWindow.Handle);
            WindowPointCandidate? bestCandidate = null;

            foreach ((AutomationElement element, _) in EnumerateElementsWithDepth(root, options))
            {
                if (ShouldSkipElementText(element, options))
                    continue;

                if (!TryGetElementBounds(element, options.FilterBounds, out Rect bounds) || !bounds.Contains(screenPoint))
                    continue;

                if (!TryCreatePointTextCandidate(element, screenPoint, 0, TextUnit.Line, out TextExtractionCandidate candidate))
                    continue;

                WindowPointCandidate windowPointCandidate = new(candidate, Math.Max(1, bounds.Width * bounds.Height));
                if (IsBetterWindowPointCandidate(windowPointCandidate, bestCandidate))
                    bestCandidate = windowPointCandidate;
            }

            return bestCandidate?.Candidate.Text ?? string.Empty;
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

    private static string GetTextFromRegion(Rect screenRect, UiAutomationOptions options, IReadOnlyCollection<IntPtr>? excludedHandles)
    {
        List<WindowSelectionCandidate> candidates = WindowSelectionUtilities.GetCapturableWindows(excludedHandles);
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

    private static UiAutomationOverlaySnapshot? GetOverlaySnapshotFromRegion(
        Rect screenRect,
        UiAutomationOptions options,
        IReadOnlyCollection<IntPtr>? excludedHandles)
    {
        if (screenRect == Rect.Empty || screenRect.Width <= 0 || screenRect.Height <= 0)
            return null;

        List<WindowSelectionCandidate> candidates = WindowSelectionUtilities.GetCapturableWindows(excludedHandles);
        WindowSelectionCandidate? targetWindow = FindTargetWindowCandidate(screenRect, candidates);
        if (targetWindow is null || targetWindow.Handle == IntPtr.Zero)
            return null;

        try
        {
            AutomationElement root = AutomationElement.FromHandle(targetWindow.Handle);
            HashSet<string> seenItems = new(StringComparer.CurrentCulture);
            List<UiAutomationOverlayItem> items = [];

            AppendOverlayItemsFromSamplePoints(root, screenRect, options, seenItems, items);
            AppendOverlayItemsFromElementTree(root, options, seenItems, items);

            return new UiAutomationOverlaySnapshot(screenRect, targetWindow, SortOverlayItems(items));
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

    private static void AppendOverlayItemsFromElementTree(
        AutomationElement root,
        UiAutomationOptions options,
        ISet<string> seenItems,
        List<UiAutomationOverlayItem> overlayItems)
    {
        if (options.PreferFocusedElement)
            TryExtractFocusedElementOverlayItems(root, options, seenItems, overlayItems);

        foreach (AutomationElement element in EnumerateElements(root, options))
        {
            if (ShouldSkipElementText(element, options))
                continue;

            TryAddOverlayItemsFromElement(element, options, seenItems, overlayItems);
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

    private static void AppendOverlayItemsFromSamplePoints(
        AutomationElement root,
        Rect selectionRect,
        UiAutomationOptions options,
        ISet<string> seenItems,
        List<UiAutomationOverlayItem> overlayItems)
    {
        foreach (Point samplePoint in GetSamplePoints(selectionRect))
        {
            AutomationElement? element = GetElementAtPoint(samplePoint);
            if (element is null || !IsDescendantOrSelf(root, element))
                continue;

            OverlayCandidate? candidate = GetBestPointOverlayCandidate(element, samplePoint, options, TextUnit.Line);
            if (candidate is not null)
                TryAddUniqueOverlayItem(candidate.Value.Item, seenItems, overlayItems);
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

    private static OverlayCandidate? GetBestPointOverlayCandidate(
        AutomationElement element,
        Point screenPoint,
        UiAutomationOptions options,
        TextUnit pointTextUnit)
    {
        OverlayCandidate? bestCandidate = null;
        AutomationElement? current = element;

        for (int depth = 0; current is not null && depth <= MaxPointAncestorDepth; depth++)
        {
            if (!ShouldSkipElementText(current, options)
                && TryCreatePointOverlayCandidate(current, screenPoint, depth, pointTextUnit, options.FilterBounds, out OverlayCandidate candidate)
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

    private static bool TryCreatePointOverlayCandidate(
        AutomationElement element,
        Point screenPoint,
        int depth,
        TextUnit pointTextUnit,
        Rect? filterBounds,
        out OverlayCandidate candidate)
    {
        candidate = default;

        if (TryCreatePointTextRangeOverlayItem(element, screenPoint, pointTextUnit, filterBounds, out UiAutomationOverlayItem pointTextItem))
        {
            candidate = new(pointTextItem, AutomationTextSource.PointTextPattern, depth);
            return true;
        }

        if (TryCreateElementBoundsOverlayItem(element, filterBounds, out UiAutomationOverlayItem elementBoundsItem, out AutomationTextSource source))
        {
            candidate = new(elementBoundsItem, source, depth);
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

    private static bool IsBetterCandidate(OverlayCandidate candidate, OverlayCandidate? currentBest)
    {
        if (currentBest is null)
            return true;

        if (candidate.Source != currentBest.Value.Source)
            return candidate.Source > currentBest.Value.Source;

        return candidate.Depth < currentBest.Value.Depth;
    }

    private static bool IsBetterWindowPointCandidate(WindowPointCandidate candidate, WindowPointCandidate? currentBest)
    {
        if (currentBest is null)
            return true;

        if (candidate.Candidate.Source != currentBest.Value.Candidate.Source)
            return candidate.Candidate.Source > currentBest.Value.Candidate.Source;

        return candidate.Area < currentBest.Value.Area;
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

    private static void TryExtractFocusedElementOverlayItems(
        AutomationElement root,
        UiAutomationOptions options,
        ISet<string> seenItems,
        List<UiAutomationOverlayItem> overlayItems)
    {
        try
        {
            AutomationElement? focusedElement = AutomationElement.FocusedElement;
            if (focusedElement is null || !IsDescendantOrSelf(root, focusedElement))
                return;

            if (!ShouldSkipElementText(focusedElement, options))
                TryAddOverlayItemsFromElement(focusedElement, options, seenItems, overlayItems);
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static IEnumerable<(AutomationElement Element, int Depth)> EnumerateElementsWithDepth(AutomationElement root, UiAutomationOptions options)
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
            yield return (element, depth);

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

    private static IEnumerable<AutomationElement> EnumerateElements(AutomationElement root, UiAutomationOptions options)
    {
        foreach ((AutomationElement element, _) in EnumerateElementsWithDepth(root, options))
            yield return element;
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

    private static void TryAddOverlayItemsFromElement(
        AutomationElement element,
        UiAutomationOptions options,
        ISet<string> seenItems,
        List<UiAutomationOverlayItem> overlayItems)
    {
        bool hasVisibleTextRanges = options.FilterBounds is Rect filterBounds
            && TryAddVisibleTextRangeOverlayItems(element, filterBounds, seenItems, overlayItems);

        if (hasVisibleTextRanges)
            return;

        if (TryCreateElementBoundsOverlayItem(element, options.FilterBounds, out UiAutomationOverlayItem overlayItem, out _))
            TryAddUniqueOverlayItem(overlayItem, seenItems, overlayItems);
    }

    private static bool TryAddVisibleTextRangeOverlayItems(
        AutomationElement element,
        Rect filterBounds,
        ISet<string> seenItems,
        List<UiAutomationOverlayItem> overlayItems)
    {
        try
        {
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out object pattern)
                || pattern is not TextPattern textPattern)
            {
                return false;
            }

            TextPatternRange[] visibleRanges = textPattern.GetVisibleRanges();
            bool createdAnyRange = false;

            foreach (TextPatternRange range in visibleRanges)
            {
                if (!TryCreateTextRangeOverlayItem(element, range, filterBounds, UiAutomationOverlaySource.VisibleTextRange, out UiAutomationOverlayItem overlayItem))
                    continue;

                createdAnyRange = true;
                TryAddUniqueOverlayItem(overlayItem, seenItems, overlayItems);
            }

            return createdAnyRange;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryCreatePointTextRangeOverlayItem(
        AutomationElement element,
        Point screenPoint,
        TextUnit preferredUnit,
        Rect? filterBounds,
        out UiAutomationOverlayItem overlayItem)
    {
        overlayItem = default!;

        try
        {
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out object pattern)
                || pattern is not TextPattern textPattern)
            {
                return false;
            }

            TextPatternRange range = textPattern.RangeFromPoint(screenPoint);
            range.ExpandToEnclosingUnit(preferredUnit);

            if (TryCreateTextRangeOverlayItem(element, range, filterBounds, UiAutomationOverlaySource.PointTextRange, out overlayItem))
                return true;

            if (preferredUnit == TextUnit.Line)
                return false;

            range = textPattern.RangeFromPoint(screenPoint);
            range.ExpandToEnclosingUnit(TextUnit.Line);
            return TryCreateTextRangeOverlayItem(element, range, filterBounds, UiAutomationOverlaySource.PointTextRange, out overlayItem);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryCreateTextRangeOverlayItem(
        AutomationElement element,
        TextPatternRange range,
        Rect? filterBounds,
        UiAutomationOverlaySource source,
        out UiAutomationOverlayItem overlayItem)
    {
        overlayItem = default!;
        string text = NormalizeText(range.GetText(-1));
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!TryGetRangeBounds(range, filterBounds, out Rect rangeBounds))
            return false;

        GetElementMetadata(element, out string controlTypeProgrammaticName, out string automationId, out string runtimeId);
        overlayItem = new UiAutomationOverlayItem(text, rangeBounds, source, controlTypeProgrammaticName, automationId, runtimeId);
        return true;
    }

    private static bool TryGetRangeBounds(TextPatternRange range, Rect? filterBounds, out Rect bounds)
    {
        bounds = Rect.Empty;

        try
        {
            Rect aggregateBounds = Rect.Empty;

            foreach (Rect rectangle in range.GetBoundingRectangles())
            {
                if (!TryClipBounds(rectangle, filterBounds, out Rect clippedBounds))
                    continue;

                aggregateBounds = aggregateBounds == Rect.Empty ? clippedBounds : Rect.Union(aggregateBounds, clippedBounds);
            }

            return TryClipBounds(aggregateBounds, filterBounds, out bounds);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryCreateElementBoundsOverlayItem(
        AutomationElement element,
        Rect? filterBounds,
        out UiAutomationOverlayItem overlayItem,
        out AutomationTextSource source)
    {
        overlayItem = default!;
        source = AutomationTextSource.None;

        if (!TryGetElementBounds(element, filterBounds, out Rect bounds))
            return false;

        string text;
        if (TryExtractValuePatternText(element, out string valuePatternText))
        {
            text = NormalizeText(valuePatternText);
            source = AutomationTextSource.ValuePattern;
        }
        else if (TryExtractTextPatternText(element, filterBounds, out string textPatternText))
        {
            text = NormalizeText(textPatternText);
            source = AutomationTextSource.TextPattern;
        }
        else if (TryExtractNameText(element, out string nameText))
        {
            text = NormalizeText(nameText);
            source = AutomationTextSource.NameFallback;
        }
        else
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
            return false;

        GetElementMetadata(element, out string controlTypeProgrammaticName, out string automationId, out string runtimeId);
        overlayItem = new UiAutomationOverlayItem(text, bounds, UiAutomationOverlaySource.ElementBounds, controlTypeProgrammaticName, automationId, runtimeId);
        return true;
    }

    private static bool TryGetElementBounds(AutomationElement element, Rect? filterBounds, out Rect bounds)
    {
        bounds = Rect.Empty;

        try
        {
            return TryClipBounds(element.Current.BoundingRectangle, filterBounds, out bounds);
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool HasVisibleTextDescendant(AutomationElement element)
    {
        const int maxDepth = 2;
        Queue<(AutomationElement Element, int Depth)> queue = new();

        try
        {
            AutomationElement? child = TreeWalker.ControlViewWalker.GetFirstChild(element);
            while (child is not null)
            {
                queue.Enqueue((child, 1));
                child = TreeWalker.ControlViewWalker.GetNextSibling(child);
            }
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        while (queue.Count > 0)
        {
            (AutomationElement currentElement, int depth) = queue.Dequeue();

            try
            {
                ControlType controlType = currentElement.Current.ControlType;
                if (controlType == ControlType.Text
                    || controlType == ControlType.Edit
                    || controlType == ControlType.Document)
                {
                    return true;
                }
            }
            catch (ElementNotAvailableException)
            {
                continue;
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            if (depth >= maxDepth)
                continue;

            try
            {
                AutomationElement? child = TreeWalker.ControlViewWalker.GetFirstChild(currentElement);
                while (child is not null)
                {
                    queue.Enqueue((child, depth + 1));
                    child = TreeWalker.ControlViewWalker.GetNextSibling(child);
                }
            }
            catch (ElementNotAvailableException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        return false;
    }

    private static void GetElementMetadata(
        AutomationElement element,
        out string controlTypeProgrammaticName,
        out string automationId,
        out string runtimeId)
    {
        controlTypeProgrammaticName = string.Empty;
        automationId = string.Empty;
        runtimeId = string.Empty;

        try
        {
            AutomationElement.AutomationElementInformation current = element.Current;
            controlTypeProgrammaticName = current.ControlType?.ProgrammaticName ?? string.Empty;
            automationId = current.AutomationId ?? string.Empty;
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        try
        {
            int[]? rawRuntimeId = element.GetRuntimeId();
            if (rawRuntimeId is { Length: > 0 })
                runtimeId = string.Join('-', rawRuntimeId);
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static bool TryExtractNameText(AutomationElement element, out string text)
    {
        text = string.Empty;

        try
        {
            AutomationElement.AutomationElementInformation current = element.Current;
            if (!ShouldUseNameFallback(current.ControlType))
                return false;

            if (current.ControlType != ControlType.Text && HasVisibleTextDescendant(element))
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
