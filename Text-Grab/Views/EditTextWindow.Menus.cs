using Humanizer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using Text_Grab.Controls;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Text_Grab;

public partial class EditTextWindow
{
    private void AddPossibleMailToToRightClickMenu(int caretIndex)
    {
        string possibleEmail = Session.SelectedText;

        if (string.IsNullOrEmpty(possibleEmail))
            possibleEmail = Session.Text.GetWordAtCursorPosition(caretIndex);

        if (!possibleEmail.IsValidEmailAddress())
            return;

        if (ExternalLinkLauncher.CreateMenuItem($"mailto:{possibleEmail}") is not MenuItem emailMi)
            return;

        PassedTextControl.ContextMenu?.Items.Insert(0, new Separator());
        PassedTextControl.ContextMenu?.Items.Insert(0, emailMi);
    }

    private void AddPossibleSpellingErrorsToRightClickMenu(int caretIndex)
    {
        int cmdIndex = 0;
        SpellingError spellingError;
        spellingError = PassedTextControl.GetSpellingError(caretIndex);
        if (spellingError is not null
            && PassedTextControl.ContextMenu is not null)
        {
            foreach (string str in spellingError.Suggestions)
            {
                MenuItem mi = new()
                {
                    Header = str,
                    FontWeight = FontWeights.Bold,
                    Command = System.Windows.Documents.EditingCommands.CorrectSpellingError,
                    CommandParameter = str,
                    CommandTarget = PassedTextControl
                };
                PassedTextControl.ContextMenu.Items.Insert(cmdIndex, mi);
                cmdIndex++;
            }

            if (cmdIndex == 0)
            {
                MenuItem mi = new()
                {
                    Header = "no suggestions",
                    IsEnabled = false
                };
                PassedTextControl.ContextMenu.Items.Insert(cmdIndex, mi);
                cmdIndex++;
            }

            PassedTextControl.ContextMenu.Items.Insert(cmdIndex, new Separator());
            cmdIndex++;
            MenuItem ignoreAllMI = new()
            {
                Header = "Ignore All",
                Command = System.Windows.Documents.EditingCommands.IgnoreSpellingError,
                CommandTarget = PassedTextControl
            };
            PassedTextControl.ContextMenu.Items.Insert(cmdIndex, ignoreAllMI);
            cmdIndex++;
            PassedTextControl.ContextMenu.Items.Insert(cmdIndex, new Separator());
        }
    }

    private void AddPossibleURLToRightClickMenu(int caretIndex)
    {
        string possibleURL = Session.SelectedText;

        if (string.IsNullOrEmpty(possibleURL))
        {
            possibleURL = Session.Text.GetWordAtCursorPosition(caretIndex);
        }

        if (ExternalLinkLauncher.CreateMenuItem(possibleURL) is MenuItem urlMi)
        {
            PassedTextControl.ContextMenu?.Items.Insert(0, new Separator());
            PassedTextControl.ContextMenu?.Items.Insert(0, urlMi);
        }
    }

