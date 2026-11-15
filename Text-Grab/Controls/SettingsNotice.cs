using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Text_Grab.Controls;

/// <summary>
/// An inline notice shown under a settings card to explain why a setting is disabled
/// or has no effect, with an optional action button that fixes the cause.
/// The notice collapses itself whenever <see cref="Message"/> is empty.
/// </summary>
public class SettingsNotice : Border
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(SettingsNotice),
            new PropertyMetadata(string.Empty, OnMessageChanged));

    public static readonly DependencyProperty ActionTextProperty =
        DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(SettingsNotice),
            new PropertyMetadata(string.Empty, OnActionTextChanged));

    private readonly TextBlock messageTextBlock;
    private readonly Wpf.Ui.Controls.Button actionButton;

    public event RoutedEventHandler? ActionClick;

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public SettingsNotice()
    {
        Visibility = Visibility.Collapsed;
        Margin = new Thickness(0, 0, 0, 8);
        Padding = new Thickness(12, 8, 12, 8);
        CornerRadius = new CornerRadius(4);
        SetResourceReference(BackgroundProperty, "ControlFillColorSecondaryBrush");

        Wpf.Ui.Controls.SymbolIcon icon = new()
        {
            Symbol = Wpf.Ui.Controls.SymbolRegular.Info16,
            FontSize = 16,
            Margin = new Thickness(0, 1, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        icon.SetResourceReference(Control.ForegroundProperty, "TextFillColorSecondaryBrush");

        messageTextBlock = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        messageTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

        actionButton = new Wpf.Ui.Controls.Button
        {
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        actionButton.Click += (_, e) => ActionClick?.Invoke(this, e);

        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(messageTextBlock, 1);
        Grid.SetColumn(actionButton, 2);
        grid.Children.Add(icon);
        grid.Children.Add(messageTextBlock);
        grid.Children.Add(actionButton);
        Child = grid;

        Loaded += (_, _) =>
        {
            // Border has no automation peer of its own, so expose the text and button
            // under ids derived from this notice's id for the UI test harness.
            string id = AutomationProperties.GetAutomationId(this);
            if (string.IsNullOrEmpty(id))
                return;

            AutomationProperties.SetAutomationId(messageTextBlock, $"{id}.Text");
            AutomationProperties.SetAutomationId(actionButton, $"{id}.Action");
        };
    }

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SettingsNotice notice)
            return;

        string text = e.NewValue as string ?? string.Empty;
        notice.messageTextBlock.Text = text;
        notice.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void OnActionTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SettingsNotice notice)
            return;

        string text = e.NewValue as string ?? string.Empty;
        notice.actionButton.Content = text;
        notice.actionButton.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
