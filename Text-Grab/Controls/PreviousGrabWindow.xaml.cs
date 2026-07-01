using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
/// The action a user picked from the overlay while a long-running grab was in progress or after it failed.
/// </summary>
public enum GrabChoice
{
    /// <summary>No choice was made.</summary>
    None,

    /// <summary>Abandon the grab entirely.</summary>
    Cancel,

    /// <summary>Cancel the current grab and return to selecting a new region.</summary>
    ReGrab,

    /// <summary>Open a Grab Frame using the originally captured region image.</summary>
    SendToGrabFrame,
}

/// <summary>
/// Interaction logic for PreviousGrabWindow.xaml
/// </summary>
public partial class PreviousGrabWindow : Window
{
    private static readonly TimeSpan flashDuration = TimeSpan.FromMilliseconds(300);
    private static readonly Duration choiceBarSlideDuration = new(TimeSpan.FromMilliseconds(250));
    private const double choiceBarSlideDistance = 48;

    public PreviousGrabWindow(Rect rect, PreviousGrabIndicator indicator = PreviousGrabIndicator.None, ImageSource? regionBackground = null)
    {
        InitializeComponent();

        int borderThickness = 3;

        Width = rect.Width + (2 * borderThickness);
        Height = rect.Height + (2 * borderThickness);
        Left = rect.Left - borderThickness;
        Top = rect.Top - borderThickness;

        // When supplied, freeze a snapshot of the selected region as the overlay's
        // background so the user's selection stays visible even if the UI behind it
        // changes while a long-running grab (e.g. Windows AI description) is working.
        if (regionBackground is not null)
        {
            RegionBackgroundImage.Source = regionBackground;
            RegionBackgroundImage.Visibility = Visibility.Visible;
        }

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
    /// Raised when the user picks an action from the overlay's choice bar.
    /// </summary>
    public event EventHandler<GrabChoice>? ChoiceSelected;

    /// <summary>
    /// Swaps the loading spinner for the success checkmark, then closes shortly after.
    /// </summary>
    public void ShowSuccess()
    {
        HideChoiceBar();
        LoadingViewbox.Visibility = Visibility.Collapsed;
        SuccessViewbox.Visibility = Visibility.Visible;
        CloseAfterDelay();
    }

    /// <summary>
    /// Shows the success checkmark and completes once this window has closed, so callers
    /// can sequence work (like inserting text into another app) after the overlay has
    /// released any focus it took while its choice bar was shown.
    /// </summary>
    public Task ShowSuccessAsync()
    {
        TaskCompletionSource closedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Closed += (_, _) => closedSource.TrySetResult();
        ShowSuccess();
        return closedSource.Task;
    }

    /// <summary>
    /// Shows Cancel / Re-grab while the grab is still running, keeping the spinner visible.
    /// </summary>
    public void ShowRunningChoices()
    {
        LoadingViewbox.Visibility = Visibility.Visible;
        SendToGrabFrameButton.Visibility = Visibility.Collapsed;
        ShowChoiceBar();
    }

    /// <summary>
    /// Shows Cancel / Re-grab / Send to Grab Frame after the grab finished empty or failed.
    /// </summary>
    public void ShowFailedChoices()
    {
        LoadingViewbox.Visibility = Visibility.Collapsed;
        SendToGrabFrameButton.Visibility = Visibility.Visible;
        ShowChoiceBar();
    }

    private void ShowChoiceBar()
    {
        ChoiceBar.IsEnabled = true;

        // Animate only on the first reveal, not when the choices change while already visible.
        if (ChoiceBar.Visibility != Visibility.Visible)
        {
            ChoiceBar.Visibility = Visibility.Visible;

            CubicEase easeOut = new() { EasingMode = EasingMode.EaseOut };
            DoubleAnimation slideUp = new(choiceBarSlideDistance, 0, choiceBarSlideDuration) { EasingFunction = easeOut };
            DoubleAnimation fadeIn = new(0, 1, choiceBarSlideDuration) { EasingFunction = easeOut };
            ChoiceBarSlide.BeginAnimation(TranslateTransform.YProperty, slideUp);
            ChoiceBar.BeginAnimation(OpacityProperty, fadeIn);
        }

        // The overlay is created non-interactive; enable hit testing so the buttons respond.
        IsHitTestVisible = true;
        Activate();
    }

    private void HideChoiceBar()
    {
        ChoiceBar.Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => RaiseChoice(GrabChoice.Cancel);

    private void ReGrabButton_Click(object sender, RoutedEventArgs e) => RaiseChoice(GrabChoice.ReGrab);

    private void SendToGrabFrameButton_Click(object sender, RoutedEventArgs e) => RaiseChoice(GrabChoice.SendToGrabFrame);

    private void RaiseChoice(GrabChoice choice)
    {
        // Prevent a second click from racing another choice while the caller reacts.
        ChoiceBar.IsEnabled = false;
        ChoiceSelected?.Invoke(this, choice);
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
