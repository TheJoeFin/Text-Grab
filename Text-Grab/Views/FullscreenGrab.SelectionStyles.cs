using Dapplo.Windows.User32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Text_Grab.Extensions;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Bitmap = System.Drawing.Bitmap;
using Point = System.Windows.Point;

namespace Text_Grab.Views;

public partial class FullscreenGrab
{
    private enum SelectionInteractionMode
    {
        None = 0,
        CreatingRectangle = 1,
        CreatingFreeform = 2,
        MovingSelection = 3,
        ResizeLeft = 4,
        ResizeTop = 5,
        ResizeRight = 6,
        ResizeBottom = 7,
        ResizeTopLeft = 8,
        ResizeTopRight = 9,
        ResizeBottomLeft = 10,
        ResizeBottomRight = 11,
    }

    private const double MinimumSelectionSize = 6.0;
    private const double AdjustHandleSize = 12.0;
    private static readonly SolidColorBrush SelectionBorderBrush = new(System.Windows.Media.Color.FromArgb(255, 40, 118, 126));
    private static readonly SolidColorBrush WindowSelectionFillBrush = new(System.Windows.Media.Color.FromArgb(52, 255, 255, 255));
    private static readonly SolidColorBrush WindowSelectionLabelBackgroundBrush = new(System.Windows.Media.Color.FromArgb(224, 20, 27, 46));
    private static readonly SolidColorBrush FreeformFillBrush = new(System.Windows.Media.Color.FromArgb(36, 40, 118, 126));
    private readonly DispatcherTimer windowSelectionTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Path freeformSelectionPath = new()
    {
        Stroke = SelectionBorderBrush,
        Fill = FreeformFillBrush,
        StrokeThickness = 2,
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false
    };

    private readonly List<Point> freeformSelectionPoints = [];
    private readonly List<Border> selectionHandleBorders = [];
    private readonly Border selectionOutlineBorder = new();
    private readonly Grid windowSelectionHighlightContent = new() { ClipToBounds = false, IsHitTestVisible = false };
    private readonly Border windowSelectionInfoBadge = new();
    private readonly TextBlock windowSelectionAppNameText = new();
    private readonly TextBlock windowSelectionTitleText = new();
    private Point adjustmentStartPoint = new();
    private Rect selectionRectBeforeDrag = Rect.Empty;
    private WindowSelectionCandidate? clickedWindowCandidate;
    private WindowSelectionCandidate? hoveredWindowCandidate;
    private SelectionInteractionMode selectionInteractionMode = SelectionInteractionMode.None;
    private FsgSelectionStyle currentSelectionStyle = FsgSelectionStyle.Region;
    private bool isAwaitingAdjustAfterCommit = false;
    private bool suppressSelectionStyleComboBoxSelectionChanged = false;

    private FsgSelectionStyle CurrentSelectionStyle => currentSelectionStyle;

    private void InitializeSelectionStyles()
    {
        selectBorder.BorderThickness = new Thickness(2);
        selectBorder.BorderBrush = SelectionBorderBrush;
        selectBorder.Background = Brushes.Transparent;
        selectBorder.CornerRadius = new CornerRadius(6);
        selectBorder.IsHitTestVisible = false;
        selectBorder.SnapsToDevicePixels = true;

        selectionOutlineBorder.BorderThickness = new Thickness(2);
        selectionOutlineBorder.BorderBrush = SelectionBorderBrush;
        selectionOutlineBorder.Background = Brushes.Transparent;
        selectionOutlineBorder.CornerRadius = new CornerRadius(0);
        selectionOutlineBorder.IsHitTestVisible = false;
        selectionOutlineBorder.SnapsToDevicePixels = true;

        windowSelectionAppNameText.FontWeight = FontWeights.SemiBold;
        windowSelectionAppNameText.Foreground = Brushes.White;
        windowSelectionAppNameText.TextTrimming = TextTrimming.CharacterEllipsis;

        windowSelectionTitleText.Margin = new Thickness(0, 2, 0, 0);
        windowSelectionTitleText.Foreground = Brushes.White;
        windowSelectionTitleText.TextTrimming = TextTrimming.CharacterEllipsis;
        windowSelectionTitleText.TextWrapping = TextWrapping.NoWrap;

        StackPanel windowSelectionTextStack = new()
        {
            MaxWidth = 360,
            Orientation = Orientation.Vertical
        };
        windowSelectionTextStack.Children.Add(windowSelectionAppNameText);
        windowSelectionTextStack.Children.Add(windowSelectionTitleText);

        windowSelectionInfoBadge.Background = WindowSelectionLabelBackgroundBrush;
        windowSelectionInfoBadge.CornerRadius = new CornerRadius(4);
        windowSelectionInfoBadge.HorizontalAlignment = HorizontalAlignment.Left;
        windowSelectionInfoBadge.Margin = new Thickness(8);
        windowSelectionInfoBadge.Padding = new Thickness(8, 5, 8, 6);
        windowSelectionInfoBadge.VerticalAlignment = VerticalAlignment.Top;
        windowSelectionInfoBadge.Child = windowSelectionTextStack;

        windowSelectionHighlightContent.Children.Add(windowSelectionInfoBadge);
        windowSelectionTimer.Tick += WindowSelectionTimer_Tick;
    }

    private void ApplySelectionStyle(FsgSelectionStyle selectionStyle, bool persistToSettings = true)
    {
        currentSelectionStyle = selectionStyle;
        SyncSelectionStyleComboBox(selectionStyle);

        RegionSelectionMenuItem.IsChecked = selectionStyle == FsgSelectionStyle.Region;
        WindowSelectionMenuItem.IsChecked = selectionStyle == FsgSelectionStyle.Window;
        FreeformSelectionMenuItem.IsChecked = selectionStyle == FsgSelectionStyle.Freeform;
        AdjustAfterSelectionMenuItem.IsChecked = selectionStyle == FsgSelectionStyle.AdjustAfter;

        if (persistToSettings)
        {
            DefaultSettings.FsgSelectionStyle = selectionStyle.ToString();
            DefaultSettings.Save();
        }

        ResetSelectionVisualState();
        RegionClickCanvas.Cursor = selectionStyle == FsgSelectionStyle.Window ? Cursors.Hand : Cursors.Cross;
        UpdateTopToolbarVisibility(RegionClickCanvas.IsMouseOver || TopButtonsStackPanel.IsMouseOver);

        if (selectionStyle == FsgSelectionStyle.Window)
            UpdateWindowSelectionHighlight();
    }

