using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Controls;

public partial class LanguagePicker : UserControl
{
    public ObservableCollection<ILanguage> Languages { get; } = [];

    public event RoutedEventHandler? LanguageChanged;

    public ILanguage SelectedLanguage
    {
        get { return (ILanguage)GetValue(SelectedLanguageProperty); }
        set { SetValue(SelectedLanguageProperty, value); }
    }

    public static readonly DependencyProperty SelectedLanguageProperty =
        DependencyProperty.Register("SelectedLanguage", typeof(ILanguage), typeof(LanguagePicker), new PropertyMetadata(null));

    public LanguagePicker()
    {
        DataContext = this;
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Languages.Clear();

        ILanguage currentSelectedLanguage = LanguageUtilities.GetOCRLanguage();

        // get current keyboard language
        CultureInfo keyboardLanguage = InputLanguageManager.Current.CurrentInputLanguage;

        // The challenge here is that UI Automation and Windows AI support any langauage
        // since this picker will set the spell checker language and stuff like that
        // it needs to represent real languages and not just OCR engine target languages
        // As new models are supported they will need to be caught and filtered here too

        if (currentSelectedLanguage is UiAutomationLang or WindowsAiLang)
            currentSelectedLanguage = new GlobalLang(keyboardLanguage.Name);

        int selectedIndex = 0;
        int i = 0;
        foreach (ILanguage langFromUtil in LanguageUtilities.GetAllLanguages())
        {
            if (langFromUtil is UiAutomationLang or WindowsAiLang)
                continue;

            Languages.Add(langFromUtil);
            if (langFromUtil.LanguageTag == currentSelectedLanguage.LanguageTag)
                selectedIndex = i;
            i++;
        }

        if (Languages.Count > 0 && selectedIndex < Languages.Count)
            MainComboBox.SelectedIndex = selectedIndex;
        else if (Languages.Count > 0)
            MainComboBox.SelectedIndex = 0;
    }

    private void MainComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainComboBox.SelectedItem is ILanguage selectedILanguage)
        {
            SelectedLanguage = selectedILanguage;
            CaptureLanguageUtilities.PersistSelectedLanguage(selectedILanguage);
            LanguageChanged?.Invoke(this, new RoutedEventArgs());
        }
    }

    internal void Select(string languageTag)
    {
        int i = 0;
        foreach (ILanguage language in Languages)
        {
            if (language.LanguageTag == languageTag)
            {
                MainComboBox.SelectedIndex = i;
                SelectedLanguage = language;
                break;
            }
            i++;
        }
    }
}
