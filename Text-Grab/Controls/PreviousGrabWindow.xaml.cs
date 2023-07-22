using System;
using System.Windows;
using System.Windows.Threading;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for PreviousGrabWindow.xaml
/// </summary>
public partial class PreviousGrabWindow : Window
{
    public PreviousGrabWindow(Rect rect)
    {
        InitializeComponent();

        int borderThickness = 3;

        Width = rect.Width + (2 * borderThickness);
        Height = rect.Height + (2 * borderThickness);
        Left = rect.Left - borderThickness;
        Top = rect.Top - borderThickness;

        DispatcherTimer timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.Tick += (s, e) => { timer.Stop(); Close(); };
        timer.Start();
    }
}
