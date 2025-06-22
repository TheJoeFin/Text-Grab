using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Interfaces;
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

        ILanguage currentInputGlobalLang = LanguageUtilities.GetCurrentInputLanguage();

        int selectedIndex = 0;
        int i = 0;
        foreach (ILanguage langFromUtil in LanguageUtilities.GetAllLanguages())
        {
            Languages.Add(langFromUtil);
            if (langFromUtil.LanguageTag == currentInputGlobalLang.LanguageTag)
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
