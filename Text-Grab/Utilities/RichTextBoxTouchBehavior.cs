using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Controls;

namespace Text_Grab.Utilities;

/// <summary>
/// Phone-style touch for a <see cref="RichTextBox"/>: one finger pans (and flicks) instead of selecting, a tap
/// places the caret, and a long press selects the word under the finger with draggable handles, then shows
/// the context menu. Pen and mouse keep their usual behavior.
/// </summary>
/// <remarks>
/// WPF promotes an unhandled touch to the mouse, and the text editor reads that as a selection drag (and
/// Windows turns a press-and-hold into a right click). Touch capture still participates in WPF manipulation
/// to suppress promoted mouse input, but <see cref="SmoothScrollBehavior"/> leaves movement and inertia to
/// this controller. Native touch right-click and flick gestures are handled separately so pen settings stay unchanged.
/// </remarks>
public static class RichTextBoxTouchBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(RichTextBoxTouchBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    /// <summary>
    /// Raised on the text box after a tap has placed the caret, with the tap point in the
    /// <see cref="RoutedEventArgs.Source"/>'s coordinates, so the host can show what a hover would have.
    /// </summary>
    public static readonly RoutedEvent TappedEvent = EventManager.RegisterRoutedEvent(
        "Tapped", RoutingStrategy.Bubble, typeof(TouchTappedEventHandler), typeof(RichTextBoxTouchBehavior));

    public static void AddTappedHandler(DependencyObject element, TouchTappedEventHandler handler) =>
        (element as UIElement)?.AddHandler(TappedEvent, handler);

    public static void RemoveTappedHandler(DependencyObject element, TouchTappedEventHandler handler) =>
        (element as UIElement)?.RemoveHandler(TappedEvent, handler);

    private static readonly DependencyProperty ControllerProperty = DependencyProperty.RegisterAttached(
        "Controller", typeof(RichTextBoxTouchController), typeof(RichTextBoxTouchBehavior));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox box)
            return;

        (box.GetValue(ControllerProperty) as RichTextBoxTouchController)?.Detach();
        box.ClearValue(ControllerProperty);

        if ((bool)e.NewValue)
            box.SetValue(ControllerProperty, new RichTextBoxTouchController(box));
    }
}

public delegate void TouchTappedEventHandler(object sender, TouchTappedEventArgs e);

public sealed class TouchTappedEventArgs(RoutedEvent routedEvent, object source, Point position)
    : RoutedEventArgs(routedEvent, source)
{
    public Point Position { get; } = position;
}

internal sealed class RichTextBoxTouchController
{
    // Flick inertia: velocity decays exponentially with this time constant, so a flick
    // coasts v * 325 ms of distance and comes to rest in about a second.
    private const double FlingTimeConstantMs = 325;
    private const double MinFlingVelocity = 0.1;   // DIPs/ms; a slower release just stops
    private const double MaxFlingVelocity = 8;
    private const double FlingStopVelocity = 0.02;

    // Dragging a selection end into this band at the top or bottom scrolls the text,
    // faster the deeper the finger goes.
    private const double AutoScrollBand = 40;
    private const double AutoScrollMaxSpeed = 1.2; // DIPs/ms

    private readonly RichTextBox box;
    private readonly TouchGestureTracker gesture = new();
    private readonly DispatcherTimer holdTimer;
    private readonly Stopwatch clock = Stopwatch.StartNew();

    // the finger on the text, and what it went down on
    private TouchDevice? textTouch;
    private DependencyObject? touchTarget;
    // more fingers while one is busy: swallowed, since no gesture here uses them
    private readonly HashSet<TouchDevice> extraTouches = [];
    // the touch landed on a moving fling, and only stops it
    private bool stoppedFling;

    private TouchSelectionHandlesAdorner? handles;
    private AdornerLayer? handlesLayer;
    // the selection came from touch, so it has handles
    private bool showHandles;
    // the long-pressed word, which a hold-and-drag extends from
    private TextPointer? wordStart;
    private TextPointer? wordEnd;

    private TouchDevice? handleTouch;
    // the selection end that stays put while the other handle is dragged
    private TextPointer? fixedEnd;
    // from the finger to the line the dragged handle belongs to
    private Vector grabOffset;

    // Our own record of the scroll position: ScrollViewer offsets lag until the next layout pass.
    private double offsetX;
    private double offsetY;
    private ScrollViewer? scrollViewer;

