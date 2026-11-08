using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Text_Grab.Utilities;

/// <summary>
/// Adds eased mouse wheel scrolling and touch panning (with flick inertia) to controls that
/// host a pixel-scrolling ScrollViewer, such as TextBox and RichTextBox.
/// Uses its own per-frame loop and tracks its own position, because ScrollViewer.VerticalOffset
/// lags behind until the next layout pass and would make animations restart from a stale value.
/// </summary>
public static class SmoothScrollBehavior
{
    // Time constant of the exponential ease; ~4x this is the time to settle.
    private const double EaseTimeConstantSeconds = 0.055;
    // Touch flick deceleration in DIPs per ms^2 (WPF default is 0.001; higher stops sooner).
    private const double FlickDeceleration = 0.0015;

    private sealed class ScrollState
    {
        public double Current;
        public double Target;
        public bool IsAnimating;

        // touch manipulation
        public bool IsPanning;
        public double HorizontalCurrent;
    }

    private static readonly ConditionalWeakTable<ScrollViewer, ScrollState> States = [];
    private static readonly List<ScrollViewer> Active = [];
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static double lastFrameSeconds;
    private static bool isRenderHooked;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(SmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        element.PreviewMouseWheel -= Element_PreviewMouseWheel;
        element.ManipulationStarting -= Element_ManipulationStarting;
        element.ManipulationStarted -= Element_ManipulationStarted;
        element.ManipulationInertiaStarting -= Element_ManipulationInertiaStarting;
        element.ManipulationDelta -= Element_ManipulationDelta;
        element.ManipulationCompleted -= Element_ManipulationCompleted;
        element.PreviewTouchMove -= Element_PreviewTouchMove;

        bool enabled = (bool)e.NewValue;
        element.IsManipulationEnabled = enabled;

        if (enabled)
        {
            element.PreviewMouseWheel += Element_PreviewMouseWheel;
            element.ManipulationStarting += Element_ManipulationStarting;
            element.ManipulationStarted += Element_ManipulationStarted;
            element.ManipulationInertiaStarting += Element_ManipulationInertiaStarting;
            element.ManipulationDelta += Element_ManipulationDelta;
            element.ManipulationCompleted += Element_ManipulationCompleted;
            element.PreviewTouchMove += Element_PreviewTouchMove;
        }
    }

    #region Wheel

