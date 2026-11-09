using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Text_Grab.Controls;

/// <summary>
/// Full-screen modal that shows a rendered Mermaid diagram with mouse-wheel zoom and drag panning.
/// Right-click offers the same save options as the inline diagram.
/// </summary>
internal sealed class MermaidViewerWindow : Window
{
    private readonly ZoomBorder zoomBorder;

    internal MermaidViewerWindow(MermaidDiagram diagram)
    {
        bool light = diagram.IsLightTheme;
        Brush background = new SolidColorBrush(light ? Color.FromRgb(0xf5, 0xf5, 0xf5) : Color.FromRgb(0x20, 0x20, 0x24));
        Brush foreground = new SolidColorBrush(light ? Color.FromRgb(0x20, 0x20, 0x24) : Color.FromRgb(0xee, 0xee, 0xee));

        Title = "Mermaid diagram";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = background;

        // Cover the owning window rather than the whole screen.
        Window? owner = Window.GetWindow(diagram);
        if (owner is not null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = owner.Left;
            Top = owner.Top;
            Width = owner.ActualWidth;
            Height = owner.ActualHeight;
            if (owner.WindowState == WindowState.Maximized)
                WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Maximized;
        }

        Image image = new()
        {
            Source = diagram.RenderedBitmap,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both,
            Margin = new Thickness(32),
            SnapsToDevicePixels = true,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        zoomBorder = new ZoomBorder { ClipToBounds = true, Child = image, ContextMenu = diagram.CreateContextMenu() };
        zoomBorder.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
                zoomBorder.Reset();
        };

        TextBlock hint = new()
        {
            Text = "Scroll to zoom · drag to pan · double-click to reset · right-click to save · Esc to close",
            Foreground = foreground,
            Opacity = 0.7,
            Margin = new Thickness(16, 10, 16, 10),
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false,
        };

        Button close = new()
        {
            Content = "✕",
            Width = 36,
            Height = 36,
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = "Close (Esc)",
        };
        close.Click += (_, _) => Close();
        System.Windows.Automation.AutomationProperties.SetName(close, "Close diagram viewer");
        System.Windows.Automation.AutomationProperties.SetName(zoomBorder, "Mermaid diagram viewer");

        Grid root = new();
        root.Children.Add(zoomBorder);
        root.Children.Add(hint);
        root.Children.Add(close);
        Content = root;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
            else if (e.Key is Key.D0 or Key.NumPad0 && Keyboard.Modifiers == ModifierKeys.Control)
            {
                zoomBorder.Reset();
                e.Handled = true;
            }
        };
    }
}
