using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Text_Grab.Models;

namespace Text_Grab.Controls;

/// <summary>
/// A RichTextBox that shows a compact inline picker popup when the trigger character
/// (default '{') is typed, allowing users to insert named value chips into the document.
/// Supports grouped items with section headers (e.g. "Regions" and "Patterns").
/// </summary>
public class InlinePickerRichTextBox : RichTextBox
{
    private readonly Popup _popup;
    private readonly ListBox _listBox;

    private TextPointer? _triggerStart;
    private bool _isModifyingDocument;

    #region Dependency Properties

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<InlinePickerItem>),
            typeof(InlinePickerRichTextBox),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SerializedTextProperty =
        DependencyProperty.Register(
            nameof(SerializedText),
            typeof(string),
            typeof(InlinePickerRichTextBox),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    #endregion Dependency Properties

    #region Properties

    public IEnumerable<InlinePickerItem> ItemsSource
    {
        get => (IEnumerable<InlinePickerItem>?)GetValue(ItemsSourceProperty) ?? [];
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// The document serialized back to a plain string, where each chip is replaced
    /// with its <see cref="InlinePickerItem.Value"/> (e.g. "{1}").
    /// Supports two-way binding.
    /// </summary>
    public string SerializedText
    {
        get => (string)GetValue(SerializedTextProperty);
        set => SetValue(SerializedTextProperty, value);
    }

    /// <summary>Character that opens the picker popup. Default is '{'.</summary>
    public char TriggerChar { get; set; } = '{';

    #endregion Properties

    public event EventHandler<InlinePickerItem>? ItemInserted;

    /// <summary>
    /// Called when a pattern-group item is selected. The handler should show the
    /// <see cref="PatternMatchModeDialog"/> and return the configured
    /// <see cref="TemplatePatternMatch"/>, or null to cancel.
    /// </summary>
    public Func<InlinePickerItem, TemplatePatternMatch?>? PatternItemSelected { get; set; }

    static InlinePickerRichTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(InlinePickerRichTextBox),
            new FrameworkPropertyMetadata(typeof(InlinePickerRichTextBox)));
    }

    public InlinePickerRichTextBox()
    {
        AcceptsReturn = false;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

        _listBox = BuildPopupListBox();

        Border popupBorder = new()
        {
            Child = _listBox,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            Effect = new DropShadowEffect
            {
                BlurRadius = 6,
                Direction = 270,
                Opacity = 0.2,
                ShadowDepth = 2,
                Color = Colors.Black
            }
        };
        popupBorder.SetResourceReference(BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        popupBorder.SetResourceReference(BorderBrushProperty, "Teal");

        _popup = new Popup
        {
            Child = popupBorder,
            StaysOpen = true,
            AllowsTransparency = true,
            Placement = PlacementMode.RelativePoint,
            PlacementTarget = this,
        };

        TextChanged += OnTextChanged;
        PreviewKeyDown += OnPreviewKeyDown;
        LostKeyboardFocus += OnLostKeyboardFocus;
    }

    private ListBox BuildPopupListBox()
    {
        ListBox lb = new()
        {
            MinWidth = 140,
            MaxHeight = 200,
            FontSize = 11,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FocusVisualStyle = null,
            Focusable = false,
        };
        lb.SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");

        // Use a template selector to render headers vs selectable items
        lb.ItemTemplateSelector = new PickerItemTemplateSelector(
            BuildSelectableItemTemplate(),
            BuildHeaderItemTemplate());

        lb.PreviewMouseDown += ListBox_PreviewMouseDown;
        lb.ItemContainerStyle = BuildCompactItemStyle();
        return lb;
    }

    private static DataTemplate BuildSelectableItemTemplate()
    {
        DataTemplate dt = new();
        FrameworkElementFactory spFactory = new(typeof(StackPanel));
        spFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        FrameworkElementFactory nameFactory = new(typeof(TextBlock));
        nameFactory.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(InlinePickerItem.DisplayName)));
        nameFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        nameFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 2, 4, 2));

        FrameworkElementFactory valueFactory = new(typeof(TextBlock));
        valueFactory.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(InlinePickerItem.Value)));
        valueFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        valueFactory.SetValue(TextBlock.FontSizeProperty, 9.0);
        valueFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 6, 2));
        valueFactory.SetValue(TextBlock.OpacityProperty, 0.55);

        spFactory.AppendChild(nameFactory);
        spFactory.AppendChild(valueFactory);
        dt.VisualTree = spFactory;
        return dt;
    }

    private static DataTemplate BuildHeaderItemTemplate()
    {
        DataTemplate dt = new();
        FrameworkElementFactory tb = new(typeof(TextBlock));
        tb.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(InlinePickerItem.DisplayName)));
        tb.SetValue(TextBlock.FontSizeProperty, 9.5);
        tb.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        tb.SetValue(TextBlock.OpacityProperty, 0.6);
        tb.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 4, 4, 2));
        tb.SetValue(UIElement.IsHitTestVisibleProperty, false);
        dt.VisualTree = tb;
        return dt;
    }

    private static Style BuildCompactItemStyle()
    {
        // Provide a minimal ControlTemplate so WPF-UI's touch-sized ListBoxItem
        // template (large MinHeight + padding) is completely replaced.
        FrameworkElementFactory border = new(typeof(Border))
        {
            Name = "Bd"
        };
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(FrameworkElement.MarginProperty, new Thickness(1, 1, 1, 0));

        FrameworkElementFactory cp = new(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(cp);

        ControlTemplate template = new(typeof(ListBoxItem)) { VisualTree = border };

        Trigger hoverTrigger = new() { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush(Color.FromArgb(0x22, 0x30, 0x8E, 0x98)), "Bd"));
        template.Triggers.Add(hoverTrigger);

        Trigger selectedTrigger = new() { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush(Color.FromArgb(0x44, 0x30, 0x8E, 0x98)), "Bd"));
        template.Triggers.Add(selectedTrigger);

        Style style = new(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 0.0));
        style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, (Style?)null));
        return style;
    }

    #region Keyboard & Focus handling

    private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Keep popup open if focus moved into it (e.g. scrollbar click)
        if (e.NewFocus is DependencyObject target && IsVisualDescendant(_popup.Child, target))
            return;

        HidePopup();
        _triggerStart = null;
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // RichTextBox intercepts mouse-down for cursor placement before child controls receive it.
        // Detect clicks on a chip's remove button and route them manually.
        if (e.OriginalSource is DependencyObject source)
        {
            Button? btn = FindVisualAncestor<Button>(source);
            if (btn != null && FindVisualAncestor<InlineChipElement>(btn) != null)
            {
                btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, btn));
                e.Handled = true;
                return;
            }
        }
        base.OnPreviewMouseLeftButtonDown(e);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_popup.IsOpen)
            return;

        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                MoveSelection(1);
                break;

            case Key.Up:
                e.Handled = true;
                MoveSelection(-1);
                break;

            case Key.Enter:
            case Key.Tab:
                e.Handled = true;
                CommitSelection();
                break;

            case Key.Escape:
                e.Handled = true;
                HidePopup();
                _triggerStart = null;
                break;

            case Key.Back:
                // If backspace reaches the trigger char position, close the popup
                TextPointer? oneBack = CaretPosition.GetNextInsertionPosition(LogicalDirection.Backward);
                if (oneBack != null && _triggerStart != null && oneBack.CompareTo(_triggerStart) <= 0)
                {
                    HidePopup();
                    _triggerStart = null;
                }
                break;
        }
    }

    #endregion Keyboard & Focus handling

    /// <summary>
    /// Moves the listbox selection by <paramref name="direction"/> (+1 or -1),
    /// skipping non-selectable section header items.
    /// </summary>
    private void MoveSelection(int direction)
    {
        int count = _listBox.Items.Count;
        if (count == 0) return;

        int start = _listBox.SelectedIndex;
        int next = start;

        for (int i = 0; i < count; i++)
        {
            next = (next + direction + count) % count;
            if (_listBox.Items[next] is InlinePickerItem item && item.Group != HeaderGroupTag)
            {
                _listBox.SelectedIndex = next;
                _listBox.ScrollIntoView(_listBox.SelectedItem);
                return;
            }
        }
    }

    #region Text change & popup management

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isModifyingDocument)
            return;

        if (_triggerStart == null)
        {
            // Detect trigger char just inserted before caret
            TextPointer? prev = CaretPosition.GetNextInsertionPosition(LogicalDirection.Backward);
            if (prev != null && new TextRange(prev, CaretPosition).Text == TriggerChar.ToString())
            {
                _triggerStart = prev;
                RefreshPopup();
                return;
            }
        }
        else
        {
            // If caret moved back past the trigger, close the popup
            if (CaretPosition.CompareTo(_triggerStart) <= 0)
            {
                HidePopup();
                _triggerStart = null;
                return;
            }

            RefreshPopup();
        }

        UpdateSerializedText();
    }

    private void RefreshPopup()
    {
        List<InlinePickerItem> filtered = GetFilteredItems();

        if (filtered.Count == 0)
        {
            HidePopup();
            return;
        }

        _listBox.ItemsSource = filtered;

        // Auto-select the first non-header item
        if (_listBox.SelectedIndex < 0 || _listBox.SelectedIndex >= filtered.Count)
        {
            int firstSelectable = filtered.FindIndex(i => i.Group != HeaderGroupTag);
            _listBox.SelectedIndex = firstSelectable >= 0 ? firstSelectable : 0;
        }

        if (!_popup.IsOpen)
        {
            Rect caretRect = CaretPosition.GetCharacterRect(LogicalDirection.Forward);
            _popup.HorizontalOffset = caretRect.X;
            _popup.VerticalOffset = caretRect.Bottom + 2;
            _popup.IsOpen = true;
        }
    }

    private void HidePopup() => _popup.IsOpen = false;

    private List<InlinePickerItem> GetFilteredItems()
    {
        if (_triggerStart == null)
            return [];

        string filterText = new TextRange(_triggerStart, CaretPosition).Text
            .TrimStart(TriggerChar);

        IEnumerable<InlinePickerItem> source = ItemsSource ?? [];

        List<InlinePickerItem> filtered = filterText.Length == 0
            ? [.. source]
            : [.. source.Where(i =>
                i.DisplayName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                i.Value.Contains(filterText, StringComparison.OrdinalIgnoreCase))];

        // Insert section headers when items have different groups
        return InsertGroupHeaders(filtered);
    }

    private static List<InlinePickerItem> InsertGroupHeaders(List<InlinePickerItem> items)
    {
        if (items.Count == 0)
            return items;

        // Check if there are multiple distinct groups
        List<string> distinctGroups = items
            .Select(i => i.Group)
            .Where(g => !string.IsNullOrEmpty(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctGroups.Count <= 1)
            return items;

        // Build list with section headers
        List<InlinePickerItem> result = [];
        string? currentGroup = null;

        foreach (InlinePickerItem item in items)
        {
            string group = string.IsNullOrEmpty(item.Group) ? "Other" : item.Group;
            if (!string.Equals(group, currentGroup, StringComparison.OrdinalIgnoreCase))
            {
                currentGroup = group;
                result.Add(new InlinePickerItem($"── {group} ──", "") { Group = HeaderGroupTag });
            }
            result.Add(item);
        }

        return result;
    }

    /// <summary>Sentinel group value for non-selectable section header items.</summary>
    internal const string HeaderGroupTag = "__header__";

    #endregion Text change & popup management

    #region Selection & chip insertion

    private void ListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? hit = e.OriginalSource as DependencyObject;
        while (hit is not null and not ListBoxItem)
            hit = VisualTreeHelper.GetParent(hit);

        if (hit is ListBoxItem item)
        {
            _listBox.SelectedItem = item.DataContext ?? item.Content;
            CommitSelection();
            e.Handled = true;
            Focus();
        }
    }

    private void CommitSelection()
    {
        if (_listBox.SelectedItem is not InlinePickerItem selectedItem || _triggerStart == null)
            return;

        // Skip non-selectable header items
        if (selectedItem.Group == HeaderGroupTag)
            return;

        // Save trigger position before any dialog opens (dialogs steal focus,
        // which fires OnLostKeyboardFocus and nulls _triggerStart)
        TextPointer savedTriggerStart = _triggerStart;

        // For pattern items, invoke the dialog callback to configure match mode
        bool isPatternItem = string.Equals(selectedItem.Group, "Patterns", StringComparison.OrdinalIgnoreCase);
        InlinePickerItem itemToInsert = selectedItem;

        if (isPatternItem && PatternItemSelected != null)
        {
            HidePopup();

            TemplatePatternMatch? patternConfig = PatternItemSelected(selectedItem);
            if (patternConfig == null)
            {
                _triggerStart = null;
                return; // user cancelled
            }

            // Build the placeholder value from the dialog result
            string placeholderValue = BuildPatternPlaceholder(patternConfig);
            string displayLabel = $"{patternConfig.PatternName} ({patternConfig.MatchMode})";
            itemToInsert = new InlinePickerItem(displayLabel, placeholderValue, selectedItem.Group);
        }

        _isModifyingDocument = true;
        try
        {
            // Delete from trigger position to current caret (removes "{" + typed filter text)
            new TextRange(savedTriggerStart, CaretPosition).Text = string.Empty;

            // CaretPosition is now at the insertion point (where trigger was)
            InlineChipElement chip = new()
            {
                DisplayName = itemToInsert.DisplayName,
                Value = itemToInsert.Value,
            };
            chip.RemoveRequested += Chip_RemoveRequested;

            InlineUIContainer container = new(chip, CaretPosition)
            {
                BaselineAlignment = BaselineAlignment.Center,
            };

            // Move caret to just after the inserted chip
            TextPointer? afterChip = container.ElementEnd.GetNextInsertionPosition(LogicalDirection.Forward);
            CaretPosition = afterChip ?? container.ElementEnd;

            HidePopup();
            _triggerStart = null;
            ItemInserted?.Invoke(this, itemToInsert);
        }
        finally
        {
            _isModifyingDocument = false;
        }

        UpdateSerializedText();
        Focus();
    }

    private static string BuildPatternPlaceholder(TemplatePatternMatch config)
    {
        string mode = config.MatchMode;

        // Include separator in placeholder for "all" and multi-index modes
        bool needsSeparator = mode == "all"
            || (mode.Contains(',') && mode.Split(',').Length > 1);

        if (needsSeparator && config.Separator != ", ")
            return $"{{p:{config.PatternName}:{mode}:{config.Separator}}}";

        return $"{{p:{config.PatternName}:{mode}}}";
    }

    private void Chip_RemoveRequested(object? sender, EventArgs e)
    {
        if (sender is not InlineChipElement chip)
            return;

        _isModifyingDocument = true;
        try
        {
            foreach (Block block in Document.Blocks)
            {
                if (block is not Paragraph para)
                    continue;

                foreach (Inline inline in para.Inlines.ToList())
                {
                    if (inline is InlineUIContainer { Child: InlineChipElement c } iuc && c == chip)
                    {
                        para.Inlines.Remove(iuc);
                        break;
                    }
                }
            }
        }
        finally
        {
            _isModifyingDocument = false;
        }

        UpdateSerializedText();
    }

    #endregion Selection & chip insertion

    #region Serialization

    private void UpdateSerializedText() => SerializedText = GetSerializedText();

    /// <summary>
    /// Returns the document content as a plain string, with each chip replaced by
    /// its <see cref="InlinePickerItem.Value"/> (e.g. "{1}").
    /// </summary>
    public string GetSerializedText()
    {
        StringBuilder sb = new();
        bool firstPara = true;

        foreach (Block block in Document.Blocks)
        {
            if (block is not Paragraph para)
                continue;

            if (!firstPara)
                sb.AppendLine();
            firstPara = false;

            foreach (Inline inline in para.Inlines)
            {
                switch (inline)
                {
                    case Run run:
                        sb.Append(run.Text);
                        break;
                    case InlineUIContainer { Child: InlineChipElement chip }:
                        sb.Append(chip.Value);
                        break;
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Populates the document from a serialized string, recreating chips for any
    /// <see cref="InlinePickerItem.Value"/> tokens found in <paramref name="text"/>.
    /// </summary>
    public void SetSerializedText(string text, IEnumerable<InlinePickerItem>? items = null)
    {
        _isModifyingDocument = true;
        try
        {
            Document.Blocks.Clear();
            Paragraph para = new();
            Document.Blocks.Add(para);

            IEnumerable<InlinePickerItem> source = items ?? ItemsSource ?? [];
            string remaining = text ?? string.Empty;

            while (remaining.Length > 0)
            {
                InlinePickerItem? nextItem = null;
                int nextIndex = int.MaxValue;

                foreach (InlinePickerItem item in source)
                {
                    int idx = remaining.IndexOf(item.Value, StringComparison.Ordinal);
                    if (idx >= 0 && idx < nextIndex)
                    {
                        nextIndex = idx;
                        nextItem = item;
                    }
                }

                if (nextItem == null)
                {
                    para.Inlines.Add(new Run(remaining));
                    break;
                }

                if (nextIndex > 0)
                    para.Inlines.Add(new Run(remaining[..nextIndex]));

                InlineChipElement chip = new()
                {
                    DisplayName = nextItem.DisplayName,
                    Value = nextItem.Value,
                };
                chip.RemoveRequested += Chip_RemoveRequested;
                para.Inlines.Add(new InlineUIContainer(chip) { BaselineAlignment = BaselineAlignment.Center });

                remaining = remaining[(nextIndex + nextItem.Value.Length)..];
            }
        }
        finally
        {
            _isModifyingDocument = false;
        }

        UpdateSerializedText();
    }

    #endregion Serialization

    private static T? FindVisualAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        DependencyObject? current = element;
        while (current != null)
        {
            if (current is T match) return match;
            // ContentElements (Paragraph, Run, Inline, …) are not Visual —
            // VisualTreeHelper.GetParent throws for them, so stop here.
            current = current is Visual ? VisualTreeHelper.GetParent(current) : null;
        }
        return null;
    }

    private static bool IsVisualDescendant(DependencyObject? root, DependencyObject target)
    {
        DependencyObject? current = target;
        while (current != null)
        {
            if (current == root) return true;
            current = VisualTreeHelper.GetParent(current)
                      ?? LogicalTreeHelper.GetParent(current);
        }
        return false;
    }
}

/// <summary>
/// Selects between a selectable item template and a non-selectable section header template
/// based on the <see cref="InlinePickerItem.Group"/> value.
/// </summary>
internal class PickerItemTemplateSelector : DataTemplateSelector
{
    private readonly DataTemplate _selectableTemplate;
    private readonly DataTemplate _headerTemplate;

    public PickerItemTemplateSelector(DataTemplate selectableTemplate, DataTemplate headerTemplate)
    {
        _selectableTemplate = selectableTemplate;
        _headerTemplate = headerTemplate;
    }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is InlinePickerItem pickerItem
            && pickerItem.Group == InlinePickerRichTextBox.HeaderGroupTag)
            return _headerTemplate;

        return _selectableTemplate;
    }
}
