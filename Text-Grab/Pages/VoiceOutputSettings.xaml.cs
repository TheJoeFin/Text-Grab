using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.Media.SpeechSynthesis;

namespace Text_Grab.Pages;

public partial class VoiceOutputSettings : Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private bool _loaded = false;

    public VoiceOutputSettings()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        SpeakInsteadOfToastToggle.IsChecked = DefaultSettings.SpeakInsteadOfToast;

        VoiceComboBox.Items.Clear();
        foreach (VoiceInformation voice in SpeechSynthesizer.AllVoices.OrderBy(v => v.DisplayName))
            VoiceComboBox.Items.Add(voice.DisplayName);

        string savedVoice = DefaultSettings.TtsVoiceName;
        if (!string.IsNullOrEmpty(savedVoice) && VoiceComboBox.Items.Contains(savedVoice))
            VoiceComboBox.SelectedItem = savedVoice;
        else
            VoiceComboBox.SelectedIndex = 0;

        TtsSpeakWordLimitTextBox.Text = DefaultSettings.TtsSpeakWordLimit.ToString();

        _loaded = true;
    }

    private void SpeakInsteadOfToastToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
            return;

        DefaultSettings.SpeakInsteadOfToast = SpeakInsteadOfToastToggle.IsChecked is true;
        DefaultSettings.Save();
    }

    private void VoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded)
            return;

        if (VoiceComboBox.SelectedItem is string voiceName)
        {
            DefaultSettings.TtsVoiceName = voiceName;
            DefaultSettings.Save();
        }
    }

    private void TtsSpeakWordLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded)
            return;

        if (int.TryParse(TtsSpeakWordLimitTextBox.Text, out int parsedValue) && parsedValue > 0)
        {
            DefaultSettings.TtsSpeakWordLimit = parsedValue;
            DefaultSettings.Save();
            TtsWordLimitError.Visibility = Visibility.Collapsed;
        }
        else
        {
            TtsWordLimitError.Visibility = Visibility.Visible;
        }
    }

    private void PreviewVoiceButton_Click(object sender, RoutedEventArgs e)
    {
        Singleton<TtsService>.Instance.Speak("Hello, this is a preview of the selected voice.");
    }
}
