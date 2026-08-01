using System.Windows;
using System.Windows.Controls;

namespace Text_Grab.Controls;

/// <summary>
/// A settings row card in the style of the Windows Community Toolkit SettingsCard:
/// icon, header and description on the left, control (Content) on the right.
/// The header visuals are built in code so pages can use x:Name on child controls.
/// </summary>
public class SettingsCard : Wpf.Ui.Controls.CardControl
{
    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(SettingsCard),
            new PropertyMetadata(string.Empty, OnHeaderTextChanged));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsCard),
            new PropertyMetadata(string.Empty, OnDescriptionChanged));

    private readonly TextBlock headerTextBlock;
    private readonly TextBlock descriptionTextBlock;

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public SettingsCard()
    {
        // WPF-UI's implicit styles are keyed by the exact base type, so a
        // derived control has to resolve the CardControl style itself.
        SetResourceReference(StyleProperty, typeof(Wpf.Ui.Controls.CardControl));

        // CardControl derives from ButtonBase; keep it out of the tab order
        // so focus goes straight to the inner control.
        Focusable = false;
        IsTabStop = false;
        Margin = new Thickness(0, 0, 0, 3);

        headerTextBlock = new TextBlock
        {
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
        };
        headerTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");

        descriptionTextBlock = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        descriptionTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

        StackPanel headerPanel = new() { Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        headerPanel.Children.Add(headerTextBlock);
        headerPanel.Children.Add(descriptionTextBlock);
        Header = headerPanel;
    }

    private static void OnHeaderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsCard card)
            card.headerTextBlock.Text = e.NewValue as string ?? string.Empty;
    }

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SettingsCard card)
            return;

        string newText = e.NewValue as string ?? string.Empty;
        card.descriptionTextBlock.Text = newText;
        card.descriptionTextBlock.Visibility = string.IsNullOrEmpty(newText) ? Visibility.Collapsed : Visibility.Visible;
    }
}
