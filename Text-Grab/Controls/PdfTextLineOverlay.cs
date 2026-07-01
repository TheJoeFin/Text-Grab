using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Text_Grab.Utilities;

namespace Text_Grab.Controls;

internal sealed class PdfTextLineOverlay : Border
{
    private static readonly Brush DefaultBorderBrush = new SolidColorBrush(Color.FromArgb(0x90, 0x00, 0x78, 0xD7));
    private static readonly Brush DefaultHighlightBrush = new SolidColorBrush(Color.FromArgb(0x50, 0x00, 0x78, 0xD7));
    private static readonly Brush TransparentTextBrush = new SolidColorBrush(Colors.Transparent);

    public PdfTextLineOverlay(string text)
    {
        Text = text;
        Child = new TextBlock
        {
            Text = text,
            Foreground = TransparentTextBrush,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(1, 0, 1, 0),
            IsHitTestVisible = false
        };

        Background = Brushes.Transparent;
        BorderBrush = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        ClipToBounds = true;
        IsHitTestVisible = true;
        SnapsToDevicePixels = true;
    }

    public bool IsSelected { get; private set; }

    public double Left
    {
        get => Canvas.GetLeft(this);
        private set => Canvas.SetLeft(this, value);
    }

    public double Top
    {
        get => Canvas.GetTop(this);
        private set => Canvas.SetTop(this, value);
    }

    public string Text { get; }

    public bool WasRegionSelected { get; set; }

    public void ApplyLayout(Rect bounds)
    {
        Width = Math.Max(1, bounds.Width + 2);
        Height = Math.Max(1, bounds.Height + 2);
        Left = Math.Max(0, bounds.X - 1);
        Top = Math.Max(0, bounds.Y - 1);

        if (Child is TextBlock textBlock)
        {
            textBlock.FontSize = Math.Max(1, bounds.Height * 0.75);
            textBlock.LineHeight = Math.Max(1, bounds.Height);
        }
    }

    public void Deselect()
    {
        IsSelected = false;
        Background = Brushes.Transparent;
        BorderBrush = Brushes.Transparent;
        BorderThickness = new Thickness(0);
    }

    public bool IntersectsWith(Rect rectToCheck)
    {
        Rect overlayRect = new(Left, Top, Width, Height);
        return rectToCheck.IntersectsWith(overlayRect);
    }

    public void Select()
    {
        IsSelected = true;
        Background = DefaultHighlightBrush;
        BorderBrush = DefaultBorderBrush;
        BorderThickness = new Thickness(1);
    }
}
