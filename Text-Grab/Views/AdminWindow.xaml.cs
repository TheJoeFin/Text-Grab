using Microsoft.Dism;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Text_Grab.Pages;
using Text_Grab.Utilities;

namespace Text_Grab.Views;

public partial class AdminWindow : Wpf.Ui.Controls.FluentWindow
{
    public AdminWindow()
    {
        InitializeComponent();

        AllWindowsLanguagesComboBox.Items.Clear();
        foreach (string textName in WindowsLanguageUtilities.AllLanguages)
        {
            CultureInfo languageCulture = new(textName);
            string paddedTextName = textName.PadRight(12);
            LangListItem langListItem = new()
            {
                LeftPart = paddedTextName,
                RightPart = languageCulture.DisplayName
            };
            AllWindowsLanguagesComboBox.Items.Add(langListItem);
        }

        DismApi.Initialize(DismLogLevel.LogErrorsWarnings);
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        DismApi.Shutdown();
        Application.Current.Shutdown();
    }

    private async void InstalWindowsLangButton_Click(object sender, RoutedEventArgs e)
    {
        if (AllWindowsLanguagesComboBox.SelectedItem is not LangListItem pickedLanguageFile)
            return;

        using DismSession session = DismApi.OpenOnlineSession();

        DismCapability? langToInstall = DismApi.GetCapabilities(session).FirstOrDefault(cap => cap.Name.Contains(pickedLanguageFile.LeftPart.Trim()));

        if (langToInstall is null)
        {
            foreach (DismCapability cap in DismApi.GetCapabilities(session))
                if (cap.Name.Contains("Language.OCR~~~"))
                {
                    DismOutputTextBlock.Text += $"{Environment.NewLine}Dism capability: {cap.Name}";

                    if (cap.Name.Contains(pickedLanguageFile.LeftPart))
                        DismOutputTextBlock.Text += $"<--- Found {pickedLanguageFile.LeftPart}";
                }

            DismOutputTextBlock.Text += $"{Environment.NewLine}Language: {pickedLanguageFile.LeftPart} not found.";
            return;
        }

        DismProgressBar.Visibility = Visibility.Visible;

        DismProgressCallback progressCallback = new(progress =>
        {
            DismProgressBar.Maximum = progress.Total;
            DismProgressBar.Value = progress.Current;
        });

        await Task.Run(() =>
        {
            DismApi.AddCapability(session, langToInstall.Name, false, null, progressCallback, null);
        });

        DismProgressBar.Visibility = Visibility.Collapsed;
    }

    private void LoadLanguages_Click(object sender, RoutedEventArgs e)
    {
        using DismSession session = DismApi.OpenOnlineSession();

        var caps = DismApi.GetCapabilities(session);

        foreach (DismCapability cap in DismApi.GetCapabilities(session))
        {
            string capName = cap.Name;
            if (!capName.StartsWith("Language.OCR~~~"))
                continue;
            if (cap.State != DismPackageFeatureState.Installed)
                continue;
            string localeName = capName["Language.OCR~~~".Length..capName.LastIndexOf('~')];
            CultureInfo culture = new(localeName);
            DismOutputTextBlock.Text += $"{Environment.NewLine}{localeName} - {culture.DisplayName} - {capName}";
        }
    }
}
