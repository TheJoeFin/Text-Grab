using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Panes;
using Text_Grab.Services;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Text_Grab;

/// <summary>
/// The window's side of the calc pane (<see cref="Editing.Panes.CalcPane"/>): whether it is shown,
/// its width, Show Errors, and the toggles that change them. The pane itself evaluates and shows the
/// results. The toggles stay click handlers here until E2 moves them onto registered actions.
/// </summary>
public partial class EditTextWindow
{
    /// <summary>The name <see cref="FrameworkElement.FindName"/> finds the calc pane by, as when the XAML declared it.</summary>
    private const string CalcPaneName = "CalcPaneControl";

    /// <summary>The window's calc pane, made by its factory when the window is initialized.</summary>
    private CalcPane? calcPane;

    /// <summary>The panes in this window. Only the calc pane until layouts (E4a) place others.</summary>
    private IEnumerable<IEditorPane> Panes => calcPane is null ? [] : [calcPane];

    /// <summary>A new pane of the kind <paramref name="paneId"/> names, from its factory, or null if no factory makes it.</summary>
    private static IEditorPane? CreatePane(string paneId)
        => AppServices.Provider.GetServices<IEditorPaneFactory>()
            .FirstOrDefault(factory => factory.Descriptor.Id == paneId)?
            .Create();

    /// <summary>
    /// Makes the calc pane, puts it in the editor area's second column where the XAML used to declare
    /// it, and connects it to this window's document. Called once, from <c>Window_Initialized</c>.
    /// </summary>
    private void AttachCalcPane()
    {
        if (CreatePane(PaneIds.Calc) is not CalcPane pane)
            return;

        calcPane = pane;
        System.Windows.Controls.Grid.SetColumn(pane, 1);
        EditorAreaGrid.Children.Insert(EditorAreaGrid.Children.IndexOf(ReadingEditorHost) + 1, pane);
        ConnectCalcPane(pane);
    }

    /// <summary>The bindings, name and events every calc pane has in this window, wherever it is placed.</summary>
    private void ConnectCalcPane(CalcPane pane)
    {
        RegisterName(CalcPaneName, pane);
        pane.SetBinding(CalcPane.ResultsFontSizeProperty, new Binding(nameof(PassedTextControl.FontSize)) { Source = PassedTextControl, Mode = BindingMode.OneWay });
        pane.SetBinding(CalcPane.ShowErrorsProperty, new Binding(nameof(MenuItem.IsChecked)) { Source = ShowErrorsMenuItem, Mode = BindingMode.OneWay });
        pane.Attach(Session, config: null);
        pane.HideRequested += CalcPane_HideRequested;
        pane.ShowErrorsToggleRequested += CalcPane_ShowErrorsToggleRequested;
    }

    private void DetachCalcPane()
    {
        if (calcPane is null)
            return;

        calcPane.HideRequested -= CalcPane_HideRequested;
        calcPane.ShowErrorsToggleRequested -= CalcPane_ShowErrorsToggleRequested;
        calcPane.Detach();
    }

    /// <summary>
    /// The calc pane's part of <c>Window_Loaded</c>: its open state and width from settings (unless a
    /// history item set them) and Show Errors.
    /// </summary>
    private void InitializeCalcPane()
    {
        // Restore calc pane width from settings if not loading from history
        if (ShowCalcPaneMenuItem.Tag is not true && DefaultSettings.CalcPaneWidth > 0)
        {
            _lastCalcColumnWidth = new GridLength(DefaultSettings.CalcPaneWidth, GridUnitType.Pixel);
        }

        if (ShowCalcPaneMenuItem.Tag is not true)
            ShowCalcPaneMenuItem.IsChecked = DefaultSettings.CalcShowPane;

        ShowErrorsMenuItem.IsChecked = DefaultSettings.CalcShowErrors;
        SetCalcPaneVisibility();
    }

    /// <summary>
    /// Shows or hides the calc pane in this window only, the way restoring a history item does: the
    /// default for new windows (<c>CalcShowPane</c>) is left as it is.
    /// </summary>
    internal void SetCalcPaneOpen(bool open)
    {
        ShowCalcPaneMenuItem.Tag = true;
        ShowCalcPaneMenuItem.IsChecked = open;
        SetCalcPaneVisibility();
    }

    /// <summary>Evaluates the calc pane now, rather than after the typing delay.</summary>
    internal Task EvaluateCalcPaneAsync() => calcPane?.RecalculateAsync() ?? Task.CompletedTask;

