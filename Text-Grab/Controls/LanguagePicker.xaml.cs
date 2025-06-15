using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Windows.Globalization; // Still needed for fallback or specific cases if any remain

namespace Text_Grab.Controls;

public partial class LanguagePicker : UserControl
{
    public ObservableCollection<ILanguage> Languages { get; } = []; // Changed Type

    public event RoutedEventHandler? LanguageChanged;

    public ILanguage SelectedLanguage // Changed Type
    {
        get { return (ILanguage)GetValue(SelectedLanguageProperty); } // Changed Type
        set { SetValue(SelectedLanguageProperty, value); }
    }

    public static readonly DependencyProperty SelectedLanguageProperty =
        DependencyProperty.Register("SelectedLanguage", typeof(ILanguage), typeof(LanguagePicker), new PropertyMetadata(null)); // Changed Type

    public LanguagePicker()
    {
        DataContext = this;
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Languages.Clear();

        ILanguage currentInputGlobalLang = LanguageUtilities.GetCurrentInputLanguage();

        int selectedIndex = 0;
        int i = 0;
        // LanguageUtilities.GetAllLanguages() returns IList<Language>, convert to ILanguage
        foreach (Language langFromUtil in LanguageUtilities.GetAllLanguages())
        {
            // Wrap Windows.Globalization.Language in a compatible ILanguage implementation (e.g., GlobalLang)
            ILanguage iLang = new GlobalLang(langFromUtil);
            Languages.Add(iLang);
            if (iLang.LanguageTag == currentInputGlobalLang.LanguageTag)
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
            LanguageChanged?.Invoke(this, new RoutedEventArgs());
        }
    }

    internal void Select(string ietfLanguageTag)
    {
        int i = 0;
        foreach (ILanguage language in Languages) // Iterate over the ILanguage collection
        {
            if (language.LanguageTag == ietfLanguageTag)
            {
                MainComboBox.SelectedIndex = i;
                SelectedLanguage = language;
                break;
            }
            i++;
        }
    }
}
