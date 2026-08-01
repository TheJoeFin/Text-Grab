using System.Windows;
using System.Windows.Controls;

namespace Text_Grab.Controls;

/// <summary>
/// An expandable settings card in the style of the Windows Community Toolkit
/// SettingsExpander: icon, header and description on the left, an optional
/// control (SettingsContent) at the right edge of the header row, and related
/// sub-settings inside the expandable body (Content).
/// The header visuals are built in code so pages can use x:Name on child controls.
/// </summary>
public class SettingsExpander : Wpf.Ui.Controls.CardExpander
{
    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(SettingsExpander),
            new PropertyMetadata(string.Empty, OnHeaderTextChanged));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsExpander),
            new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty SettingsContentProperty =
        DependencyProperty.Register(nameof(SettingsContent), typeof(object), typeof(SettingsExpander),
            new PropertyMetadata(null, OnSettingsContentChanged));

    private readonly TextBlock headerTextBlock;
    private readonly TextBlock descriptionTextBlock;
    private readonly ContentPresenter settingsContentPresenter;

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

    public object? SettingsContent
    {
        get => GetValue(SettingsContentProperty);
        set => SetValue(SettingsContentProperty, value);
    }

    public SettingsExpander()
    {
        // WPF-UI's implicit styles are keyed by the exact base type, so a
        // derived control has to resolve the CardExpander style itself.
        SetResourceReference(StyleProperty, typeof(Wpf.Ui.Controls.CardExpander));

        Margin = new Thickness(0, 0, 0, 3);
        ContentPadding = new Thickness(14, 10, 14, 12);

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

        StackPanel textPanel = new() { VerticalAlignment = VerticalAlignment.Center };
        textPanel.Children.Add(headerTextBlock);
        textPanel.Children.Add(descriptionTextBlock);

        settingsContentPresenter = new ContentPresenter
        {
            Margin = new Thickness(12, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        Grid headerGrid = new() { Margin = new Thickness(0, 0, 8, 0) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(settingsContentPresenter, 1);
        headerGrid.Children.Add(textPanel);
        headerGrid.Children.Add(settingsContentPresenter);
        Header = headerGrid;
    }

    private static void OnHeaderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsExpander expander)
            expander.headerTextBlock.Text = e.NewValue as string ?? string.Empty;
    }

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SettingsExpander expander)
            return;

        string newText = e.NewValue as string ?? string.Empty;
        expander.descriptionTextBlock.Text = newText;
        expander.descriptionTextBlock.Visibility = string.IsNullOrEmpty(newText) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void OnSettingsContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsExpander expander)
            expander.settingsContentPresenter.Content = e.NewValue;
    }
}
