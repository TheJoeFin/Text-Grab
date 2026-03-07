using System;
using System.Windows;
using System.Windows.Controls;

namespace Text_Grab.Controls;

[TemplatePart(Name = PartRemoveButton, Type = typeof(Button))]
public class InlineChipElement : Control
{
    private const string PartRemoveButton = "PART_RemoveButton";

    public static readonly DependencyProperty DisplayNameProperty =
        DependencyProperty.Register(nameof(DisplayName), typeof(string), typeof(InlineChipElement),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(InlineChipElement),
            new PropertyMetadata(string.Empty));

    public string DisplayName
    {
        get => (string)GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public event EventHandler? RemoveRequested;

    private Button? _removeButton;

    static InlineChipElement()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(InlineChipElement),
            new FrameworkPropertyMetadata(typeof(InlineChipElement)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_removeButton is not null)
            _removeButton.Click -= RemoveButton_Click;

        _removeButton = GetTemplateChild(PartRemoveButton) as Button;

        if (_removeButton is not null)
            _removeButton.Click += RemoveButton_Click;
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
        => RemoveRequested?.Invoke(this, EventArgs.Empty);
}
