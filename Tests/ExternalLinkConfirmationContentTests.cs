using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace Tests;

public class ExternalLinkConfirmationContentTests
{
    [WpfFact]
    public async Task WarningFlyout_HasOpaqueContrastingColorsInBothThemes()
    {
        ExternalLinkConfirmationContent content = new(new Uri("tel:+15551234567"));
        Button button = Assert.IsType<Button>(content.FindName("WarningButton"));
        Popup popup = Assert.IsType<Popup>(content.FindName("WarningPopup"));
        Border border = Assert.IsType<Border>(popup.Child);
        TextBlock warning = Assert.IsType<TextBlock>(border.Child);
        Window window = new() { Content = content, ShowInTaskbar = false, Width = 500, Height = 300 };
        try
        {
            window.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = ApplicationTheme.Light });
            window.Show();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            foreach (ApplicationTheme theme in new[] { ApplicationTheme.Light, ApplicationTheme.Dark })
            {
                window.Resources.MergedDictionaries.Clear();
                window.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = theme });
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

                Assert.True(popup.IsOpen);
                SolidColorBrush background = Assert.IsType<SolidColorBrush>(border.Background);
                SolidColorBrush foreground = Assert.IsType<SolidColorBrush>(warning.Foreground);
                Assert.Equal(byte.MaxValue, background.Color.A);
                Assert.Equal(1, background.Opacity);
                Assert.Equal(Assert.IsType<SolidColorBrush>(window.FindResource("SolidBackgroundFillColorBaseBrush")).Color, background.Color);
                Assert.Equal(Assert.IsType<SolidColorBrush>(window.FindResource("TextFillColorPrimaryBrush")).Color, foreground.Color);
                Assert.NotEqual(background.Color, foreground.Color);
            }
        }
        finally
        {
            popup.IsOpen = false;
            window.Close();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        }
    }

    [WpfFact]
    public async Task WarningSymbol_OpensTrustSourceFlyoutAndClosesOnUnload()
    {
        ExternalLinkConfirmationContent content = new(new Uri("mailto:user@example.invalid"));
        Button button = Assert.IsType<Button>(content.FindName("WarningButton"));
        Popup popup = Assert.IsType<Popup>(content.FindName("WarningPopup"));
        Window window = new() { Content = content, ShowInTaskbar = false, Width = 500, Height = 300 };
        try
        {
            window.Show();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.False(popup.IsOpen);
            Assert.Equal(Wpf.Ui.Controls.SymbolRegular.Warning24, Assert.IsType<Wpf.Ui.Controls.SymbolIcon>(button.Content).Symbol);
            Assert.Contains("trust the source", AutomationProperties.GetHelpText(button));

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.True(popup.IsOpen);
            Assert.False(popup.StaysOpen);
            Assert.Same(button, popup.PlacementTarget);
            TextBlock warning = Assert.IsType<TextBlock>(Assert.IsType<Border>(popup.Child).Child);
            Assert.Contains("Only open this link when you trust the source", warning.Text);
            Assert.Contains("incur charges", warning.Text);

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(popup.IsOpen);

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            content.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.False(popup.IsOpen);
        }
        finally
        {
            window.Close();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        }
    }
}
