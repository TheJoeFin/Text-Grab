using System;
using System.Windows;
using System.Windows.Threading;

namespace Text_Grab.Controls;

/// <summary>
/// The visual state shown inside the <see cref="PreviousGrabWindow"/> border overlay.
/// </summary>
public enum PreviousGrabIndicator
{
    /// <summary>Only the border flashes briefly.</summary>
    None,

    /// <summary>A checkmark icon is shown briefly to indicate a successful grab.</summary>
    Success,

    /// <summary>A spinner is shown until the caller invokes <see cref="PreviousGrabWindow.ShowSuccess"/> or closes the window.</summary>
    Loading,
}

/// <summary>
/// Interaction logic for PreviousGrabWindow.xaml
/// </summary>
public partial class PreviousGrabWindow : Window
{
    private static readonly TimeSpan flashDuration = TimeSpan.FromMilliseconds(300);

    public PreviousGrabWindow(Rect rect, PreviousGrabIndicator indicator = PreviousGrabIndicator.None)
    {
        InitializeComponent();

        int borderThickness = 3;

        Width = rect.Width + (2 * borderThickness);
        Height = rect.Height + (2 * borderThickness);
        Left = rect.Left - borderThickness;
        Top = rect.Top - borderThickness;

        switch (indicator)
        {
            case PreviousGrabIndicator.Success:
                SuccessViewbox.Visibility = Visibility.Visible;
                CloseAfterDelay();
                break;
            case PreviousGrabIndicator.Loading:
                LoadingViewbox.Visibility = Visibility.Visible;
                break;
            case PreviousGrabIndicator.None:
            default:
                CloseAfterDelay();
                break;
        }
    }

    /// <summary>
    /// Swaps the loading spinner for the success checkmark, then closes shortly after.
    /// </summary>
    public void ShowSuccess()
    {
        LoadingViewbox.Visibility = Visibility.Collapsed;
        SuccessViewbox.Visibility = Visibility.Visible;
        CloseAfterDelay();
    }

    private void CloseAfterDelay()
    {
        DispatcherTimer timer = new()
        {
            Interval = flashDuration
        };
        timer.Tick += (s, e) => { timer.Stop(); Close(); };
        timer.Start();
    }
}