    private void CaptureMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        LoadLanguageMenuItems(LanguageMenuItem);
    }

    private void GrabTemplateMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        LoadGrabTemplateMenuItems(GrabTemplateMenuItem);
    }

    private void EditMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        // Load text-only templates fresh each time the Edit menu opens and use them to
        // populate the Grab Templates group in both apply flyouts.
        List<GrabTemplate> textOnlyTemplates = [.. GrabTemplateManager.GetAllTemplates().Where(template => template.IsTextOnly && template.IsValid)];

        PopulateTemplateMenu(ApplyGrabTemplateMenuItem, textOnlyTemplates, ApplyGrabTemplateItem_Click);
        PopulateTemplateMenu(ApplyGrabTemplatePerLineMenuItem, textOnlyTemplates, ApplyGrabTemplatePerLineItem_Click);

        List<PatternItem> patterns = [.. PatternItemCatalog.GetAll()];
        PopulatePatternMenu(
            ApplyPatternMenuItem,
            patterns.Where(pattern => pattern.Kind == PatternKind.SavedRegex),
            ApplyPatternItem_Click);
        PopulatePatternMenu(
            ApplySmartPatternMenuItem,
            patterns.Where(pattern => pattern.Kind == PatternKind.Recognizer),
            ApplyPatternItem_Click);
        PopulatePatternMenu(
            ApplyPatternPerLineMenuItem,
            patterns.Where(pattern => pattern.Kind == PatternKind.SavedRegex),
            ApplyPatternPerLineItem_Click);
        PopulatePatternMenu(
            ApplySmartPatternPerLineMenuItem,
            patterns.Where(pattern => pattern.Kind == PatternKind.Recognizer),
            ApplyPatternPerLineItem_Click);
    }

    private static void PopulatePatternMenu(
        MenuItem parent,
        IEnumerable<PatternItem> patterns,
        RoutedEventHandler clickHandler)
    {
        // Rebuild each time the menu opens so newly saved regexes appear.
        parent.Items.Clear();

        foreach (PatternItem pattern in patterns)
        {
            MenuItem patternItem = new()
            {
                Header = pattern.Name,
                ToolTip = string.IsNullOrWhiteSpace(pattern.Description) ? null : pattern.Description,
                Tag = pattern,
            };
            patternItem.Click += clickHandler;
            parent.Items.Add(patternItem);
        }

        parent.Visibility = parent.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyPatternItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: PatternItem pattern })
            return;

        ApplySelectedTextOrAllTextTransform(text => PatternExecutor.Apply(pattern, text));
    }

    private void ApplyPatternPerLineItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: PatternItem pattern })
            return;

        ApplySelectedTextOrAllTextTransform(text => ApplyPatternPerLine(pattern, text));
    }

    /// <summary>
    /// Splits <paramref name="text"/> into lines and applies the pattern to each
    /// non-blank line independently, preserving blank lines and line structure.
    /// </summary>
    private static string ApplyPatternPerLine(PatternItem pattern, string text)
    {
        string[] lines = text.Split(Environment.NewLine);

        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                lines[i] = PatternExecutor.Apply(pattern, lines[i]);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void PopulateTemplateMenu(MenuItem parent, List<GrabTemplate> templates, RoutedEventHandler clickHandler)
    {
        parent.Items.Clear();

        if (templates.Count == 0)
        {
            parent.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (GrabTemplate template in templates)
        {
            MenuItem templateItem = new()
            {
                Header = template.Name,
                ToolTip = string.IsNullOrWhiteSpace(template.Description) ? null : template.Description,
                Tag = template,
            };
            templateItem.Click += clickHandler;
            parent.Items.Add(templateItem);
        }

        parent.Visibility = Visibility.Visible;
    }

    private void ApplyGrabTemplateItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: GrabTemplate template })
            return;

        ApplySelectedTextOrAllTextTransform(text => GrabTemplateExecutor.ApplyTextOnlyTemplate(template, text));
        GrabTemplateManager.RecordUsage(template.Id);
    }

    private void ApplyGrabTemplatePerLineItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: GrabTemplate template })
            return;

        ApplySelectedTextOrAllTextTransform(text => ApplyTemplatePerLine(template, text));
        GrabTemplateManager.RecordUsage(template.Id);
    }

    /// <summary>
    /// Splits <paramref name="text"/> into lines and applies the text-only template to each
    /// non-blank line independently, preserving blank lines and the overall line structure.
    /// </summary>
    private static string ApplyTemplatePerLine(GrabTemplate template, string text)
    {
        string[] lines = text.Split(Environment.NewLine);

        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                lines[i] = GrabTemplateExecutor.ApplyTextOnlyTemplate(template, lines[i]);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void OpenRecentEditWindowExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string currentText = Session.Text;

        HistoryInfo? historyInfo = Singleton<HistoryService>.Instance.GetEditWindows().LastOrDefault();

        if (historyInfo is null)
        {
            // No history available, just open a new window
            EditTextWindow etw = new();
            etw.Show();
            return;
        }

        EditTextWindow etwHistory = new(historyInfo);
        etwHistory.Show();
        etwHistory.Activate();

        if (string.IsNullOrWhiteSpace(currentText))
            Close();
    }

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (LanguageMenuItem is null || sender is not MenuItem clickedMenuItem)
            return;

        if (clickedMenuItem.Tag is not ILanguage ILang)
            return;

        selectedILanguage = ILang;
        CaptureLanguageUtilities.PersistSelectedLanguage(selectedILanguage);

        if (selectedILanguage is not GlobalLang)
        {
            SetCultureAndLanguageToDefault();
        }
        else
        {
            try
            {
                CultureInfo cultureInfo = new(selectedILanguage.LanguageTag);
                selectedCultureInfo = cultureInfo;
                XmlLanguage xmlLang = XmlLanguage.GetLanguage(cultureInfo.IetfLanguageTag);
                Language = xmlLang;
            }
            catch (CultureNotFoundException)
            {
                SetCultureAndLanguageToDefault();
            }
        }

        foreach (object? child in BottomBarButtons.Children)
            if (child is LanguagePicker languagePicker)
                languagePicker.Select(selectedILanguage.LanguageTag);

        foreach (MenuItem menuItem in LanguageMenuItem.Items)
            menuItem.IsChecked = false;

        clickedMenuItem.IsChecked = true;
    }

    private void LoadGrabTemplateMenuItems(MenuItem grabTemplateMenuItem)
    {
        // Remember which template (if any) was previously selected
        GrabTemplate? previouslySelected = grabTemplateMenuItem.Items
            .OfType<MenuItem>()
            .FirstOrDefault(m => m.IsChecked && m.Tag is GrabTemplate)
            ?.Tag as GrabTemplate;

        grabTemplateMenuItem.Items.Clear();

        MenuItem noneItem = new()
        {
            Header = "(None)",
            IsCheckable = true,
            IsChecked = previouslySelected is null,
            StaysOpenOnClick = true,
        };
        noneItem.Click += GrabTemplateMenuItem_Click;
        grabTemplateMenuItem.Items.Add(noneItem);

        foreach (GrabTemplate template in GrabTemplateManager.GetAllTemplates())
        {
            MenuItem templateMenuItem = new()
            {
                Header = template.Name,
                IsCheckable = true,
                IsChecked = previouslySelected?.Id == template.Id,
                Tag = template,
                StaysOpenOnClick = true,
            };
            templateMenuItem.Click += GrabTemplateMenuItem_Click;
            grabTemplateMenuItem.Items.Add(templateMenuItem);
        }
    }

    private void GrabTemplateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem clickedItem)
            return;

        foreach (MenuItem item in GrabTemplateMenuItem.Items)
            item.IsChecked = false;

        clickedItem.IsChecked = true;
    }

    private async void LoadLanguageMenuItems(MenuItem captureMenuItem)
    {
        if (captureMenuItem.Items.Count > 0)
            return;

        bool usingTesseract = DefaultSettings.UseTesseract && TesseractHelper.CanLocateTesseractExe();
        List<ILanguage> availableLanguages = await CaptureLanguageUtilities.GetCaptureLanguagesAsync(usingTesseract);
        availableLanguages = [.. availableLanguages.Where(CaptureLanguageUtilities.IsStaticImageCompatible)];
        int selectedIndex = CaptureLanguageUtilities.FindPreferredLanguageIndex(
            availableLanguages,
            DefaultSettings.LastUsedLang,
            LanguageUtilities.GetOCRLanguage());

        for (int i = 0; i < availableLanguages.Count; i++)
        {
            ILanguage language = availableLanguages[i];
            MenuItem languageMenuItem = new()
            {
                Header = language.DisplayName,
                Tag = language,
                IsCheckable = true,
                IsChecked = i == selectedIndex,
                StaysOpenOnClick = true,
            };
            languageMenuItem.Click += LanguageMenuItem_Click;
            captureMenuItem.Items.Add(languageMenuItem);
        }
    }

    private void LoadRecentTextHistory()
    {
        List<HistoryInfo> grabsHistories = Singleton<HistoryService>.Instance.GetEditWindows();
        grabsHistories = [.. grabsHistories.OrderByDescending(x => x.CaptureDateTime).Take(10)];

        ClearRecentTextMenuItems();

        if (grabsHistories.Count < 1)
        {
            OpenRecentMenuItem.IsEnabled = false;
            return;
        }

        OpenRecentMenuItem.IsEnabled = true;

        foreach (HistoryInfo history in grabsHistories)
        {
            MenuItem menuItem = new() { Tag = history.ID };
            menuItem.Click += RecentTextHistoryMenuItem_Click;

            if (Session.Text == history.TextContent)
                menuItem.IsEnabled = false;

            string snippet = history.TextContent.Trim().Replace("\t", " ").MakeStringSingleLine().Truncate(40);
            menuItem.Header = $"{history.CaptureDateTime.Humanize().Trim()} | {snippet}";
            menuItem.Icon = new SymbolIcon
            {
                Symbol = history.EditorMode switch
                {
                    EtwEditorMode.Spreadsheet => SymbolRegular.Table24,
                    EtwEditorMode.Markdown => SymbolRegular.Markdown20,
                    EtwEditorMode.Code => SymbolRegular.Code24,
                    _ => SymbolRegular.TextT24,
                }
            };
            OpenRecentMenuItem.Items.Add(menuItem);
        }
    }

    private void RecentTextHistoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string historyId)
            return;

        HistoryInfo? selectedHistory = Singleton<HistoryService>.Instance.GetTextHistoryById(historyId);

        if (selectedHistory is null)
        {
            menuItem.IsEnabled = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(Session.Text))
        {
            ResetSpreadsheetUndoHistory();
            Session.Text = selectedHistory.TextContent;
            tableDocument = EditTextTableDocument.TryDeserialize(selectedHistory.EditTextTableDocumentJson);
            editorMode = selectedHistory.EditorMode;
            SetEditorMode(editorMode);
            return;
        }

        EditTextWindow etw = new(selectedHistory);
        etw.Show();
    }

    private void ClearRecentTextMenuItems()
    {
        foreach (object item in OpenRecentMenuItem.Items)
        {
            if (item is MenuItem oldItem)
                oldItem.Click -= RecentTextHistoryMenuItem_Click;
        }
        OpenRecentMenuItem.Items.Clear();
    }

    private void ClearLanguageMenuItems()
    {
        foreach (object item in LanguageMenuItem.Items)
        {
            if (item is MenuItem oldItem)
                oldItem.Click -= LanguageMenuItem_Click;
        }
        LanguageMenuItem.Items.Clear();
    }

    private async void MenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        LoadRecentTextHistory();
        await Singleton<HistoryService>.Instance.PopulateMenuItemWithRecentGrabs(OpenRecentGrabsMenuItem);
        Singleton<HistoryService>.Instance.PopulateMenuItemWithRecentFiles(OpenRecentPdfsMenuItem, this);
    }

    private void WindowMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        OpenLastAsGrabFrameMenuItem.IsEnabled = Singleton<HistoryService>.Instance.HasAnyRecentGrabs();
    }
}