    private Vector flingVelocity;
    private bool isFlinging;
    // the finger while a selection drag may auto-scroll
    private Point? autoScrollPoint;
    // where the selection drag began; it only auto-scrolls once the finger has really moved up or down,
    // so a long press or a handle that merely starts near the edge doesn't run away
    private Point selectionDragOrigin;
    private bool isAutoScrollArmed;
    private bool isRenderHooked;
    private double lastFrameMs;

    private ContextMenu? openedMenu;

    public RichTextBoxTouchController(RichTextBox box)
    {
        this.box = box;
        holdTimer = new DispatcherTimer(DispatcherPriority.Input, box.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(TouchGestureTracker.HoldMilliseconds)
        };
        holdTimer.Tick += HoldTimer_Tick;

        box.PreviewTouchDown += Box_PreviewTouchDown;
        box.PreviewTouchMove += Box_PreviewTouchMove;
        box.PreviewTouchUp += Box_PreviewTouchUp;
        box.LostTouchCapture += Box_LostTouchCapture;
        box.PreviewStylusDown += Box_PreviewStylusDown;
        box.PreviewStylusSystemGesture += Box_PreviewStylusSystemGesture;
        box.PreviewMouseDown += Box_PreviewMouseDown;
        box.PreviewMouseWheel += Box_PreviewMouseWheel;
        box.PreviewKeyDown += Box_PreviewKeyDown;
        box.SelectionChanged += Box_SelectionChanged;
        box.IsVisibleChanged += Box_IsVisibleChanged;
        box.Unloaded += Box_Unloaded;
    }

    public void Detach()
    {
        Reset();
        holdTimer.Tick -= HoldTimer_Tick;

        box.PreviewTouchDown -= Box_PreviewTouchDown;
        box.PreviewTouchMove -= Box_PreviewTouchMove;
        box.PreviewTouchUp -= Box_PreviewTouchUp;
        box.LostTouchCapture -= Box_LostTouchCapture;
        box.PreviewStylusDown -= Box_PreviewStylusDown;
        box.PreviewStylusSystemGesture -= Box_PreviewStylusSystemGesture;
        box.PreviewMouseDown -= Box_PreviewMouseDown;
        box.PreviewMouseWheel -= Box_PreviewMouseWheel;
        box.PreviewKeyDown -= Box_PreviewKeyDown;
        box.SelectionChanged -= Box_SelectionChanged;
        box.IsVisibleChanged -= Box_IsVisibleChanged;
        box.Unloaded -= Box_Unloaded;
    }

    #region Touch on the text

