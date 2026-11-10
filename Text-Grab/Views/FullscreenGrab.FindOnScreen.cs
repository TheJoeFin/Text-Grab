using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Utilities;
using OcrRect = Windows.Foundation.Rect;

namespace Text_Grab.Views;

internal readonly record struct FullscreenFindOnScreenMatch(
    string MatchedText,
    string LineText,
    OcrRect Bounds,
    OcrRect LineBounds);

internal readonly record struct FullscreenFindOnScreenResult(
    int MatchCount,
    IReadOnlyList<FullscreenFindOnScreenMatch> Matches)
{
    public IReadOnlyList<OcrRect> Bounds => [.. Matches.Select(match => match.Bounds)];
}

internal readonly record struct FullscreenFindOnScreenDisplayMatch(
    string MatchedText,
    string LineText,
    Rect Bounds,
    Rect LineBounds);

public partial class FullscreenGrab
{
    private static readonly TimeSpan FindOnScreenDebounceDelay = TimeSpan.FromMilliseconds(250);
    private static readonly SolidColorBrush FindOnScreenFillBrush =
        CreateFrozenBrush(Color.FromArgb(90, 255, 210, 55));
    private static readonly SolidColorBrush FindOnScreenStrokeBrush =
        CreateFrozenBrush(Color.FromArgb(245, 255, 184, 0));

    private CancellationTokenSource? findOnScreenCancellation;
    private Task<FindOnScreenOcrSnapshot?>? findOnScreenOcrTask;
    private string? findOnScreenOcrLanguageTag;
    private int findOnScreenSessionVersion;
    private IReadOnlyList<FullscreenFindOnScreenDisplayMatch> currentFindOnScreenMatches = [];
    private bool isFindOnScreenInstancePickerActive;