    internal static bool ShouldKeepTopToolbarVisible(FsgSelectionStyle selectionStyle, bool isAwaitingAdjustAfterCommit)
    {
        return selectionStyle == FsgSelectionStyle.Window || isAwaitingAdjustAfterCommit;
    }

    internal static bool ShouldCommitWindowSelection(WindowSelectionCandidate? pressedWindowCandidate, WindowSelectionCandidate? releasedWindowCandidate)
    {
        return pressedWindowCandidate is not null
            && releasedWindowCandidate is not null
            && pressedWindowCandidate.Handle == releasedWindowCandidate.Handle;
    }

    internal static bool ShouldUseOverlayCutout(FsgSelectionStyle selectionStyle)
    {
        return selectionStyle is FsgSelectionStyle.Region or FsgSelectionStyle.AdjustAfter;
    }

    internal static bool ShouldDrawSelectionOutline(FsgSelectionStyle selectionStyle)
    {
        return ShouldUseOverlayCutout(selectionStyle);
    }

    private static Key GetSelectionStyleKey(FsgSelectionStyle selectionStyle)
    {
        return selectionStyle switch
        {
            FsgSelectionStyle.Region => Key.R,
            FsgSelectionStyle.Window => Key.W,
            FsgSelectionStyle.Freeform => Key.D,
            FsgSelectionStyle.AdjustAfter => Key.A,
            _ => Key.R,
        };
    }

    private bool TryGetSelectionStyle(object? sender, out FsgSelectionStyle selectionStyle)
    {
        selectionStyle = FsgSelectionStyle.Region;
        if (sender is not FrameworkElement element || element.Tag is not string tag)
            return false;

        return Enum.TryParse(tag, true, out selectionStyle);
    }