    private static void Element_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Ctrl = zoom, Shift = leave to default handling
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (sender is Control control && ScrollBy(control, e.Delta))
            e.Handled = true;
    }

    /// <summary>
    /// Smoothly scrolls the ScrollViewer inside <paramref name="owner"/> by a mouse wheel delta.
    /// Returns false when nothing could be scrolled (so the event can bubble).
    /// </summary>
    public static bool ScrollBy(Control owner, int wheelDelta)
    {
        ScrollViewer? sv = WindowUtilities.GetScrollViewer(owner);
        if (sv is null || sv.ScrollableHeight <= 0)
            return false;

        ScrollState state = States.GetOrCreateValue(sv);
        double basis = state.IsAnimating ? state.Target : sv.VerticalOffset;

        double lineHeight = Math.Max(12, owner.FontSize * owner.FontFamily.LineSpacing);
        double pixels = -wheelDelta / 120.0 * SystemParameters.WheelScrollLines * lineHeight;
        double target = Math.Clamp(basis + pixels, 0, sv.ScrollableHeight);

        // At an edge and pushing further: let the event bubble.
        if (!state.IsAnimating && Math.Abs(target - sv.VerticalOffset) < 0.5)
            return false;

        if (!state.IsAnimating)
            state.Current = sv.VerticalOffset;

        state.Target = target;
        StartAnimating(sv, state);
        return true;
    }

    #endregion

    #region Touch

    // Touch panning uses WPF's manipulation events, which give us thresholding and inertia for free.

    private static void Element_ManipulationStarting(object? sender, ManipulationStartingEventArgs e)
    {
        if (sender is not UIElement element)
            return;

        e.ManipulationContainer = element;
        e.Mode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
        e.Handled = true;
    }

    private static void Element_ManipulationStarted(object? sender, ManipulationStartedEventArgs e)
    {
        if (sender is not Control control || WindowUtilities.GetScrollViewer(control) is not ScrollViewer sv)
            return;

        ScrollState state = States.GetOrCreateValue(sv);

        // A new touch stops any coasting from the wheel or a previous flick
        if (state.IsAnimating)
            StopAnimating(sv, state);

        state.IsPanning = true;
        state.Current = sv.VerticalOffset;
        state.HorizontalCurrent = sv.HorizontalOffset;
    }

    private static void Element_ManipulationInertiaStarting(object? sender, ManipulationInertiaStartingEventArgs e)
    {
        e.TranslationBehavior = new InertiaTranslationBehavior
        {
            InitialVelocity = e.InitialVelocities.LinearVelocity,
            DesiredDeceleration = FlickDeceleration,
        };
        e.Handled = true;
    }

    private static void Element_ManipulationDelta(object? sender, ManipulationDeltaEventArgs e)
    {
        if (sender is not Control control || WindowUtilities.GetScrollViewer(control) is not ScrollViewer sv)
            return;

        ScrollState state = States.GetOrCreateValue(sv);
        Vector translation = e.DeltaManipulation.Translation;
        bool hitEdge = false;

        if (sv.ScrollableHeight > 0 && translation.Y != 0)
        {
            double wanted = state.Current - translation.Y;
            state.Current = Math.Clamp(wanted, 0, sv.ScrollableHeight);
            hitEdge |= wanted != state.Current;
            sv.ScrollToVerticalOffset(state.Current);
        }

        if (sv.ScrollableWidth > 0 && translation.X != 0)
        {
            double wanted = state.HorizontalCurrent - translation.X;
            state.HorizontalCurrent = Math.Clamp(wanted, 0, sv.ScrollableWidth);
            hitEdge |= wanted != state.HorizontalCurrent;
            sv.ScrollToHorizontalOffset(state.HorizontalCurrent);
        }

        // Stop the coast once we run into the end of the document
        if (e.IsInertial && hitEdge)
            e.Complete();

        e.Handled = true;
    }

    private static void Element_ManipulationCompleted(object? sender, ManipulationCompletedEventArgs e)
    {
        if (sender is Control control
            && WindowUtilities.GetScrollViewer(control) is ScrollViewer sv
            && States.TryGetValue(sv, out ScrollState? state))
        {
            state.IsPanning = false;
        }
    }

    private static void Element_PreviewTouchMove(object? sender, TouchEventArgs e)
    {
        // Once a pan is under way, keep the promoted mouse-move from extending a text selection
        if (sender is Control control
            && WindowUtilities.GetScrollViewer(control) is ScrollViewer sv
            && States.TryGetValue(sv, out ScrollState? state)
            && state.IsPanning)
        {
            e.Handled = true;
        }
    }

    #endregion

    #region Animation loop

    private static void StartAnimating(ScrollViewer sv, ScrollState state)
    {
        if (!state.IsAnimating)
        {
            state.IsAnimating = true;
            Active.Add(sv);
        }

        if (!isRenderHooked)
        {
            isRenderHooked = true;
            lastFrameSeconds = Clock.Elapsed.TotalSeconds;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }
    }

    private static void StopAnimating(ScrollViewer sv, ScrollState state)
    {
        state.IsAnimating = false;
        Active.Remove(sv);
    }

    private static void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        double now = Clock.Elapsed.TotalSeconds;
        double dt = Math.Clamp(now - lastFrameSeconds, 0.001, 0.1);
        lastFrameSeconds = now;

        double blend = 1 - Math.Exp(-dt / EaseTimeConstantSeconds);

        for (int i = Active.Count - 1; i >= 0; i--)
        {
            ScrollViewer sv = Active[i];
            if (!States.TryGetValue(sv, out ScrollState? state))
            {
                Active.RemoveAt(i);
                continue;
            }

            state.Target = Math.Clamp(state.Target, 0, sv.ScrollableHeight);
            state.Current += (state.Target - state.Current) * blend;

            if (Math.Abs(state.Target - state.Current) < 0.5)
            {
                state.Current = state.Target;
                state.IsAnimating = false;
                Active.RemoveAt(i);
            }

            sv.ScrollToVerticalOffset(state.Current);
        }

        if (Active.Count == 0)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            isRenderHooked = false;
        }
    }

    #endregion
}
