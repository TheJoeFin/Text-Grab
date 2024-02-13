using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Interfaces;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for LanguageSettings.xaml
/// </summary>
public partial class LanguageSettings : Page
{
    private bool usingTesseract;

    public LanguageSettings()
    {
        InitializeComponent();
        usingTesseract = Settings.Default.UseTesseract && TesseractHelper.CanLocateTesseractExe();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        LoadWindowsLanguages();

        if (usingTesseract)
            await LoadTesseractContent();
    }

    private void LoadWindowsLanguages()
    {
        WindowsLanguagesListView.Items.Clear();
        List<Language> possibleOCRLanguages = OcrEngine.AvailableRecognizerLanguages.ToList();
        foreach (Language language in possibleOCRLanguages)
            WindowsLanguagesListView.Items.Add(language);
    }

    private async Task LoadTesseractContent()
    {
        TesseractLanguagesListView.Items.Clear();
        List<ILanguage> tesseractLanguages = await TesseractHelper.TesseractLanguages();
        foreach (ILanguage iLang in tesseractLanguages)
            TesseractLanguagesListView.Items.Add(iLang);

        AllLanguagesComboBox.Items.Clear();
        foreach (string textName in TesseractGitHubFileDownloader.tesseractTrainedDataFileNames)
        {
            bool isInstalled = false;
            foreach (ILanguage iLang2 in tesseractLanguages)
                if (iLang2.LanguageTag == textName.Split('.').First())
                    isInstalled = true;

            if (!isInstalled)
                AllLanguagesComboBox.Items.Add(textName);
        }
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(AllLanguagesComboBox.Text))
            return;

        string pickedLanguageFile = AllLanguagesComboBox.Text;
        string tesseractPath = Path.GetDirectoryName(Settings.Default.TesseractPath) ?? "c:\\";
        string tesseractFilePath = $"{tesseractPath}\\tessdata\\{pickedLanguageFile}";

        TesseractGitHubFileDownloader fileDownloader = new();
        await fileDownloader.DownloadFileAsync(pickedLanguageFile, tesseractFilePath);
        await LoadTesseractContent();
    }

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {

    }
}