    private void SelectionStyleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetSelectionStyle(sender, out FsgSelectionStyle selectionStyle))
            WindowUtilities.FullscreenKeyDown(GetSelectionStyleKey(selectionStyle));
    }

    private void SelectionStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionStyleComboBoxSelectionChanged
            || SelectionStyleComboBox.SelectedItem is not ComboBoxItem selectedItem)
            return;

        if (TryGetSelectionStyle(selectedItem, out FsgSelectionStyle selectionStyle))
            WindowUtilities.FullscreenKeyDown(GetSelectionStyleKey(selectionStyle));
    }

    private void SyncSelectionStyleComboBox(FsgSelectionStyle selectionStyle)
    {
        suppressSelectionStyleComboBoxSelectionChanged = true;

        try
        {
            foreach (ComboBoxItem comboBoxItem in SelectionStyleComboBox.Items.OfType<ComboBoxItem>())
            {
                if (!TryGetSelectionStyle(comboBoxItem, out FsgSelectionStyle comboBoxItemStyle))
                    continue;

                if (comboBoxItemStyle == selectionStyle)
                {
                    SelectionStyleComboBox.SelectedItem = comboBoxItem;
                    return;
                }
            }

            SelectionStyleComboBox.SelectedIndex = -1;
        }
        finally
        {
            suppressSelectionStyleComboBoxSelectionChanged = false;
        }
    }

    private void WindowSelectionTimer_Tick(object? sender, EventArgs e)
    {
        if (CurrentSelectionStyle != FsgSelectionStyle.Window || selectionInteractionMode != SelectionInteractionMode.None)
        {
            if (hoveredWindowCandidate is not null)
            {
                hoveredWindowCandidate = null;
                clickedWindowCandidate = null;
                ClearSelectionBorderVisual();
            }

            return;
        }

        UpdateWindowSelectionHighlight();
    }

    private void UpdateWindowSelectionHighlight()
    {
        ApplyWindowSelectionHighlight(GetWindowSelectionCandidateAtCurrentMousePosition());
    }

    private void UpdateTopToolbarVisibility(bool isPointerOverSelectionSurface)
    {
        if (ShouldKeepTopToolbarVisible(CurrentSelectionStyle, isAwaitingAdjustAfterCommit))
        {
            TopButtonsStackPanel.Visibility = Visibility.Visible;
            return;
        }

        if (isSelecting)
        {
            TopButtonsStackPanel.Visibility = Visibility.Collapsed;
            return;
        }

        TopButtonsStackPanel.Visibility = isPointerOverSelectionSurface
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private WindowSelectionCandidate? GetWindowSelectionCandidateAtCurrentMousePosition()
    {
        if (!WindowUtilities.GetMousePosition(out Point mousePosition))
            return null;

        return WindowSelectionUtilities.FindWindowAtPoint(
            WindowSelectionUtilities.GetCapturableWindows(GetExcludedWindowHandles()),
            mousePosition);
    }

    private void ApplyWindowSelectionHighlight(WindowSelectionCandidate? candidate)
    {
        hoveredWindowCandidate = candidate;

        if (candidate is null)
        {
            ClearSelectionBorderVisual();
            return;
        }

        Rect windowBounds = GetWindowDeviceBounds();
        Rect intersection = Rect.Intersect(candidate.Bounds, windowBounds);
        if (intersection == Rect.Empty)
        {
            ClearSelectionBorderVisual();
            return;
        }

        Rect localRect = ConvertAbsoluteDeviceRectToLocal(intersection);
        ApplySelectionRect(localRect, WindowSelectionFillBrush, updateTemplateOverlays: false);
        UpdateWindowSelectionInfo(candidate, localRect);
    }

    private IReadOnlyCollection<IntPtr> GetExcludedWindowHandles()
    {
        List<IntPtr> handles = [];
        foreach (Window window in Application.Current.Windows)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
                handles.Add(handle);
        }

        return handles;
    }

    private double GetCurrentDeviceScale()
    {
        PresentationSource? presentationSource = PresentationSource.FromVisual(this);
        return presentationSource?.CompositionTarget is null
            ? 1.0
            : presentationSource.CompositionTarget.TransformToDevice.M11;
    }

    private Rect GetWindowDeviceBounds()
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        Point absolutePosition = this.GetAbsolutePosition();
        return new Rect(absolutePosition.X, absolutePosition.Y, ActualWidth * dpi.DpiScaleX, ActualHeight * dpi.DpiScaleY);
    }

    private Rect ConvertAbsoluteDeviceRectToLocal(Rect absoluteRect)
    {
        PresentationSource? presentationSource = PresentationSource.FromVisual(this);
        if (presentationSource?.CompositionTarget is null)
            return Rect.Empty;

        Point absoluteWindowPosition = this.GetAbsolutePosition();
        Matrix fromDevice = presentationSource.CompositionTarget.TransformFromDevice;

        Point topLeft = fromDevice.Transform(new Point(
            absoluteRect.Left - absoluteWindowPosition.X,
            absoluteRect.Top - absoluteWindowPosition.Y));

        Point bottomRight = fromDevice.Transform(new Point(
            absoluteRect.Right - absoluteWindowPosition.X,
            absoluteRect.Bottom - absoluteWindowPosition.Y));

        return new Rect(topLeft, bottomRight);
    }

    private Rect GetCurrentSelectionRect()
    {
        double left = Canvas.GetLeft(selectBorder);
        double top = Canvas.GetTop(selectBorder);

        if (double.IsNaN(left) || double.IsNaN(top))
            return Rect.Empty;

        return new Rect(left, top, selectBorder.Width, selectBorder.Height);
    }

    private void ApplySelectionRect(
        Rect rect,
        Brush? selectionFillBrush = null,
        bool updateTemplateOverlays = true,
        bool? useOverlayCutout = null)
    {
        EnsureSelectionBorderVisible();
        bool shouldUseCutout = useOverlayCutout ?? ShouldUseOverlayCutout(CurrentSelectionStyle);

        selectBorder.Width = Math.Max(0, rect.Width);
        selectBorder.Height = Math.Max(0, rect.Height);
        selectBorder.Background = selectionFillBrush ?? Brushes.Transparent;
        Canvas.SetLeft(selectBorder, rect.Left);
        Canvas.SetTop(selectBorder, rect.Top);
        clippingGeometry.Rect = shouldUseCutout
            ? rect
            : Rect.Empty;
        UpdateSelectionOutline(rect, shouldUseCutout && ShouldDrawSelectionOutline(CurrentSelectionStyle));

        if (updateTemplateOverlays)
            UpdateTemplateRegionOverlays(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    private void UpdateWindowSelectionInfo(WindowSelectionCandidate candidate, Rect localRect)
    {
        windowSelectionAppNameText.Text = candidate.DisplayAppName;
        windowSelectionTitleText.Text = candidate.DisplayTitle;
        windowSelectionInfoBadge.MaxWidth = Math.Max(72, localRect.Width - 16);
        selectBorder.Child = windowSelectionHighlightContent;
    }

    private void EnsureSelectionBorderVisible()
    {
        if (!RegionClickCanvas.Children.Contains(selectBorder))
            _ = RegionClickCanvas.Children.Add(selectBorder);
    }

    private void EnsureSelectionOutlineVisible()
    {
        if (!SelectionOutlineHost.Children.Contains(selectionOutlineBorder))
            _ = SelectionOutlineHost.Children.Add(selectionOutlineBorder);
    }

    private void ClearSelectionBorderVisual()
    {
        if (RegionClickCanvas.Children.Contains(selectBorder))
            RegionClickCanvas.Children.Remove(selectBorder);

        ClearSelectionOutline();
        selectBorder.Background = Brushes.Transparent;
        selectBorder.Child = null;
        clippingGeometry.Rect = new Rect(new Point(0, 0), new Size(0, 0));
        TemplateOverlayHost.Children.Clear();
        templateOverlayCanvas.Children.Clear();
    }

    private void UpdateSelectionOutline(Rect rect, bool shouldShowOutline)
    {
        if (!shouldShowOutline || rect.Width <= 0 || rect.Height <= 0)
        {
            ClearSelectionOutline();
            return;
        }

        EnsureSelectionOutlineVisible();
        selectionOutlineBorder.Width = Math.Max(0, rect.Width);
        selectionOutlineBorder.Height = Math.Max(0, rect.Height);
        Canvas.SetLeft(selectionOutlineBorder, rect.Left);
        Canvas.SetTop(selectionOutlineBorder, rect.Top);
    }

    private void ClearSelectionOutline()
    {
        if (SelectionOutlineHost.Children.Contains(selectionOutlineBorder))
            SelectionOutlineHost.Children.Remove(selectionOutlineBorder);
    }

    private void ResetSelectionVisualState()
    {
        isSelecting = false;
        isShiftDown = false;
        isAwaitingAdjustAfterCommit = false;
        selectionInteractionMode = SelectionInteractionMode.None;
        clickedWindowCandidate = null;
        hoveredWindowCandidate = null;
        CurrentScreen = null;

        CursorClipper.UnClipCursor();
        RegionClickCanvas.ReleaseMouseCapture();

        ClearSelectionBorderVisual();
        ClearFreeformSelection();
        ClearSelectionHandles();

        AcceptSelectionButton.Visibility = Visibility.Collapsed;
    }

    private void ClearFreeformSelection()
    {
        freeformSelectionPoints.Clear();
        freeformSelectionPath.Visibility = Visibility.Collapsed;

        if (RegionClickCanvas.Children.Contains(freeformSelectionPath))
            RegionClickCanvas.Children.Remove(freeformSelectionPath);
    }

    private void EnsureFreeformSelectionPath()
    {
        if (!RegionClickCanvas.Children.Contains(freeformSelectionPath))
            _ = RegionClickCanvas.Children.Add(freeformSelectionPath);

        freeformSelectionPath.Visibility = Visibility.Visible;
    }

    private void ClearSelectionHandles()
    {
        foreach (Border handleBorder in selectionHandleBorders)
            RegionClickCanvas.Children.Remove(handleBorder);

        selectionHandleBorders.Clear();
    }

    private void UpdateSelectionHandles()
    {
        ClearSelectionHandles();

        if (!isAwaitingAdjustAfterCommit)
            return;

        Rect selectionRect = GetCurrentSelectionRect();
        if (selectionRect == Rect.Empty)
            return;

        foreach (SelectionInteractionMode handle in new[]
        {
            SelectionInteractionMode.ResizeTopLeft,
            SelectionInteractionMode.ResizeTop,
            SelectionInteractionMode.ResizeTopRight,
            SelectionInteractionMode.ResizeRight,
            SelectionInteractionMode.ResizeBottomRight,
            SelectionInteractionMode.ResizeBottom,
            SelectionInteractionMode.ResizeBottomLeft,
            SelectionInteractionMode.ResizeLeft,
        })
        {
            Rect handleRect = GetHandleRect(selectionRect, handle);
            Border handleBorder = new()
            {
                Width = handleRect.Width,
                Height = handleRect.Height,
                Background = SelectionBorderBrush,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                IsHitTestVisible = false
            };

            selectionHandleBorders.Add(handleBorder);
            _ = RegionClickCanvas.Children.Add(handleBorder);
            Canvas.SetLeft(handleBorder, handleRect.Left);
            Canvas.SetTop(handleBorder, handleRect.Top);
        }
    }

    private Rect GetHandleRect(Rect selectionRect, SelectionInteractionMode handle)
    {
        double halfHandle = AdjustHandleSize / 2.0;
        return handle switch
        {
            SelectionInteractionMode.ResizeTopLeft => new Rect(selectionRect.Left - halfHandle, selectionRect.Top - halfHandle, AdjustHandleSize, AdjustHandleSize),
            SelectionInteractionMode.ResizeTop => new Rect(selectionRect.Left + (selectionRect.Width / 2.0) - halfHandle, selectionRect.Top - halfHandle, AdjustHandleSize, AdjustHandleSize),
            SelectionInteractionMode.ResizeTopRight => new Rect(selectionRect.Right - halfHandle, selectionRect.Top - halfHandle, AdjustHandleSize, AdjustHandleSize),
            SelectionInteractionMode.ResizeRight => new Rect(selectionRect.Right - halfHandle, selectionRect.Top + (selectionRect.Height / 2.0) - halfHandle, AdjustHandleSize, AdjustHandleSize),
            SelectionInteractionMode.ResizeBottomRight => new Rect(selectionRect.Right - halfHandle, selectionRect.Bottom - halfHandle, AdjustHandleSize, AdjustHandleSize),
            SelectionInteractionMode.ResizeBottom => new Rect(selectionRect.Left + (selectionRect.Width / 2.0) - halfHandle, selectionRect.Bottom - halfHandle, AdjustHandleSize, AdjustHandleSize),
            SelectionInteractionMode.ResizeBottomLeft => new Rect(selectionRect.Left - halfHandle, selectionRect.Bottom - halfHandle, AdjustHandleSize, AdjustHandleSize),
            SelectionInteractionMode.ResizeLeft => new Rect(selectionRect.Left - halfHandle, selectionRect.Top + (selectionRect.Height / 2.0) - halfHandle, AdjustHandleSize, AdjustHandleSize),
            _ => Rect.Empty,
        };
    }

    private SelectionInteractionMode GetSelectionInteractionModeForPoint(Point point)
    {
        Rect selectionRect = GetCurrentSelectionRect();
        if (selectionRect == Rect.Empty)
            return SelectionInteractionMode.None;

        foreach (SelectionInteractionMode handle in new[]
        {
            SelectionInteractionMode.ResizeTopLeft,
            SelectionInteractionMode.ResizeTopRight,
            SelectionInteractionMode.ResizeBottomRight,
            SelectionInteractionMode.ResizeBottomLeft,
            SelectionInteractionMode.ResizeTop,
            SelectionInteractionMode.ResizeRight,
            SelectionInteractionMode.ResizeBottom,
            SelectionInteractionMode.ResizeLeft,
        })
        {
            if (GetHandleRect(selectionRect, handle).Contains(point))
                return handle;
        }

        return selectionRect.Contains(point)
            ? SelectionInteractionMode.MovingSelection
            : SelectionInteractionMode.None;
    }

    private static Cursor GetCursorForInteractionMode(SelectionInteractionMode mode)
    {
        return mode switch
        {
            SelectionInteractionMode.MovingSelection => Cursors.SizeAll,
            SelectionInteractionMode.ResizeLeft => Cursors.SizeWE,
            SelectionInteractionMode.ResizeRight => Cursors.SizeWE,
            SelectionInteractionMode.ResizeTop => Cursors.SizeNS,
            SelectionInteractionMode.ResizeBottom => Cursors.SizeNS,
            SelectionInteractionMode.ResizeTopLeft => Cursors.SizeNWSE,
            SelectionInteractionMode.ResizeBottomRight => Cursors.SizeNWSE,
            SelectionInteractionMode.ResizeTopRight => Cursors.SizeNESW,
            SelectionInteractionMode.ResizeBottomLeft => Cursors.SizeNESW,
            _ => Cursors.Cross,
        };
    }

    private void UpdateAdjustAfterCursor(Point point)
    {
        if (!isAwaitingAdjustAfterCommit)
            return;

        SelectionInteractionMode interactionMode = GetSelectionInteractionModeForPoint(point);
        RegionClickCanvas.Cursor = interactionMode == SelectionInteractionMode.None
            ? Cursors.Cross
            : GetCursorForInteractionMode(interactionMode);
    }

    private void BeginRectangleSelection(MouseEventArgs e)
    {
        ResetSelectionVisualState();
        clickedPoint = e.GetPosition(this);
        dpiScale = VisualTreeHelper.GetDpi(this);
        selectionInteractionMode = SelectionInteractionMode.CreatingRectangle;
        isSelecting = true;
        TopButtonsStackPanel.Visibility = Visibility.Collapsed;
        RegionClickCanvas.CaptureMouse();
        CursorClipper.ClipCursor(this);
        ApplySelectionRect(new Rect(clickedPoint, clickedPoint));
        SetCurrentScreenFromMouse();
    }

    private void BeginFreeformSelection(MouseEventArgs e)
    {
        ResetSelectionVisualState();
        selectionInteractionMode = SelectionInteractionMode.CreatingFreeform;
        isSelecting = true;
        TopButtonsStackPanel.Visibility = Visibility.Collapsed;
        RegionClickCanvas.CaptureMouse();
        CursorClipper.ClipCursor(this);

        freeformSelectionPoints.Add(e.GetPosition(this));
        EnsureFreeformSelectionPath();
        freeformSelectionPath.Data = FreeformCaptureUtilities.BuildGeometry(freeformSelectionPoints);
    }

    private bool TryBeginAdjustAfterInteraction(MouseButtonEventArgs e)
    {
        if (!isAwaitingAdjustAfterCommit || !RegionClickCanvas.Children.Contains(selectBorder))
            return false;

        SelectionInteractionMode interactionMode = GetSelectionInteractionModeForPoint(e.GetPosition(this));
        if (interactionMode == SelectionInteractionMode.None)
            return false;

        adjustmentStartPoint = e.GetPosition(this);
        selectionRectBeforeDrag = GetCurrentSelectionRect();
        selectionInteractionMode = interactionMode;
        isSelecting = true;
        RegionClickCanvas.CaptureMouse();
        CursorClipper.ClipCursor(this);
        return true;
    }

    private void SetCurrentScreenFromMouse()
    {
        WindowUtilities.GetMousePosition(out Point mousePoint);
        foreach (DisplayInfo? screen in DisplayInfo.AllDisplayInfos)
        {
            Rect bound = screen.ScaledBounds();
            if (bound.Contains(mousePoint))
            {
                CurrentScreen = screen;
                break;
            }
        }
    }

    private void UpdateRectangleSelection(Point movingPoint)
    {
        if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            PanSelection(movingPoint);
            return;
        }

        isShiftDown = false;

        double left = Math.Min(clickedPoint.X, movingPoint.X);
        double top = Math.Min(clickedPoint.Y, movingPoint.Y);
        double width = Math.Abs(clickedPoint.X - movingPoint.X);
        double height = Math.Abs(clickedPoint.Y - movingPoint.Y);

        ApplySelectionRect(new Rect(left, top, width, height));
    }

    private void UpdateFreeformSelection(Point movingPoint)
    {
        if (freeformSelectionPoints.Count > 0 && (movingPoint - freeformSelectionPoints[^1]).Length < 2)
            return;

        freeformSelectionPoints.Add(movingPoint);
        EnsureFreeformSelectionPath();
        freeformSelectionPath.Data = FreeformCaptureUtilities.BuildGeometry(freeformSelectionPoints);
    }

    private void UpdateAdjustedSelection(Point movingPoint)
    {
        Rect surfaceRect = new(0, 0, RegionClickCanvas.ActualWidth, RegionClickCanvas.ActualHeight);
        if (surfaceRect.Width <= 0 || surfaceRect.Height <= 0)
            surfaceRect = new Rect(0, 0, ActualWidth, ActualHeight);

        Rect updatedRect = selectionRectBeforeDrag;
        if (selectionInteractionMode == SelectionInteractionMode.MovingSelection)
        {
            double newLeft = Math.Clamp(selectionRectBeforeDrag.Left + (movingPoint.X - adjustmentStartPoint.X), 0, Math.Max(0, surfaceRect.Width - selectionRectBeforeDrag.Width));
            double newTop = Math.Clamp(selectionRectBeforeDrag.Top + (movingPoint.Y - adjustmentStartPoint.Y), 0, Math.Max(0, surfaceRect.Height - selectionRectBeforeDrag.Height));
            updatedRect = new Rect(newLeft, newTop, selectionRectBeforeDrag.Width, selectionRectBeforeDrag.Height);
        }
        else
        {
            double left = selectionRectBeforeDrag.Left;
            double top = selectionRectBeforeDrag.Top;
            double right = selectionRectBeforeDrag.Right;
            double bottom = selectionRectBeforeDrag.Bottom;

            switch (selectionInteractionMode)
            {
                case SelectionInteractionMode.ResizeLeft:
                case SelectionInteractionMode.ResizeTopLeft:
                case SelectionInteractionMode.ResizeBottomLeft:
                    left = Math.Clamp(movingPoint.X, 0, right - MinimumSelectionSize);
                    break;
            }

            switch (selectionInteractionMode)
            {
                case SelectionInteractionMode.ResizeRight:
                case SelectionInteractionMode.ResizeTopRight:
                case SelectionInteractionMode.ResizeBottomRight:
                    right = Math.Clamp(movingPoint.X, left + MinimumSelectionSize, surfaceRect.Width);
                    break;
            }

            switch (selectionInteractionMode)
            {
                case SelectionInteractionMode.ResizeTop:
                case SelectionInteractionMode.ResizeTopLeft:
                case SelectionInteractionMode.ResizeTopRight:
                    top = Math.Clamp(movingPoint.Y, 0, bottom - MinimumSelectionSize);
                    break;
            }

            switch (selectionInteractionMode)
            {
                case SelectionInteractionMode.ResizeBottom:
                case SelectionInteractionMode.ResizeBottomLeft:
                case SelectionInteractionMode.ResizeBottomRight:
                    bottom = Math.Clamp(movingPoint.Y, top + MinimumSelectionSize, surfaceRect.Height);
                    break;
            }

            updatedRect = new Rect(new Point(left, top), new Point(right, bottom));
        }

        ApplySelectionRect(updatedRect);
        UpdateSelectionHandles();
    }

    private void EndSelectionInteraction()
    {
        isSelecting = false;
        CursorClipper.UnClipCursor();
        RegionClickCanvas.ReleaseMouseCapture();
        selectionInteractionMode = SelectionInteractionMode.None;
        CurrentScreen = null;
    }

    private async Task FinalizeRectangleSelectionAsync()
    {
        EndSelectionInteraction();

        Rect selectionRect = GetCurrentSelectionRect();
        bool isSmallClick = selectionRect.Width < MinimumSelectionSize || selectionRect.Height < MinimumSelectionSize;

        if (CurrentSelectionStyle == FsgSelectionStyle.AdjustAfter)
        {
            if (isSmallClick)
            {
                ResetSelectionVisualState();
                TopButtonsStackPanel.Visibility = Visibility.Visible;
                return;
            }

            EnterAdjustAfterMode();
            return;
        }

        FullscreenCaptureResult selection = CreateRectangleSelectionResult(CurrentSelectionStyle);
        await CommitSelectionAsync(selection, isSmallClick);
    }

    private async Task FinalizeFreeformSelectionAsync()
    {
        EndSelectionInteraction();

        Rect bounds = FreeformCaptureUtilities.GetBounds(freeformSelectionPoints);
        if (bounds == Rect.Empty || bounds.Width < MinimumSelectionSize || bounds.Height < MinimumSelectionSize)
        {
            ResetSelectionVisualState();
            TopButtonsStackPanel.Visibility = Visibility.Visible;
            return;
        }

        FullscreenCaptureResult? selection = CreateFreeformSelectionResult();
        ResetSelectionVisualState();

        if (selection is not null)
            await CommitSelectionAsync(selection, false);
    }

    private FullscreenCaptureResult CreateRectangleSelectionResult(FsgSelectionStyle selectionStyle)
    {
        Rect selectionRect = GetCurrentSelectionRect();
        PresentationSource? presentationSource = PresentationSource.FromVisual(this);
        Matrix transformToDevice = presentationSource?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        Point absoluteWindowPosition = this.GetAbsolutePosition();

        double left = Math.Round(selectionRect.Left * transformToDevice.M11);
        double top = Math.Round(selectionRect.Top * transformToDevice.M22);
        double width = Math.Max(1, Math.Round(selectionRect.Width * transformToDevice.M11));
        double height = Math.Max(1, Math.Round(selectionRect.Height * transformToDevice.M22));

        return new FullscreenCaptureResult(
            selectionStyle,
            new Rect(absoluteWindowPosition.X + left, absoluteWindowPosition.Y + top, width, height));
    }

    private FullscreenCaptureResult? CreateFreeformSelectionResult()
    {
        if (freeformSelectionPoints.Count < 3)
            return null;

        PresentationSource? presentationSource = PresentationSource.FromVisual(this);
        Matrix transformToDevice = presentationSource?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        Point absoluteWindowPosition = this.GetAbsolutePosition();

        List<Point> devicePoints = [.. freeformSelectionPoints.Select(point =>
        {
            Point devicePoint = transformToDevice.Transform(point);
            return new Point(Math.Round(devicePoint.X), Math.Round(devicePoint.Y));
        })];

        Rect deviceBounds = FreeformCaptureUtilities.GetBounds(devicePoints);
        if (deviceBounds == Rect.Empty)
            return null;

        Rect absoluteCaptureRect = new(
            absoluteWindowPosition.X + deviceBounds.X,
            absoluteWindowPosition.Y + deviceBounds.Y,
            deviceBounds.Width,
            deviceBounds.Height);

        List<Point> relativePoints = [.. devicePoints.Select(point => new Point(point.X - deviceBounds.X, point.Y - deviceBounds.Y))];

        using Bitmap rawBitmap = ImageMethods.GetRegionOfScreenAsBitmap(absoluteCaptureRect.AsRectangle(), cacheResult: false);
        Bitmap maskedBitmap = FreeformCaptureUtilities.CreateMaskedBitmap(rawBitmap, relativePoints);
        Singleton<HistoryService>.Instance.CacheLastBitmap(maskedBitmap);

        BitmapSource captureImage = ImageMethods.BitmapToImageSource(maskedBitmap);

        return new FullscreenCaptureResult(
            FsgSelectionStyle.Freeform,
            absoluteCaptureRect,
            captureImage);
    }

    private FullscreenCaptureResult CreateWindowSelectionResult(WindowSelectionCandidate candidate)
    {
        BitmapSource? capturedImage = ComposeCapturedImageFromFullscreenBackgrounds(candidate.Bounds);
        return new FullscreenCaptureResult(
            FsgSelectionStyle.Window,
            candidate.Bounds,
            capturedImage,
            candidate.Title);
    }

    private static BitmapSource? ComposeCapturedImageFromFullscreenBackgrounds(Rect absoluteCaptureRect)
    {
        if (Application.Current is null || absoluteCaptureRect.IsEmpty || absoluteCaptureRect.Width <= 0 || absoluteCaptureRect.Height <= 0)
            return null;

        int targetWidth = Math.Max(1, (int)Math.Ceiling(absoluteCaptureRect.Width));
        int targetHeight = Math.Max(1, (int)Math.Ceiling(absoluteCaptureRect.Height));
        int drawnSegments = 0;

        DrawingVisual drawingVisual = new();
        using (DrawingContext drawingContext = drawingVisual.RenderOpen())
        {
            drawingContext.DrawRectangle(Brushes.White, null, new Rect(0, 0, targetWidth, targetHeight));

            foreach (FullscreenGrab fullscreenGrab in Application.Current.Windows.OfType<FullscreenGrab>())
            {
                if (fullscreenGrab.BackgroundImage.Source is not BitmapSource backgroundBitmap)
                    continue;

                Rect windowBounds = fullscreenGrab.GetWindowDeviceBounds();
                Rect intersection = Rect.Intersect(windowBounds, absoluteCaptureRect);
                if (intersection.IsEmpty || intersection.Width <= 0 || intersection.Height <= 0)
                    continue;

                int cropX = Math.Max(0, (int)Math.Round(intersection.Left - windowBounds.Left));
                int cropY = Math.Max(0, (int)Math.Round(intersection.Top - windowBounds.Top));
                int cropW = Math.Min((int)Math.Round(intersection.Width), backgroundBitmap.PixelWidth - cropX);
                int cropH = Math.Min((int)Math.Round(intersection.Height), backgroundBitmap.PixelHeight - cropY);

                if (cropW <= 0 || cropH <= 0)
                    continue;

                CroppedBitmap croppedBitmap = new(backgroundBitmap, new Int32Rect(cropX, cropY, cropW, cropH));
                croppedBitmap.Freeze();

                Rect destinationRect = new(
                    intersection.Left - absoluteCaptureRect.Left,
                    intersection.Top - absoluteCaptureRect.Top,
                    cropW,
                    cropH);

                drawingContext.DrawImage(croppedBitmap, destinationRect);
                drawnSegments++;
            }
        }

        if (drawnSegments == 0)
            return null;

        RenderTargetBitmap renderedBitmap = new(targetWidth, targetHeight, 96, 96, PixelFormats.Pbgra32);
        renderedBitmap.Render(drawingVisual);
        renderedBitmap.Freeze();
        return renderedBitmap;
    }

    private void EnterAdjustAfterMode()
    {
        isAwaitingAdjustAfterCommit = true;
        selectionInteractionMode = SelectionInteractionMode.None;
        selectBorder.Background = Brushes.Transparent;
        AcceptSelectionButton.Visibility = Visibility.Visible;
        TopButtonsStackPanel.Visibility = Visibility.Visible;
        UpdateSelectionHandles();
        UpdateAdjustAfterCursor(Mouse.GetPosition(this));
    }

    private Rect GetHistoryPositionRect(FullscreenCaptureResult selection)
    {
        if (selection.SelectionStyle is FsgSelectionStyle.Region or FsgSelectionStyle.AdjustAfter)
        {
            GetDpiAdjustedRegionOfSelectBorder(out _, out double posLeft, out double posTop);
            return new Rect(posLeft, posTop, selectBorder.Width, selectBorder.Height);
        }

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        return new Rect(
            selection.CaptureRegion.X / dpi.DpiScaleX,
            selection.CaptureRegion.Y / dpi.DpiScaleY,
            selection.CaptureRegion.Width / dpi.DpiScaleX,
            selection.CaptureRegion.Height / dpi.DpiScaleY);
    }

    private BitmapSource? GetBitmapSourceForGrabFrame(FullscreenCaptureResult selection)
    {
        if (selection.CapturedImage is not null)
            return selection.CapturedImage;

        if (selection.SelectionStyle is FsgSelectionStyle.Region or FsgSelectionStyle.AdjustAfter
            && BackgroundImage.Source is BitmapSource backgroundBitmap
            && RegionClickCanvas.Children.Contains(selectBorder))
        {
            PresentationSource? presentationSource = PresentationSource.FromVisual(this);
            Matrix transformToDevice = presentationSource?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            Rect selectionRect = GetCurrentSelectionRect();

            if (TryGetBitmapCropRectForSelection(
                selectionRect,
                transformToDevice,
                BackgroundImage.RenderTransform,
                backgroundBitmap.PixelWidth,
                backgroundBitmap.PixelHeight,
                out Int32Rect cropRect))
            {
                CroppedBitmap croppedBitmap = new(backgroundBitmap, cropRect);
                croppedBitmap.Freeze();
                return croppedBitmap;
            }
        }

        using Bitmap capturedBitmap = ImageMethods.GetRegionOfScreenAsBitmap(selection.CaptureRegion.AsRectangle(), cacheResult: false);
        return ImageMethods.BitmapToImageSource(capturedBitmap);
    }

    private async Task PlaceGrabFrameInSelectionRectAsync(FullscreenCaptureResult selection)
    {
        BitmapSource? frozenImage = GetBitmapSourceForGrabFrame(selection);
        ILanguage selectedLanguage = LanguagesComboBox.SelectedItem as ILanguage ?? LanguageUtilities.GetOCRLanguage();
        IntPtr fullscreenGrabHandle = new WindowInteropHelper(this).Handle;
        IReadOnlyCollection<IntPtr>? excludedHandles = fullscreenGrabHandle == IntPtr.Zero ? null : [fullscreenGrabHandle];
        UiAutomationOverlaySnapshot? uiAutomationSnapshot = selectedLanguage is UiAutomationLang
            ? await UIAutomationUtilities.GetOverlaySnapshotFromRegionAsync(selection.CaptureRegion, excludedHandles)
            : null;
        GrabFrame grabFrame = frozenImage is not null ? new GrabFrame(frozenImage, uiAutomationSnapshot) : new GrabFrame();

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        Rect selectionRect = new(
            selection.CaptureRegion.X / dpi.DpiScaleX,
            selection.CaptureRegion.Y / dpi.DpiScaleY,
            selection.CaptureRegion.Width / dpi.DpiScaleX,
            selection.CaptureRegion.Height / dpi.DpiScaleY);

        grabFrame.Left = selectionRect.Left - (2 / dpi.PixelsPerDip);
        grabFrame.Top = selectionRect.Top - (48 / dpi.PixelsPerDip);

        if (destinationTextBox is not null)
            grabFrame.DestinationTextBox = destinationTextBox;

        grabFrame.TableToggleButton.IsChecked = TableToggleButton.IsChecked;
        if (selectionRect.Width > 20 && selectionRect.Height > 20)
        {
            grabFrame.Width = selectionRect.Width + 4;
            grabFrame.Height = selectionRect.Height + 74;
        }

        grabFrame.Show();
        grabFrame.Activate();

        DisposeBitmapSource(BackgroundImage);
        WindowUtilities.CloseAllFullscreenGrabs();
    }

    private static bool IsTemplateAction(ButtonInfo action) => action.ClickEvent == "ApplyTemplate_Click";

    private async Task CommitSelectionAsync(FullscreenCaptureResult selection, bool isSmallClick)
    {
        clickedWindowCandidate = null;

        if (NewGrabFrameMenuItem.IsChecked is true)
        {
            await PlaceGrabFrameInSelectionRectAsync(selection);
            return;
        }

        if (LanguagesComboBox.SelectedItem is not ILanguage selectedOcrLang)
            selectedOcrLang = LanguageUtilities.GetOCRLanguage();

        bool isSingleLine = SingleLineMenuItem is not null && SingleLineMenuItem.IsChecked;
        bool isTable = TableMenuItem is not null && TableMenuItem.IsChecked;
        TextFromOCR = string.Empty;
        IntPtr fullscreenGrabHandle = new WindowInteropHelper(this).Handle;
        IReadOnlyCollection<IntPtr>? excludedHandles = fullscreenGrabHandle == IntPtr.Zero ? null : [fullscreenGrabHandle];

        if (isSmallClick && selection.SelectionStyle == FsgSelectionStyle.Region)
        {
            BackgroundBrush.Opacity = 0;
            PresentationSource? presentationSource = PresentationSource.FromVisual(this);
            Matrix transformToDevice = presentationSource?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            Rect selectionRect = GetCurrentSelectionRect();
            Point clickedPointForOcr = transformToDevice.Transform(new Point(
                selectionRect.Left + (selectionRect.Width / 2.0),
                selectionRect.Top + (selectionRect.Height / 2.0)));
            clickedPointForOcr = new Point(
                Math.Round(clickedPointForOcr.X),
                Math.Round(clickedPointForOcr.Y));

            TextFromOCR = await OcrUtilities.GetClickedWordAsync(this, clickedPointForOcr, selectedOcrLang);
        }
        else if (selectedOcrLang is UiAutomationLang)
        {
            TextFromOCR = await OcrUtilities.GetTextFromAbsoluteRectAsync(selection.CaptureRegion, selectedOcrLang, excludedHandles);
        }
        else if (selection.CapturedImage is not null)
        {
            TextFromOCR = isTable
                ? await OcrUtilities.GetTextFromBitmapSourceAsTableAsync(selection.CapturedImage, selectedOcrLang)
                : await OcrUtilities.GetTextFromBitmapSourceAsync(selection.CapturedImage, selectedOcrLang);
        }
        else if (isTable)
        {
            // TODO: Look into why this happens and find a better way to dispose the bitmap
            // DO NOT add a using statement to this selected bitmap, it crashes the app
            Bitmap selectionBitmap = ImageMethods.GetRegionOfScreenAsBitmap(selection.CaptureRegion.AsRectangle());
            TextFromOCR = await OcrUtilities.GetTextFromBitmapAsTableAsync(selectionBitmap, selectedOcrLang);
        }
        else
        {
            TextFromOCR = await OcrUtilities.GetTextFromAbsoluteRectAsync(selection.CaptureRegion, selectedOcrLang, excludedHandles);
        }

        if (DefaultSettings.UseHistory && !isSmallClick)
        {
            Bitmap? historyBitmap = selection.CapturedImage is not null
                ? ImageMethods.BitmapSourceToBitmap(selection.CapturedImage)
                : Singleton<HistoryService>.Instance.CachedBitmap is Bitmap cachedBitmap
                    ? new Bitmap(cachedBitmap)
                    : null;

            historyInfo = new HistoryInfo
            {
                ID = Guid.NewGuid().ToString(),
                DpiScaleFactor = GetCurrentDeviceScale(),
                LanguageTag = LanguageUtilities.GetLanguageTag(selectedOcrLang),
                LanguageKind = LanguageUtilities.GetLanguageKind(selectedOcrLang),
                CaptureDateTime = DateTimeOffset.Now,
                PositionRect = GetHistoryPositionRect(selection),
                IsTable = TableToggleButton.IsChecked!.Value,
                TextContent = TextFromOCR,
                ImageContent = historyBitmap,
                SourceMode = TextGrabMode.Fullscreen,
                SelectionStyle = selection.SelectionStyle,
            };
        }

        if (string.IsNullOrWhiteSpace(TextFromOCR))
        {
            BackgroundBrush.Opacity = DefaultSettings.FsgShadeOverlay ? .2 : 0.0;
            TopButtonsStackPanel.Visibility = Visibility.Visible;

            if (selection.SelectionStyle == FsgSelectionStyle.AdjustAfter)
                EnterAdjustAfterMode();
            else
                ResetSelectionVisualState();

            return;
        }

        if (NextStepDropDownButton.Flyout is ContextMenu contextMenu)
        {
            bool shouldInsert = false;
            bool showedFreeformTemplateMessage = false;

            foreach (MenuItem menuItem in GetActionablePostGrabMenuItems(contextMenu))
            {
                if (!menuItem.IsChecked || menuItem.Tag is not ButtonInfo action)
                    continue;

                if (action.ClickEvent == "Insert_Click")
                {
                    shouldInsert = true;
                    continue;
                }

                if (!selection.SupportsTemplateActions && IsTemplateAction(action))
                {
                    if (!showedFreeformTemplateMessage)
                    {
                        MessageBox.Show(
                            "Grab Templates are currently available only for rectangular selections. Freeform captures will keep their OCR text without applying templates.",
                            "Text Grab",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        showedFreeformTemplateMessage = true;
                    }

                    continue;
                }

                PostGrabContext grabContext = new(
                    Text: TextFromOCR ?? string.Empty,
                    CaptureRegion: selection.CaptureRegion,
                    DpiScale: GetCurrentDeviceScale(),
                    CapturedImage: selection.CapturedImage,
                    Language: selectedOcrLang,
                    SelectionStyle: selection.SelectionStyle);

                TextFromOCR = await PostGrabActionManager.ExecutePostGrabAction(action, grabContext);
            }

            if (shouldInsert && !DefaultSettings.TryInsert)
            {
                string textToInsert = TextFromOCR;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    await WindowUtilities.TryInsertString(textToInsert);
                });
            }
        }

        if (SendToEditTextToggleButton.IsChecked is true
            && destinationTextBox is null)
        {
            bool isWebSearch = false;
            if (NextStepDropDownButton.Flyout is ContextMenu postCaptureMenu)
            {
                foreach (MenuItem menuItem in GetActionablePostGrabMenuItems(postCaptureMenu))
                {
                    if (menuItem.IsChecked
                        && menuItem.Tag is ButtonInfo action
                        && action.ClickEvent == "WebSearch_Click")
                    {
                        isWebSearch = true;
                        break;
                    }
                }
            }

            if (!isWebSearch)
            {
                EditTextWindow etw = WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
                destinationTextBox = etw.PassedTextControl;
            }
        }

        OutputUtilities.HandleTextFromOcr(
            TextFromOCR,
            isSingleLine,
            isTable,
            destinationTextBox);
        WindowUtilities.CloseAllFullscreenGrabs();
    }

    private async void AcceptSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!isAwaitingAdjustAfterCommit)
            return;

        isAwaitingAdjustAfterCommit = false;
        ClearSelectionHandles();
        AcceptSelectionButton.Visibility = Visibility.Collapsed;

        await CommitSelectionAsync(CreateRectangleSelectionResult(FsgSelectionStyle.AdjustAfter), false);
    }

    private void HandleRegionCanvasMouseDown(MouseButtonEventArgs e)
    {
        switch (CurrentSelectionStyle)
        {
            case FsgSelectionStyle.Window:
                clickedWindowCandidate = GetWindowSelectionCandidateAtCurrentMousePosition() ?? hoveredWindowCandidate;
                ApplyWindowSelectionHighlight(clickedWindowCandidate);

                if (clickedWindowCandidate is not null)
                    RegionClickCanvas.CaptureMouse();
                break;
            case FsgSelectionStyle.Freeform:
                BeginFreeformSelection(e);
                break;
            case FsgSelectionStyle.AdjustAfter:
                if (!TryBeginAdjustAfterInteraction(e))
                    BeginRectangleSelection(e);
                break;
            case FsgSelectionStyle.Region:
            default:
                BeginRectangleSelection(e);
                break;
        }
    }

    private void HandleRegionCanvasMouseMove(MouseEventArgs e)
    {
        Point movingPoint = e.GetPosition(this);

        switch (selectionInteractionMode)
        {
            case SelectionInteractionMode.CreatingRectangle:
                UpdateRectangleSelection(movingPoint);
                break;
            case SelectionInteractionMode.CreatingFreeform:
                UpdateFreeformSelection(movingPoint);
                break;
            case SelectionInteractionMode.None:
                if (CurrentSelectionStyle == FsgSelectionStyle.AdjustAfter)
                    UpdateAdjustAfterCursor(movingPoint);
                break;
            default:
                UpdateAdjustedSelection(movingPoint);
                break;
        }
    }

    private async Task HandleRegionCanvasMouseUpAsync(MouseButtonEventArgs e)
    {
        switch (selectionInteractionMode)
        {
            case SelectionInteractionMode.CreatingRectangle:
                await FinalizeRectangleSelectionAsync();
                break;
            case SelectionInteractionMode.CreatingFreeform:
                await FinalizeFreeformSelectionAsync();
                break;
            case SelectionInteractionMode.MovingSelection:
            case SelectionInteractionMode.ResizeLeft:
            case SelectionInteractionMode.ResizeTop:
            case SelectionInteractionMode.ResizeRight:
            case SelectionInteractionMode.ResizeBottom:
            case SelectionInteractionMode.ResizeTopLeft:
            case SelectionInteractionMode.ResizeTopRight:
            case SelectionInteractionMode.ResizeBottomLeft:
            case SelectionInteractionMode.ResizeBottomRight:
                EndSelectionInteraction();
                UpdateSelectionHandles();
                UpdateAdjustAfterCursor(e.GetPosition(this));
                break;
            default:
                if (CurrentSelectionStyle == FsgSelectionStyle.Window)
                {
                    WindowSelectionCandidate? pressedWindowCandidate = clickedWindowCandidate;
                    WindowSelectionCandidate? releasedWindowCandidate = GetWindowSelectionCandidateAtCurrentMousePosition() ?? hoveredWindowCandidate;

                    if (RegionClickCanvas.IsMouseCaptured)
                        RegionClickCanvas.ReleaseMouseCapture();

                    ApplyWindowSelectionHighlight(releasedWindowCandidate);

                    if (ShouldCommitWindowSelection(pressedWindowCandidate, releasedWindowCandidate)
                        && pressedWindowCandidate is not null)
                    {
                        await CommitSelectionAsync(
                            CreateWindowSelectionResult(pressedWindowCandidate),
                            false);
                    }
                }

                clickedWindowCandidate = null;
                break;
        }
    }
}
