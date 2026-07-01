using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

// From StackOverFlow:
// https://stackoverflow.com/questions/741956/pan-zoom-image
// Answered by https://stackoverflow.com/users/282801/wies%c5%82aw-%c5%a0olt%c3%a9s
// Read on 2024-05-02
// Modified to match code style of this project

namespace Text_Grab.Controls;

public class ZoomBorder : Border
{
    private UIElement? child = null;
    private bool isPanning = false;
    private Point origin;
    private Point start;

    public event EventHandler? ResetRequested;

    public ZoomBorder()
    {
        Background = Brushes.Transparent;
    }

    private TranslateTransform GetTranslateTransform(UIElement element) =>
        (TranslateTransform)((TransformGroup)element.RenderTransform)
          .Children.First(tr => tr is TranslateTransform);

    private ScaleTransform GetScaleTransform(UIElement element) =>
        (ScaleTransform)((TransformGroup)element.RenderTransform)
          .Children.First(tr => tr is ScaleTransform);

    public override UIElement Child
    {
        get { return base.Child; }
        set
        {
            if (value != null && value != Child)
                Initialize(value);
            base.Child = value;
        }
    }

    public bool CanPan { get; set; } = false;

    public bool CanZoom { get; set; } = true;

    public bool IsSpacePanModifierPressed { get; set; } = false;

    public bool RequireSpaceToPan { get; set; } = false;

    public double GetScale()
    {
        if (child is null)
            return 1.0;

        return GetScaleTransform(child).ScaleX;
    }

    public void Initialize(UIElement element)
    {
        child = element;
        if (child is null)
            return;

        TransformGroup group = new();
        ScaleTransform st = new();
        group.Children.Add(st);
        TranslateTransform tt = new();
        group.Children.Add(tt);
        child.RenderTransform = group;
        child.RenderTransformOrigin = new Point(0.0, 0.0);
        MouseWheel += Child_MouseWheel;
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Child_PreviewMouseDown), true);
        AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(Child_PreviewMouseUp), true);
        AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(Child_MouseMove), true);
    }

    public void Reset()
    {
        if (child is null)
            return;

        // reset zoom
        ScaleTransform st = GetScaleTransform(child);
        st.ScaleX = 1.0;
        st.ScaleY = 1.0;

        // reset pan
        TranslateTransform tt = GetTranslateTransform(child);
        tt.X = 0.0;
        tt.Y = 0.0;

        isPanning = false;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        CanPan = false;
    }

    public void SetScale(double scale)
    {
        if (child is null)
            return;

        if (!double.IsFinite(scale) || scale <= 0)
            scale = 1.0;

        ScaleTransform st = GetScaleTransform(child);
        TranslateTransform tt = GetTranslateTransform(child);

        st.ScaleX = scale;
        st.ScaleY = scale;

        double childWidth = child.RenderSize.Width > 0 ? child.RenderSize.Width : ActualWidth;
        double childHeight = child.RenderSize.Height > 0 ? child.RenderSize.Height : ActualHeight;

        if (double.IsFinite(childWidth) && childWidth > 0)
            tt.X = ((childWidth * scale) - childWidth) * -0.5;
        else
            tt.X = 0;

        if (double.IsFinite(childHeight) && childHeight > 0)
            tt.Y = ((childHeight * scale) - childHeight) * -0.5;
        else
            tt.Y = 0;

        isPanning = false;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        CanPan = scale > 1.0;
    }

    private bool IsPanGestureActive() =>
        !RequireSpaceToPan || IsSpacePanModifierPressed || Keyboard.IsKeyDown(Key.Space);

    private bool BlocksPanFromSource(object? originalSource)
    {
        DependencyObject? current = originalSource switch
        {
            DependencyObject dependencyObject => dependencyObject,
            null => null,
            _ => null
        };

        while (current is not null)
        {
            if (current is TextBox)
                return true;

            if (current is PdfTextLineOverlay)
                return !IsPanGestureActive();

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => null
            };
        }

        return false;
    }

    private void Child_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (child is null || !CanZoom)
            return;

        ScaleTransform st = GetScaleTransform(child);
        TranslateTransform tt = GetTranslateTransform(child);

        double zoom = e.Delta > 0 ? .2 : -.2;
        if (!(e.Delta > 0) && (st.ScaleX < .4 || st.ScaleY < .4))
            return;

        Point relative = e.GetPosition(child);
        double absoluteX;
        double absoluteY;

        absoluteX = relative.X * st.ScaleX + tt.X;
        absoluteY = relative.Y * st.ScaleY + tt.Y;

        st.ScaleX += zoom;
        st.ScaleY += zoom;

        tt.X = absoluteX - relative.X * st.ScaleX;
        tt.Y = absoluteY - relative.Y * st.ScaleY;

        CanPan = true;
    }

    private void Child_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            ResetRequested?.Invoke(this, EventArgs.Empty);
            Reset();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
            return;

        if (child is null
            || GetScaleTransform(child) is not ScaleTransform st
            || st.ScaleX == 1.0
            || !CanPan
            || !IsPanGestureActive()
            || BlocksPanFromSource(e.OriginalSource))
        {
            return;
        }

        TranslateTransform tt = GetTranslateTransform(child);
        start = e.GetPosition(this);
        origin = new Point(tt.X, tt.Y);

        bool captured = CaptureMouse();
        if (!captured)
            return;

        isPanning = true;
        Cursor = Cursors.Hand;
        e.Handled = true;
    }

    private void Child_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || child is null || !isPanning)
            return;

        isPanning = false;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    private void Child_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isPanning && BlocksPanFromSource(e.OriginalSource))
            return;

        if (child is null
            || GetScaleTransform(child) is not ScaleTransform st
            || st.ScaleX == 1.0
            || !isPanning
            || !CanPan
            || KeyboardExtensions.IsShiftDown()
            || KeyboardExtensions.IsCtrlDown())
        {
            isPanning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            return;
        }

        TranslateTransform tt = GetTranslateTransform(child);
        Vector v = start - e.GetPosition(this);
        tt.X = origin.X - v.X;
        tt.Y = origin.Y - v.Y;
        e.Handled = true;
    }
}