    private sealed record FindOnScreenOcrSnapshot(
        IOcrLinesWords Result,
        double Scale,
        ILanguage Language);

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private async void FindOnScreenTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        await QueueFindOnScreenSearchAsync(FindOnScreenTextBox.Text);
    }

    private async void FindOnScreenLaunchButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenFindOnScreenAsync();
    }

    private void FindOnScreenCloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseFindOnScreen();
    }

    private void FindOnScreenEnterKeyButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ExecuteFindOnScreenEnterActionAsync();
    }

    private void FindOnScreenEnterActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded
            || FindOnScreenEnterActionComboBox.SelectedItem is not ComboBoxItem
            {
                Tag: string actionName
            }
            || !Enum.TryParse(actionName, out FsgFindEnterAction action))
        {
            return;
        }

        DefaultSettings.FsgFindEnterAction = action.ToString();
        DefaultSettings.Save();
    }

    private async Task QueueFindOnScreenSearchAsync(string searchText)
    {
        findOnScreenCancellation?.Cancel();
        findOnScreenCancellation?.Dispose();
        findOnScreenCancellation = null;

        FindOnScreenPlaceholder.Visibility = string.IsNullOrEmpty(searchText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        currentFindOnScreenMatches = [];
        isFindOnScreenInstancePickerActive = false;
        ClearFindOnScreenHighlights();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            if (IsFindOnScreenActive)
                FindOnScreenStatusText.Text = "Ready";
            else
                FindOnScreenStatusText.Visibility = Visibility.Collapsed;

            UpdateTopToolbarVisibility(RegionClickCanvas.IsMouseOver || TopButtonsStackPanel.IsMouseOver);
            return;
        }

        TopButtonsStackPanel.Visibility = Visibility.Visible;
        FindOnScreenStatusText.Text = "Searching...";
        FindOnScreenStatusText.Visibility = Visibility.Visible;

        CancellationTokenSource cancellation = new();
        findOnScreenCancellation = cancellation;

        try
        {
            await Task.Delay(FindOnScreenDebounceDelay, cancellation.Token);
            await SearchScreenAsync(searchText, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SearchScreenAsync(string searchText, CancellationToken cancellationToken)
    {
        try
        {
            FindOnScreenOcrSnapshot? snapshot = await GetFindOnScreenOcrSnapshotAsync();
            if (cancellationToken.IsCancellationRequested
                || _isCleanedUp
                || !string.Equals(searchText, FindOnScreenTextBox.Text, StringComparison.Ordinal))
            {
                return;
            }

            if (snapshot is null)
            {
                currentFindOnScreenMatches = [];
                FindOnScreenStatusText.Text = "0 matches";
                FindOnScreenStatusText.Visibility = Visibility.Visible;
                return;
            }

            FullscreenFindOnScreenResult result = FindOcrMatches(
                snapshot.Result,
                searchText,
                snapshot.Language.IsSpaceJoining(),
                snapshot.Language.IsRightToLeft());

            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            List<FullscreenFindOnScreenDisplayMatch> displayMatches =
            [
                .. result.Matches.Select(match => new FullscreenFindOnScreenDisplayMatch(
                    match.MatchedText,
                    match.LineText,
                    ScaleOcrBoundsToOverlay(
                        match.Bounds,
                        snapshot.Scale,
                        dpi.DpiScaleX,
                        dpi.DpiScaleY),
                    ScaleOcrBoundsToOverlay(
                        match.LineBounds,
                        snapshot.Scale,
                        dpi.DpiScaleX,
                        dpi.DpiScaleY)))
            ];

            currentFindOnScreenMatches = displayMatches;
            int matchCount = displayMatches.Count;
            DrawFindOnScreenHighlights([.. displayMatches.Select(match => match.Bounds).Distinct()]);
            FindOnScreenStatusText.Text = matchCount == 1 ? "1 match" : $"{matchCount} matches";
            FindOnScreenStatusText.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            findOnScreenOcrTask = null;
            findOnScreenOcrLanguageTag = null;

            if (!_isCleanedUp && !cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine($"Fullscreen find-on-screen failed: {ex.Message}");
                FindOnScreenStatusText.Text = "Search unavailable";
                FindOnScreenStatusText.Visibility = Visibility.Visible;
            }
        }
    }

    private async Task<FindOnScreenOcrSnapshot?> GetFindOnScreenOcrSnapshotAsync()
    {
        ILanguage selectedLanguage = LanguagesComboBox.SelectedItem as ILanguage
            ?? LanguageUtilities.GetOCRLanguage();
        ILanguage searchLanguage = GetFindOnScreenOcrLanguage(selectedLanguage);
        string languageTag = $"{searchLanguage.GetType().FullName}:{searchLanguage.LanguageTag}";

        if (findOnScreenOcrTask is null
            || !string.Equals(findOnScreenOcrLanguageTag, languageTag, StringComparison.Ordinal))
        {
            BitmapSource? bitmapSource = BackgroundImage.Source as BitmapSource;
            findOnScreenOcrLanguageTag = languageTag;
            findOnScreenOcrTask = RecognizeFindOnScreenAsync(bitmapSource, searchLanguage);
        }

        return await findOnScreenOcrTask;
    }

    private async Task<FindOnScreenOcrSnapshot?> RecognizeFindOnScreenAsync(
        BitmapSource? bitmapSource,
        ILanguage language)
    {
        IOcrLinesWords? result;
        double scale;

        if (bitmapSource is not null)
        {
            using System.Drawing.Bitmap bitmap = ImageMethods.BitmapSourceToBitmap(bitmapSource);
            (result, scale) = await OcrEngineUtilities.GetOcrResultFromBitmapAsync(bitmap, language);
        }
        else
        {
            using System.Drawing.Bitmap bitmap = ImageMethods.GetWindowsBoundsBitmap(this);
            (result, scale) = await OcrEngineUtilities.GetOcrResultFromBitmapAsync(bitmap, language);
        }

        return result is null ? null : new FindOnScreenOcrSnapshot(result, scale, language);
    }

    internal static ILanguage GetFindOnScreenOcrLanguage(ILanguage selectedLanguage)
    {
        if (selectedLanguage is UiAutomationLang or WindowsAiDescriptionLang or TessLang)
            return CaptureLanguageUtilities.GetUiAutomationFallbackLanguage();

        return selectedLanguage;
    }

    internal static FullscreenFindOnScreenResult FindOcrMatches(
        IOcrLinesWords ocrResult,
        string searchText,
        bool isSpaceJoining,
        bool isRightToLeft)
    {
        string query = searchText.Trim();
        if (string.IsNullOrEmpty(query))
            return new FullscreenFindOnScreenResult(0, []);

        List<FullscreenFindOnScreenMatch> matches = [];

        foreach (IOcrLine line in ocrResult.Lines)
        {
            if (line.Words.Length == 0)
            {
                AddLineOnlyMatches(line.Text, line.BoundingBox, query, matches);
                continue;
            }

            (string lineText, IReadOnlyList<(int SourceIndex, int Start, int Length)> segments) =
                TextSearchUtilities.BuildSearchText(
                    [.. line.Words.Select(word => (word.Text, word.BoundingBox.X))],
                    isSpaceJoining,
                    isRightToLeft);

            int matchStart = 0;
            while (matchStart < lineText.Length)
            {
                int foundAt = lineText.IndexOf(query, matchStart, StringComparison.CurrentCultureIgnoreCase);
                if (foundAt < 0)
                    break;

                IReadOnlyList<IOcrWord> matchedWords =
                [
                    .. segments
                        .Where(segment => TextSearchUtilities.SpansOverlap(
                            segment.Start,
                            segment.Length,
                            foundAt,
                            query.Length))
                        .Select(segment => line.Words[segment.SourceIndex])
                ];

                if (matchedWords.Count > 0)
                {
                    matches.Add(new FullscreenFindOnScreenMatch(
                        lineText.Substring(foundAt, query.Length),
                        line.Text,
                        GetUnionBounds(matchedWords),
                        line.BoundingBox));
                }

                matchStart = foundAt + query.Length;
            }
        }

        return new FullscreenFindOnScreenResult(matches.Count, matches);
    }

    private static void AddLineOnlyMatches(
        string lineText,
        OcrRect lineBounds,
        string query,
        List<FullscreenFindOnScreenMatch> matches)
    {
        int matchStart = 0;
        while (matchStart < lineText.Length)
        {
            int foundAt = lineText.IndexOf(query, matchStart, StringComparison.CurrentCultureIgnoreCase);
            if (foundAt < 0)
                break;

            matches.Add(new FullscreenFindOnScreenMatch(
                lineText.Substring(foundAt, query.Length),
                lineText,
                lineBounds,
                lineBounds));
            matchStart = foundAt + query.Length;
        }
    }

    private static OcrRect GetUnionBounds(IReadOnlyList<IOcrWord> words)
    {
        double left = words.Min(word => word.BoundingBox.Left);
        double top = words.Min(word => word.BoundingBox.Top);
        double right = words.Max(word => word.BoundingBox.Right);
        double bottom = words.Max(word => word.BoundingBox.Bottom);
        return new OcrRect(left, top, right - left, bottom - top);
    }

    internal static Rect ScaleOcrBoundsToOverlay(
        OcrRect bounds,
        double ocrScale,
        double dpiScaleX,
        double dpiScaleY)
    {
        double divisorX = ocrScale * dpiScaleX;
        double divisorY = ocrScale * dpiScaleY;

        if (divisorX <= 0 || divisorY <= 0)
            return Rect.Empty;

        return new Rect(
            bounds.X / divisorX,
            bounds.Y / divisorY,
            bounds.Width / divisorX,
            bounds.Height / divisorY);
    }

    private bool IntersectsToolbar(Rect bounds)
    {
        if (TopButtonsStackPanel.Visibility != Visibility.Visible
            || !TopButtonsStackPanel.IsLoaded
            || PresentationSource.FromVisual(this) is null)
        {
            return false;
        }

        Point toolbarPosition = TopButtonsStackPanel.TransformToVisual(this).Transform(new Point(0, 0));
        Rect toolbarBounds = new(toolbarPosition, TopButtonsStackPanel.RenderSize);
        return bounds.IntersectsWith(toolbarBounds);
    }

    private void DrawFindOnScreenHighlights(IReadOnlyList<Rect> bounds)
    {
        ClearFindOnScreenHighlights();
        Rect overlayBounds = new(0, 0, ActualWidth, ActualHeight);

        foreach (Rect rawBounds in bounds)
        {
            Rect highlightBounds = Rect.Intersect(rawBounds, overlayBounds);
            if (highlightBounds.IsEmpty || highlightBounds.Width < 1 || highlightBounds.Height < 1)
                continue;

            highlightBounds.Inflate(2, 2);
            highlightBounds = Rect.Intersect(highlightBounds, overlayBounds);

            Rectangle highlight = new()
            {
                Width = highlightBounds.Width,
                Height = highlightBounds.Height,
                Fill = FindOnScreenFillBrush,
                Stroke = FindOnScreenStrokeBrush,
                StrokeThickness = 2,
                RadiusX = 2,
                RadiusY = 2,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(highlight, highlightBounds.X);
            Canvas.SetTop(highlight, highlightBounds.Y);
            FindOnScreenOverlayHost.Children.Add(highlight);
        }
    }

    private void ClearFindOnScreenHighlights()
    {
        FindOnScreenOverlayHost.Children.Clear();
    }

    private void InvalidateFindOnScreenOcr()
    {
        findOnScreenOcrTask = null;
        findOnScreenOcrLanguageTag = null;
    }

    private void RefreshFindOnScreenSearch()
    {
        InvalidateFindOnScreenOcr();

        if (!string.IsNullOrWhiteSpace(FindOnScreenTextBox.Text))
            _ = QueueFindOnScreenSearchAsync(FindOnScreenTextBox.Text);
        else if (IsFindOnScreenActive)
            _ = ScanFindOnScreenAsync(findOnScreenSessionVersion);
        else
            ClearFindOnScreenHighlights();
    }

    internal static string BuildFindOnScreenCopyText(
            IReadOnlyList<FullscreenFindOnScreenDisplayMatch> matches,
            FsgFindEnterAction action)
    {
            if (matches.Count == 0)
                return string.Empty;

            IEnumerable<string> values = action == FsgFindEnterAction.CopyMatchedLines
                ? matches
                    .GroupBy(match => (match.LineText, match.LineBounds))
                    .Select(group => group.Key.LineText)
                : matches.Select(match => match.MatchedText);

            return string.Join(Environment.NewLine, values);
    }

    internal static Rect GetFindOnScreenRegion(IEnumerable<Rect> matchBounds)
    {
            Rect region = Rect.Empty;

            foreach (Rect bounds in matchBounds.Where(bounds => !bounds.IsEmpty))
                region = region.IsEmpty ? bounds : Rect.Union(region, bounds);

            if (!region.IsEmpty)
                region.Inflate(3, 3);

            return region;
    }

    private async Task ExecuteFindOnScreenEnterActionAsync()
    {
            if (currentFindOnScreenMatches.Count == 0)
            {
                FindOnScreenStatusText.Text = "No matches";
                return;
            }

            FsgFindEnterAction action = GetFindOnScreenEnterAction();
            switch (action)
            {
                case FsgFindEnterAction.CopyMatches:
                case FsgFindEnterAction.CopyMatchedLines:
                    CopyFindOnScreenMatches(action);
                    break;
                case FsgFindEnterAction.ClickInstance:
                    // Nothing to pick between with a single match, so click it and close right away.
                    if (currentFindOnScreenMatches.Count == 1)
                        await ClickFindOnScreenInstanceAsync(currentFindOnScreenMatches[0]);
                    else
                        ShowFindOnScreenInstancePicker();
                    break;
                case FsgFindEnterAction.GrabRegion:
                    await CaptureFindOnScreenRegionAsync(placeGrabFrame: false);
                    break;
                case FsgFindEnterAction.PlaceGrabFrame:
                    await CaptureFindOnScreenRegionAsync(placeGrabFrame: true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
    }

    private void CopyFindOnScreenMatches(FsgFindEnterAction action)
    {
            string text = BuildFindOnScreenCopyText(currentFindOnScreenMatches, action);
            try
            {
                Clipboard.SetDataObject(text, true);
            }
            catch (ExternalException ex)
            {
                // Stay open so the failure is visible and Enter can be retried.
                Debug.WriteLine($"Fullscreen find-on-screen copy failed: {ex.Message}");
                FindOnScreenStatusText.Text = "Copy failed";
                return;
            }

            if (DefaultSettings.ShowToast)
                NotificationUtilities.ShowToast(text);

            WindowUtilities.CloseAllFullscreenGrabs();
    }

    private void ShowFindOnScreenInstancePicker()
    {
            isFindOnScreenInstancePickerActive = true;
            DrawFindOnScreenHighlights([.. currentFindOnScreenMatches.Select(match => match.Bounds).Distinct()]);

            for (int index = 0; index < currentFindOnScreenMatches.Count; index++)
            {
                FullscreenFindOnScreenDisplayMatch match = currentFindOnScreenMatches[index];
                Button marker = new()
                {
                    Content = (index + 1).ToString(),
                    Style = (Style)FindResource("FindOnScreenMarkerButtonStyle"),
                    Background = FindOnScreenStrokeBrush,
                    Tag = match
                };
                AutomationProperties.SetAutomationId(
                    marker,
                    $"FullscreenGrab.FindOnScreenInstance.{index + 1}");
                AutomationProperties.SetName(
                    marker,
                    $"Match {index + 1}: {match.MatchedText}");
                marker.Click += FindOnScreenInstanceButton_Click;

                // Width grows with the digit count (10+ matches), so measure once it's in the tree.
                FindOnScreenOverlayHost.Children.Add(marker);
                marker.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double markerWidth = marker.DesiredSize.Width;
                double markerHeight = marker.DesiredSize.Height;

                double left = Math.Clamp(match.Bounds.Left, 0, Math.Max(0, ActualWidth - markerWidth));
                double top = Math.Clamp(match.Bounds.Top - markerHeight - 2, 0, Math.Max(0, ActualHeight - markerHeight));
                Canvas.SetLeft(marker, left);
                Canvas.SetTop(marker, top);
            }

            FindOnScreenStatusText.Text = $"Pick 1-{currentFindOnScreenMatches.Count}";
            FindOnScreenCloseButton.Focus();
    }

    private async void FindOnScreenInstanceButton_Click(object sender, RoutedEventArgs e)
    {
            if (sender is Button { Tag: FullscreenFindOnScreenDisplayMatch match })
                await ClickFindOnScreenInstanceAsync(match);
    }

    private void PickFindOnScreenInstance(int instanceNumber)
    {
            if (!isFindOnScreenInstancePickerActive
                || instanceNumber < 1
                || instanceNumber > currentFindOnScreenMatches.Count)
            {
                return;
            }

            _ = ClickFindOnScreenInstanceAsync(currentFindOnScreenMatches[instanceNumber - 1]);
    }

    private static bool TryGetFindOnScreenInstanceNumber(Key key, out int instanceNumber)
    {
            instanceNumber = key switch
            {
                >= Key.D1 and <= Key.D9 => (int)key - (int)Key.D0,
                >= Key.NumPad1 and <= Key.NumPad9 => (int)key - (int)Key.NumPad0,
                _ => 0
            };
            return instanceNumber > 0;
    }

    private async Task ClickFindOnScreenInstanceAsync(FullscreenFindOnScreenDisplayMatch match)
    {
            Point screenPoint = OverlayPointToScreen(new Point(
                match.Bounds.Left + (match.Bounds.Width / 2),
                match.Bounds.Top + (match.Bounds.Height / 2)));

            WindowUtilities.CloseAllFullscreenGrabs();
            await Task.Delay(150);
            try
            {
                WindowUtilities.ClickScreenPoint(screenPoint);
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Fullscreen find-on-screen click failed: {ex.Message}");
            }
    }

    private async Task CaptureFindOnScreenRegionAsync(bool placeGrabFrame)
    {
            FullscreenCaptureResult? selection = CreateFindOnScreenCaptureResult(
                currentFindOnScreenMatches.Select(match => match.Bounds));
            if (selection is null)
            {
                FindOnScreenStatusText.Text = "No region";
                return;
            }

            if (placeGrabFrame)
            {
                await PlaceGrabFrameInSelectionRectAsync(selection);
                return;
            }

            bool? placeGrabFramePreference = NewGrabFrameToggleButton.IsChecked;
            NewGrabFrameToggleButton.IsChecked = false;
            await CommitSelectionAsync(selection, isSmallClick: false);
            NewGrabFrameToggleButton.IsChecked = placeGrabFramePreference;
    }

    private FullscreenCaptureResult? CreateFindOnScreenCaptureResult(IEnumerable<Rect> matchBounds)
    {
            Rect overlayRegion = GetFindOnScreenRegion(matchBounds);
            if (overlayRegion.IsEmpty)
                return null;

            overlayRegion = Rect.Intersect(
                overlayRegion,
                new Rect(0, 0, ActualWidth, ActualHeight));
            if (overlayRegion.IsEmpty)
                return null;

            Point screenTopLeft = OverlayPointToScreen(new Point(overlayRegion.X, overlayRegion.Y));
            Point screenBottomRight = OverlayPointToScreen(new Point(overlayRegion.Right, overlayRegion.Bottom));
            Rect captureRegion = new(
                screenTopLeft.X,
                screenTopLeft.Y,
                Math.Max(1, screenBottomRight.X - screenTopLeft.X),
                Math.Max(1, screenBottomRight.Y - screenTopLeft.Y));

            BitmapSource? capturedImage = CropBackgroundToOverlayRegion(overlayRegion);
            return new FullscreenCaptureResult(FsgSelectionStyle.Region, captureRegion, capturedImage);
    }

    private Point OverlayPointToScreen(Point overlayPoint)
    {
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            Point absoluteWindowPosition = this.GetAbsolutePosition();
            return new Point(
                absoluteWindowPosition.X + (overlayPoint.X * dpi.DpiScaleX),
                absoluteWindowPosition.Y + (overlayPoint.Y * dpi.DpiScaleY));
    }

    private BitmapSource? CropBackgroundToOverlayRegion(Rect overlayRegion)
    {
            if (BackgroundImage.Source is not BitmapSource source)
                return null;

            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            int left = Math.Clamp((int)Math.Floor(overlayRegion.X * dpi.DpiScaleX), 0, source.PixelWidth - 1);
            int top = Math.Clamp((int)Math.Floor(overlayRegion.Y * dpi.DpiScaleY), 0, source.PixelHeight - 1);
            int width = Math.Clamp((int)Math.Ceiling(overlayRegion.Width * dpi.DpiScaleX), 1, source.PixelWidth - left);
            int height = Math.Clamp((int)Math.Ceiling(overlayRegion.Height * dpi.DpiScaleY), 1, source.PixelHeight - top);

            CroppedBitmap cropped = new(source, new Int32Rect(left, top, width, height));
            cropped.Freeze();
            return cropped;
    }

    private FsgFindEnterAction GetFindOnScreenEnterAction()
    {
            if (FindOnScreenEnterActionComboBox.SelectedItem is ComboBoxItem { Tag: string actionName }
                && Enum.TryParse(actionName, out FsgFindEnterAction action))
            {
                return action;
            }

            return FsgFindEnterAction.CopyMatches;
    }

    private void LoadFindOnScreenEnterAction()
    {
            FsgFindEnterAction selectedAction = FsgFindEnterAction.CopyMatches;
            if (!Enum.TryParse(DefaultSettings.FsgFindEnterAction, true, out FsgFindEnterAction savedAction))
                savedAction = selectedAction;
            else
                selectedAction = savedAction;

            FindOnScreenEnterActionComboBox.SelectedItem =
                FindOnScreenEnterActionComboBox.Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(item => string.Equals(
                        item.Tag?.ToString(),
                        selectedAction.ToString(),
                        StringComparison.Ordinal));
    }

    private async Task OpenFindOnScreenAsync()
    {
        if (IsFindOnScreenActive)
        {
            FindOnScreenTextBox.Focus();
            FindOnScreenTextBox.SelectAll();
            return;
        }

        findOnScreenSessionVersion++;
        InvalidateFindOnScreenOcr();
        CancelFindOnScreenSearch();

        TopButtonsStackPanel.Visibility = Visibility.Visible;
        CaptureToolbarOptions.Visibility = Visibility.Collapsed;
        FindOnScreenToolbarOptions.Visibility = Visibility.Visible;
        LoadFindOnScreenEnterAction();
        FindOnScreenTextBox.Clear();
        FindOnScreenPlaceholder.Visibility = Visibility.Visible;
        FindOnScreenStatusText.Text = "Scanning...";
        FindOnScreenStatusText.Visibility = Visibility.Visible;
        ClearFindOnScreenHighlights();

        FindOnScreenTextBox.Focus();
        await ScanFindOnScreenAsync(findOnScreenSessionVersion);
    }

    private async Task ScanFindOnScreenAsync(int sessionVersion)
    {
        try
        {
            FindOnScreenOcrSnapshot? snapshot = await GetFindOnScreenOcrSnapshotAsync();
            if (!IsFindOnScreenActive
                || sessionVersion != findOnScreenSessionVersion
                || _isCleanedUp)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(FindOnScreenTextBox.Text))
                FindOnScreenStatusText.Text = snapshot is null ? "No text found" : "Ready";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            findOnScreenOcrTask = null;
            findOnScreenOcrLanguageTag = null;

            if (IsFindOnScreenActive
                && sessionVersion == findOnScreenSessionVersion
                && !_isCleanedUp)
            {
                Debug.WriteLine($"Fullscreen find-on-screen scan failed: {ex.Message}");
                FindOnScreenStatusText.Text = "Search unavailable";
            }
        }
    }

    private void CloseFindOnScreen()
    {
        findOnScreenSessionVersion++;
        CancelFindOnScreenSearch();
        FindOnScreenTextBox.Clear();
        ClearFindOnScreenHighlights();
        FindOnScreenStatusText.Text = string.Empty;
        FindOnScreenStatusText.Visibility = Visibility.Collapsed;
        FindOnScreenToolbarOptions.Visibility = Visibility.Collapsed;
        CaptureToolbarOptions.Visibility = Visibility.Visible;
        isFindOnScreenInstancePickerActive = false;
        TopButtonsStackPanel.Visibility = Visibility.Visible;
        UpdateTopToolbarVisibility(isPointerOverSelectionSurface: true);
        FindOnScreenLaunchButton.Focus();
    }

    private void CancelFindOnScreenSearch()
    {
        findOnScreenCancellation?.Cancel();
        findOnScreenCancellation?.Dispose();
        findOnScreenCancellation = null;
    }

    private void FindOnScreenTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        UpdateTopToolbarVisibility(isPointerOverSelectionSurface: true);
    }

    private void FindOnScreenTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() => UpdateTopToolbarVisibility(
                RegionClickCanvas.IsMouseOver || TopButtonsStackPanel.IsMouseOver)));
    }

    private void CleanupFindOnScreen()
    {
        CancelFindOnScreenSearch();
        findOnScreenOcrTask = null;
        findOnScreenOcrLanguageTag = null;
        FindOnScreenOverlayHost.Children.Clear();

        FindOnScreenLaunchButton.Click -= FindOnScreenLaunchButton_Click;
        FindOnScreenCloseButton.Click -= FindOnScreenCloseButton_Click;
        FindOnScreenEnterKeyButton.Click -= FindOnScreenEnterKeyButton_Click;
        FindOnScreenEnterActionComboBox.SelectionChanged -= FindOnScreenEnterActionComboBox_SelectionChanged;
        FindOnScreenTextBox.TextChanged -= FindOnScreenTextBox_TextChanged;
        FindOnScreenTextBox.GotFocus -= FindOnScreenTextBox_GotFocus;
        FindOnScreenTextBox.LostFocus -= FindOnScreenTextBox_LostFocus;
    }

    private bool IsFindOnScreenActive => FindOnScreenToolbarOptions.Visibility == Visibility.Visible;
}
