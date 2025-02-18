using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Utilities;
using Windows.Globalization;

namespace Text_Grab.Controls;

public partial class LanguagePicker : UserControl
{
    public ObservableCollection<Language> Languages = [];

    public event RoutedEventHandler? LanguageChanged;

    public Language SelectedLanguage
    {
        get { return (Language)GetValue(SelectedLanguageProperty); }
        set { SetValue(SelectedLanguageProperty, value); }
    }

    public static readonly DependencyProperty SelectedLanguageProperty =
        DependencyProperty.Register("SelectedLanguage", typeof(Language), typeof(LanguagePicker), new PropertyMetadata(null));

    public LanguagePicker()
    {
        DataContext = this;
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Languages.Clear();

        Language currentLanguage = LanguageUtilities.GetCurrentInputLanguage();

        int selectedIndex = 0;
        int i = 0;
        foreach (Language language in LanguageUtilities.GetAllLanguages())
        {
            if (language.LanguageTag == currentLanguage.LanguageTag)
                selectedIndex = i;

            MainComboBox.Items.Add(language);
            i++;
        }

        MainComboBox.SelectedIndex = selectedIndex;
    }

    private void MainComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LanguageChanged?.Invoke(this, new RoutedEventArgs());
    }
}