    private void Box_PreviewTouchDown(object? sender, TouchEventArgs e)
    {
        if (textTouch is not null || handleTouch is not null)
        {
            // Left alone it would start a manipulation.
            extraTouches.Add(e.TouchDevice);
            e.Handled = true;
            return;
        }

        // The scroll bar keeps working the way it always has.
        if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) is not null)
        {
            StopFling();
            return;
        }

        CloseMenu();
        stoppedFling = isFlinging;
        StopFling();

        textTouch = e.TouchDevice;
        touchTarget = e.OriginalSource as DependencyObject;
        gesture.Down(e.GetTouchPoint(box).Position, e.Timestamp);
        e.TouchDevice.Capture(box);
        holdTimer.Stop();
        holdTimer.Start();
        e.Handled = true;
    }

    private void Box_PreviewTouchMove(object? sender, TouchEventArgs e)
    {
        if (e.TouchDevice != textTouch)
        {
            // Anything else not claimed here (a scroll bar drag) carries on as usual.
            if (extraTouches.Contains(e.TouchDevice))
                e.Handled = true;
            return;
        }

        e.Handled = true;
        Point position = e.GetTouchPoint(box).Position;
        TouchGestureState before = gesture.State;
        Vector delta = gesture.Move(position, e.Timestamp);

        switch (gesture.State)
        {
            case TouchGestureState.Panning:
                if (before != TouchGestureState.Panning)
                {
                    holdTimer.Stop();
                    BeginScroll();
                }

                ScrollBy(delta);
                break;

            case TouchGestureState.LongPressed:
                ExtendWordSelection(position);
                SetAutoScroll(position);
                break;
        }
    }

    private void Box_PreviewTouchUp(object? sender, TouchEventArgs e)
    {
        if (e.TouchDevice != textTouch)
        {
            if (extraTouches.Remove(e.TouchDevice))
                e.Handled = true;
            return;
        }

        e.Handled = true;
        holdTimer.Stop();
        Point position = e.GetTouchPoint(box).Position;
        DependencyObject? target = touchTarget;
        bool timerMissedLongPress = gesture.State == TouchGestureState.Pending;
        TouchGestureOutcome outcome = gesture.Up(position, e.Timestamp, out Vector velocity);

        // Cleared before releasing capture, so the LostTouchCapture this raises is ignored.
        textTouch = null;
        touchTarget = null;
        StopAutoScroll();
        e.TouchDevice.Capture(null);

        switch (outcome)
        {
            case TouchGestureOutcome.Tap:
                Tap(position, target);
                break;

            case TouchGestureOutcome.PanEnded:
                StartFling(velocity);
                break;

            case TouchGestureOutcome.LongPressEnded:
                if (timerMissedLongPress)
                    BeginLongPress(gesture.StartPosition, target);

                ShowLongPressMenu(gesture.StartPosition, target);
                break;
        }
    }

    private void Box_LostTouchCapture(object? sender, TouchEventArgs e)
    {
        if (e.TouchDevice != textTouch)
            return;

        // Taken away mid-gesture (the window lost activation): drop it. A selection already made stays.
        holdTimer.Stop();
        gesture.Cancel();
        textTouch = null;
        touchTarget = null;
        StopAutoScroll();
    }

    private void HoldTimer_Tick(object? sender, EventArgs e)
    {
        holdTimer.Stop();
        if (textTouch is not null && gesture.TryLongPress())
            BeginLongPress(gesture.StartPosition, touchTarget);
    }

    private void Tap(Point position, DependencyObject? target)
    {
        if (stoppedFling)
            return;

        // Controls hosted in the document (to-do checkboxes, Mermaid diagrams) get the click a mouse would give them.
        if (FindAncestor<ButtonBase>(target) is ButtonBase button)
        {
            Press(button);
            return;
        }

        if (GetHostedElement(target) is FrameworkElement hosted && RaiseMouseDown(hosted, target))
            return;

        if (PositionAt(position, snapToText: true) is not TextPointer caret)
            return;

        // Tapping the touch selection brings its menu back instead of dropping it.
        if (showHandles && !box.Selection.IsEmpty && box.Selection.Contains(caret))
        {
            box.Focus();
            OpenMenu(box.ContextMenu, box, GetSelectionBounds(), FindSelectedLink());
            return;
        }

        HideHandles();
        box.Focus();
        box.Selection.Select(caret, caret);
        box.RaiseEvent(new TouchTappedEventArgs(RichTextBoxTouchBehavior.TappedEvent, box, position));
    }

    private void BeginLongPress(Point position, DependencyObject? target)
    {
        wordStart = null;
        wordEnd = null;
        BeginSelectionDrag(position);

        // A hosted control with a menu of its own (a Mermaid diagram) shows that instead.
        if (GetHostedElement(target) is FrameworkElement { ContextMenu: not null })
            return;

        box.Focus();
        if (PositionAt(position, snapToText: false) is TextPointer over
            && TextPointerWords.GetWordRange(over) is (TextPointer start, TextPointer end))
        {
            wordStart = start;
            wordEnd = end;
            box.Selection.Select(start, end);
            ShowHandles();
            return;
        }

        // Not on a word: just the caret, so the menu can still paste or select all.
        HideHandles();
        if (PositionAt(position, snapToText: true) is TextPointer caret)
            box.Selection.Select(caret, caret);
    }

    private void ExtendWordSelection(Point finger)
    {
        if (wordStart is null || wordEnd is null || PositionAt(finger, snapToText: true) is not TextPointer at)
            return;

        // Grows a word at a time from the long-pressed word; the handles are for fine-tuning.
        if (at.CompareTo(wordEnd) > 0)
            box.Selection.Select(wordStart, TextPointerWords.GetWordRange(at)?.End ?? at);
        else if (at.CompareTo(wordStart) < 0)
            box.Selection.Select(wordEnd, TextPointerWords.GetWordRange(at)?.Start ?? at);
        else
            box.Selection.Select(wordStart, wordEnd);
    }

    private void ShowLongPressMenu(Point position, DependencyObject? target)
    {
        if (GetHostedElement(target) is FrameworkElement { ContextMenu: ContextMenu ownMenu } hosted)
        {
            Point inHosted = box.TranslatePoint(position, hosted);
            OpenMenu(ownMenu, hosted, new Rect(inHosted, inHosted));
            return;
        }

        Hyperlink? link = FindAncestor<Hyperlink>(target)
            ?? FindAncestor<Hyperlink>(PositionAt(position, snapToText: false)?.Parent);
        OpenMenu(box.ContextMenu, box, GetSelectionBounds(), link);
    }

    /// <summary>The link the touch selection starts or ends in, if any.</summary>
    private Hyperlink? FindSelectedLink() =>
        FindAncestor<Hyperlink>(box.Selection.Start.Parent) ?? FindAncestor<Hyperlink>(box.Selection.End.Parent);

    #endregion

    #region Selection handles

    private void ShowHandles()
    {
        showHandles = true;

        if (handles is null)
        {
            handles = new TouchSelectionHandlesAdorner(box);
            handles.PreviewTouchDown += Handles_PreviewTouchDown;
            handles.PreviewTouchMove += Handles_PreviewTouchMove;
            handles.PreviewTouchUp += Handles_PreviewTouchUp;
            handles.LostTouchCapture += Handles_LostTouchCapture;
            handles.PreviewStylusSystemGesture += Box_PreviewStylusSystemGesture;
        }

        if (handlesLayer is null)
        {
            // The window's layer rather than the one inside the scrolled content, so a knob
            // hanging below the last visible line isn't clipped off.
            handlesLayer = AdornerLayer.GetAdornerLayer(box);
            if (handlesLayer is null)
                return;

            handlesLayer.Add(handles);
            box.LayoutUpdated += Box_LayoutUpdated;
        }

        UpdateHandles();
    }

    private void HideHandles()
    {
        showHandles = false;
        if (handles is null || handlesLayer is null)
            return;

        box.LayoutUpdated -= Box_LayoutUpdated;
        handlesLayer.Remove(handles);
        handlesLayer = null;
        handles.Clear();
    }

    private void UpdateHandles()
    {
        if (handles is null || handlesLayer is null)
            return;

        if (!showHandles || box.Selection.IsEmpty)
            handles.Clear();
        else
            handles.Update(GetViewport());
    }

    // Keeps the handles on the selection through scrolling, reflow and resizing.
    private void Box_LayoutUpdated(object? sender, EventArgs e) => UpdateHandles();

    private void Box_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!showHandles)
            return;

        // Typing, a menu command or code collapsed the selection: nothing left for handles to hold.
        if (box.Selection.IsEmpty && handleTouch is null)
            HideHandles();
        else
            UpdateHandles();
    }

    private void Handles_PreviewTouchDown(object? sender, TouchEventArgs e)
    {
        // Never let a touch on a handle reach the mouse.
        e.Handled = true;
        if (handles is null || textTouch is not null || handleTouch is not null)
            return;

        Point position = e.GetTouchPoint(box).Position;
        if (handles.HitTestHandle(position) is not SelectionHandle handle)
            return;

        CloseMenu();
        StopFling();
        TextSelection selection = box.Selection;
        fixedEnd = handle == SelectionHandle.Start ? selection.End : selection.Start;
        // The finger is on the knob below the line; the selection follows the line it points at.
        grabOffset = handles.GetLinePoint(handle) - position;
        BeginSelectionDrag(position);
        handleTouch = e.TouchDevice;
        e.TouchDevice.Capture(handles);
    }

    private void Handles_PreviewTouchMove(object? sender, TouchEventArgs e)
    {
        if (e.TouchDevice != handleTouch)
            return;

        e.Handled = true;
        Point position = e.GetTouchPoint(box).Position;
        MoveHandle(position);
        SetAutoScroll(position);
    }

    private void Handles_PreviewTouchUp(object? sender, TouchEventArgs e)
    {
        if (e.TouchDevice != handleTouch)
            return;

        e.Handled = true;
        handleTouch = null;
        fixedEnd = null;
        StopAutoScroll();
        e.TouchDevice.Capture(null);

        // The selection is where the user wants it: now offer what to do with it.
        box.Focus();
        OpenMenu(box.ContextMenu, box, GetSelectionBounds(), FindSelectedLink());
    }

    private void Handles_LostTouchCapture(object? sender, TouchEventArgs e)
    {
        if (e.TouchDevice != handleTouch)
            return;

        handleTouch = null;
        fixedEnd = null;
        StopAutoScroll();
    }

    private void MoveHandle(Point finger)
    {
        if (fixedEnd is null || PositionAt(finger + grabOffset, snapToText: true) is not TextPointer moving)
            return;

        // Never collapse to nothing, or the handles would vanish from under the finger.
        // Crossing the other handle is fine: the ends swap.
        if (moving.CompareTo(fixedEnd) != 0)
            box.Selection.Select(fixedEnd, moving);
    }

    #endregion

    #region Scrolling

    private ScrollViewer? GetScrollViewer()
    {
        if (scrollViewer is null || !ReferenceEquals(scrollViewer.TemplatedParent, box))
            scrollViewer = WindowUtilities.GetScrollViewer(box);

        return scrollViewer;
    }

    /// <summary>The visible text area, in text box coordinates.</summary>
    private Rect GetViewport()
    {
        if (GetScrollViewer() is not ScrollViewer sv || !sv.IsDescendantOf(box))
            return Rect.Empty;

        return sv.TransformToAncestor(box).TransformBounds(new Rect(0, 0, sv.ViewportWidth, sv.ViewportHeight));
    }

    private void BeginScroll()
    {
        if (GetScrollViewer() is not ScrollViewer sv)
            return;

        offsetX = sv.HorizontalOffset;
        offsetY = sv.VerticalOffset;
    }

    /// <summary>Moves the text along with a finger movement; returns the axes that ran into an end.</summary>
    private PanAxes ScrollBy(Vector fingerDelta)
    {
        if (GetScrollViewer() is not ScrollViewer sv)
            return PanAxes.Both;

        PanAxes stuck = PanAxes.None;

        if (fingerDelta.Y != 0)
        {
            double wanted = offsetY - fingerDelta.Y;
            offsetY = Math.Clamp(wanted, 0, Math.Max(0, sv.ScrollableHeight));
            if (offsetY != wanted)
                stuck |= PanAxes.Vertical;
            sv.ScrollToVerticalOffset(offsetY);
        }

        if (fingerDelta.X != 0)
        {
            double wanted = offsetX - fingerDelta.X;
            offsetX = Math.Clamp(wanted, 0, Math.Max(0, sv.ScrollableWidth));
            if (offsetX != wanted)
                stuck |= PanAxes.Horizontal;
            sv.ScrollToHorizontalOffset(offsetX);
        }

        return stuck;
    }

    private void StartFling(Vector velocity)
    {
        if (velocity.Length < MinFlingVelocity)
            return;

        if (velocity.Length > MaxFlingVelocity)
            velocity *= MaxFlingVelocity / velocity.Length;

        // Coasts on from the pan's own record of where it left the text.
        flingVelocity = velocity;
        isFlinging = true;
        HookRendering();
    }

    private void StopFling()
    {
        isFlinging = false;
        flingVelocity = default;
    }

    private void BeginSelectionDrag(Point origin)
    {
        selectionDragOrigin = origin;
        isAutoScrollArmed = false;
    }

    private void SetAutoScroll(Point finger)
    {
        isAutoScrollArmed |= Math.Abs(finger.Y - selectionDragOrigin.Y) > TouchGestureTracker.Slop;
        if (!isAutoScrollArmed)
            return;

        if (autoScrollPoint is null)
            BeginScroll();

        autoScrollPoint = finger;
        HookRendering();
    }

    private void StopAutoScroll()
    {
        autoScrollPoint = null;
        isAutoScrollArmed = false;
    }

    /// <summary>DIPs/ms to scroll for a finger dragging a selection end: positive scrolls down.</summary>
    private double GetAutoScrollSpeed(Point finger)
    {
        Rect viewport = GetViewport();
        if (viewport.IsEmpty)
            return 0;

        double intoTop = viewport.Top + AutoScrollBand - finger.Y;
        if (intoTop > 0)
            return -AutoScrollMaxSpeed * Math.Min(1, intoTop / AutoScrollBand);

        double intoBottom = finger.Y - (viewport.Bottom - AutoScrollBand);
        if (intoBottom > 0)
            return AutoScrollMaxSpeed * Math.Min(1, intoBottom / AutoScrollBand);

        return 0;
    }

    private void HookRendering()
    {
        if (isRenderHooked)
            return;

        isRenderHooked = true;
        lastFrameMs = clock.Elapsed.TotalMilliseconds;
        CompositionTarget.Rendering += CompositionTarget_Rendering;
    }

    private void UnhookRendering()
    {
        if (!isRenderHooked)
            return;

        isRenderHooked = false;
        CompositionTarget.Rendering -= CompositionTarget_Rendering;
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        double now = clock.Elapsed.TotalMilliseconds;
        double dt = Math.Clamp(now - lastFrameMs, 1, 50);
        lastFrameMs = now;

        if (isFlinging)
        {
            // Exact distance covered by an exponentially decaying velocity over dt.
            double decay = Math.Exp(-dt / FlingTimeConstantMs);
            Vector step = flingVelocity * (FlingTimeConstantMs * (1 - decay));
            flingVelocity *= decay;

            PanAxes stuck = ScrollBy(step);
            if (stuck.HasFlag(PanAxes.Vertical))
                flingVelocity.Y = 0;
            if (stuck.HasFlag(PanAxes.Horizontal))
                flingVelocity.X = 0;

            if (flingVelocity.Length < FlingStopVelocity)
                StopFling();
        }

        if (autoScrollPoint is Point finger && GetAutoScrollSpeed(finger) is double speed and not 0)
        {
            ScrollBy(new Vector(0, -speed * dt));

            // The text moved under a still finger, so the selection end moves with it.
            box.UpdateLayout();
            if (handleTouch is not null)
                MoveHandle(finger);
            else if (gesture.State == TouchGestureState.LongPressed)
                ExtendWordSelection(finger);
        }

        if (!isFlinging && autoScrollPoint is null)
            UnhookRendering();
    }

    #endregion

    #region Other input takes over

    private void Box_PreviewStylusDown(object sender, StylusDownEventArgs e)
    {
        if (e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus)
            return;

        StopFling();
        HideHandles();
    }

    private void Box_PreviewStylusSystemGesture(object sender, StylusSystemGestureEventArgs e)
    {
        // Native right-clicks and flicks can otherwise compete with our touch gestures.
        if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch
            && e.SystemGesture is SystemGesture.RightTap or SystemGesture.RightDrag or SystemGesture.Flick
            && FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) is null)
        {
            e.Handled = true;
        }
    }

    private void Box_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Mouse and pen take over; promoted touch belongs to the touch handlers above.
        if (e.StylusDevice?.TabletDevice.Type == TabletDeviceType.Touch)
            return;

        StopFling();
        HideHandles();
    }

    private void Box_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => StopFling();

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        StopFling();
        HideHandles();
    }

    private void Box_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!box.IsVisible)
            Reset();
    }

    private void Box_Unloaded(object sender, RoutedEventArgs e) => Reset();

    private void Reset()
    {
        holdTimer.Stop();
        gesture.Cancel();
        textTouch = null;
        touchTarget = null;
        extraTouches.Clear();
        handleTouch = null;
        fixedEnd = null;
        StopFling();
        StopAutoScroll();
        UnhookRendering();
        CloseMenu();
        HideHandles();
    }

    #endregion

    #region Menu

    /// <param name="link">
    /// A link the touch is on. Touch has no Ctrl+click, so its menu gets an item to follow the link.
    /// </param>
    private void OpenMenu(ContextMenu? menu, UIElement owner, Rect area, Hyperlink? link = null)
    {
        if (menu is null)
            return;

        CloseMenu();
        if (link is not null)
            AddLinkItems(menu, link);

        // Above what it acts on, so it doesn't cover the selection; Popup flips it below near the top of the screen.
        menu.PlacementTarget = owner;
        menu.Placement = PlacementMode.Top;
        menu.PlacementRectangle = area;
        menu.Closed += Menu_Closed;
        openedMenu = menu;
        menu.IsOpen = true;
    }

    private void Menu_Closed(object sender, RoutedEventArgs e)
    {
        ContextMenu menu = (ContextMenu)sender;
        menu.Closed -= Menu_Closed;

        // Hand the menu back as it was for right clicks.
        menu.ClearValue(ContextMenu.PlacementTargetProperty);
        menu.ClearValue(ContextMenu.PlacementProperty);
        menu.ClearValue(ContextMenu.PlacementRectangleProperty);
        RemoveLinkItems(menu);

        if (ReferenceEquals(openedMenu, menu))
            openedMenu = null;
    }

    private void AddLinkItems(ContextMenu menu, Hyperlink link)
    {
        // A footnote reference has no address, just a click that jumps to the note; its tooltip says where.
        MenuItem open = new()
        {
            Header = link.NavigateUri is null && link.ToolTip is string jump ? jump : "Open link",
            ToolTip = link.NavigateUri?.OriginalString,
            Tag = LinkItemTag,
        };
        open.Click += (_, _) => FollowLink(link);
        menu.Items.Insert(0, open);
        menu.Items.Insert(1, new Separator { Tag = LinkItemTag });
    }

    private static void RemoveLinkItems(ContextMenu menu)
    {
        for (int i = menu.Items.Count - 1; i >= 0; i--)
        {
            if (menu.Items[i] is FrameworkElement { Tag: string tag } && ReferenceEquals(tag, LinkItemTag))
                menu.Items.RemoveAt(i);
        }
    }

    private const string LinkItemTag = "TouchLinkItem";

    /// <summary>Does what clicking the link does: its Click handlers, then navigation to its address.</summary>
    internal static void FollowLink(Hyperlink link)
    {
        try
        {
            if (ContentElementAutomationPeer.CreatePeerForElement(link)?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
            {
                invoke.Invoke();
                return;
            }
        }
        catch (System.Windows.Automation.ElementNotEnabledException)
        {
        }

        if (link.NavigateUri is Uri uri)
            link.RaiseEvent(new System.Windows.Navigation.RequestNavigateEventArgs(uri, link.TargetName));
    }

    private void CloseMenu()
    {
        if (openedMenu is { IsOpen: true } menu)
            menu.IsOpen = false;
    }

    /// <summary>The visible part of the selection, in text box coordinates.</summary>
    private Rect GetSelectionBounds()
    {
        TextSelection selection = box.Selection;
        Rect bounds = selection.Start.GetCharacterRect(LogicalDirection.Forward);
        bounds.Union(selection.End.GetCharacterRect(LogicalDirection.Backward));

        Rect viewport = GetViewport();
        if (viewport.IsEmpty)
            return bounds;

        Rect visible = Rect.Intersect(bounds, viewport);
        return visible.IsEmpty
            ? new Rect(viewport.Left + viewport.Width / 2, viewport.Top + viewport.Height / 2, 0, 0)
            : visible;
    }

    #endregion

    #region Hit testing

    private TextPointer? PositionAt(Point position, bool snapToText)
    {
        try
        {
            return box.GetPositionFromPoint(position, snapToText);
        }
        catch (InvalidOperationException)
        {
            // No valid layout to hit-test against right now.
            return null;
        }
    }

    private T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        for (DependencyObject? current = start; current is not null && current != box; current = GetParent(current))
        {
            if (current is T match)
                return match;
        }

        return null;
    }

    /// <summary>The control hosted in the document (in an InlineUIContainer or BlockUIContainer) that holds <paramref name="start"/>.</summary>
    private FrameworkElement? GetHostedElement(DependencyObject? start)
    {
        for (DependencyObject? current = start; current is not null && current != box; current = GetParent(current))
        {
            if (current is FrameworkElement { Parent: InlineUIContainer or BlockUIContainer } hosted)
                return hosted;
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject element) =>
        element is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element)
            : LogicalTreeHelper.GetParent(element);

    private static void Press(ButtonBase button)
    {
        if (!button.IsEnabled)
            return;

        AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(button);
        if (peer?.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle)
            toggle.Toggle();
        else if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
            invoke.Invoke();
    }

    /// <summary>
    /// Raises the left mouse down a real click would, tunnelling from the hosted control down to what the finger
    /// touched (and no further, so the text box doesn't start a selection). True when something acted on it.
    /// </summary>
    private static bool RaiseMouseDown(FrameworkElement hosted, DependencyObject? target)
    {
        List<UIElement> route = [];
        for (DependencyObject? current = target; current is not null; current = GetParent(current))
        {
            if (current is UIElement element)
                route.Insert(0, element);

            if (current == hosted)
                break;
        }

        foreach (UIElement element in route)
        {
            MouseButtonEventArgs args = new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent
            };
            element.RaiseEvent(args);
            if (args.Handled)
                return true;
        }

        return false;
    }

    #endregion
}