    private void CalcPane_HideRequested(object? sender, EventArgs e)
    {
        ShowCalcPaneMenuItem.IsChecked = false;
        DefaultSettings.CalcShowPane = false;
        SetCalcPaneVisibility();
    }

    private void CalcPane_ShowErrorsToggleRequested(object? sender, EventArgs e)
    {
        // The pane's ShowErrors is bound to the menu item, so this reaches it before the evaluation.
        ShowErrorsMenuItem.IsChecked = !ShowErrorsMenuItem.IsChecked;
        DefaultSettings.CalcShowErrors = ShowErrorsMenuItem.IsChecked;
        _ = calcPane?.RecalculateAsync();
    }

    private async void ShowErrorsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Re-evaluate expressions when toggle changes
        if (calcPane is not null)
            await calcPane.RecalculateAsync();
    }

    // A bottom-bar button (ButtonInfo "Copy All Calculation Results") calls this by name.
    private void CalcCopyAllButton_Click(object sender, RoutedEventArgs e) => calcPane?.CopyAllResults();

    private void ShowCalcPaneMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;

        DefaultSettings.CalcShowPane = menuItem.IsChecked;

        SetCalcPaneVisibility();
    }

    private void CalcToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCalcPaneMenuItem.IsChecked = !ShowCalcPaneMenuItem.IsChecked;
        DefaultSettings.CalcShowPane = ShowCalcPaneMenuItem.IsChecked;
        SetCalcPaneVisibility();
    }

    private void ToggleCalcPaneExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ShowCalcPaneMenuItem.IsChecked = !ShowCalcPaneMenuItem.IsChecked;
        DefaultSettings.CalcShowPane = ShowCalcPaneMenuItem.IsChecked;
        SetCalcPaneVisibility();
    }

    private void SetCalcPaneVisibility()
    {
        // Check if we're loading from history and should ignore default settings
        if (ShowCalcPaneMenuItem.Tag is bool fromHistory && fromHistory)
        {
            ShowCalcPaneMenuItem.Tag = null; // Clear the flag after first use
            // Use ShowCalcPaneMenuItem.IsChecked which was set from history
        }
        else
        {
            // Not from history, apply user's default setting
            ShowCalcPaneMenuItem.IsChecked = DefaultSettings.CalcShowPane;
        }

        if (calcPane is null)
            return;

        if (ShowCalcPaneMenuItem.IsChecked)
        {
            TextBoxSplitter.Visibility = Visibility.Visible;
            CalcPaneShadow.Visibility = Visibility.Visible;

            // Restore previous width if it was collapsed
            if (CalcColumn.Width.Value == 0)
                CalcColumn.Width = _lastCalcColumnWidth;

            // Disable text wrapping when calc pane is visible to maintain vertical alignment
            // Store the previous wrapping state to restore later
            if (PassedTextControl.TextWrapping != TextWrapping.NoWrap)
            {
                _previousTextWrapping = PassedTextControl.TextWrapping;
                PassedTextControl.TextWrapping = TextWrapping.NoWrap;
            }

            // Shows the results and evaluates after the typing delay.
            calcPane.IsOpen = true;
        }
        else
        {
            calcPane.IsOpen = false;
            TextBoxSplitter.Visibility = Visibility.Collapsed;
            CalcPaneShadow.Visibility = Visibility.Collapsed;

            // Remember current width, then collapse column to remove pane area
            if (CalcColumn.Width.Value > 0)
                _lastCalcColumnWidth = CalcColumn.Width;
            CalcColumn.Width = new GridLength(0);

            // Restore previous text wrapping setting when calc pane is hidden
            if (_previousTextWrapping.HasValue)
            {
                PassedTextControl.TextWrapping = _previousTextWrapping.Value;
                _previousTextWrapping = null;
            }
        }
    }

    private void ShowErrorsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;

        DefaultSettings.CalcShowErrors = menuItem.IsChecked;
        _ = calcPane?.RecalculateAsync();
    }

    private void TextBoxSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Toggle between equal split (star) and collapsed
        if (CalcColumn.Width.IsStar)
        {
            // If already star-sized, collapse the pane
            ShowCalcPaneMenuItem.IsChecked = false;
            DefaultSettings.CalcShowPane = false;
            SetCalcPaneVisibility();
        }
        else
        {
            // If collapsed or pixel-sized, set to equal split (1 star = 50% of available space)
            CalcColumn.Width = new GridLength(1, GridUnitType.Star);
            _lastCalcColumnWidth = new GridLength(1, GridUnitType.Star);
        }
    }
}
