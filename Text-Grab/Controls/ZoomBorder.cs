using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

// From StackOverFlow: 
// https://stackoverflow.com/questions/741956/pan-zoom-image
// Answered by https://stackoverflow.com/users/282801/wies%c5%82aw-%c5%a0olt%c3%a9s
// Read on 2024-05-02
// Modified to match code style of this project

namespace Text_Grab.Controls;

public class ZoomBorder : Border
{
    private UIElement? child = null;
    private Point origin;
    private Point start;

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

    public bool CanPan { get; set; } = true;

    public bool CanZoom { get; set; } = true;

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
        MouseLeftButtonDown += Child_MouseLeftButtonDown;
        MouseLeftButtonUp += Child_MouseLeftButtonUp;
        PreviewMouseDown += ZoomBorder_PreviewMouseDown;
        MouseMove += Child_MouseMove;
        PreviewMouseRightButtonDown += new MouseButtonEventHandler(
          Child_PreviewMouseRightButtonDown);
    }

    private void ZoomBorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
            Reset();
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
    }

    private void Child_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (child is null)
            return;

        TranslateTransform tt = GetTranslateTransform(child);
        start = e.GetPosition(this);
        origin = new Point(tt.X, tt.Y);
        Cursor = Cursors.Hand;
        // child.CaptureMouse();
    }

    private void Child_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (child is null)
            return;

        child.ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
    }

    void Child_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
    }

    private void Child_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource is TextBox)
            return;

        if (child is null
            || GetScaleTransform(child) is not ScaleTransform st
            || st.ScaleX == 1.0
            || Mouse.LeftButton == MouseButtonState.Released
            || !CanPan
            || KeyboardExtensions.IsShiftDown()
            || KeyboardExtensions.IsCtrlDown())
        {
            child?.ReleaseMouseCapture();
            return;
        }

        TranslateTransform tt = GetTranslateTransform(child);
        Vector v = start - e.GetPosition(this);
        tt.X = origin.X - v.X;
        tt.Y = origin.Y - v.Y;
    }
}