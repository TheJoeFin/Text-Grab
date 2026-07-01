using System.Collections.ObjectModel;
using System.Windows;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Views;

public partial class LicensesWindow : FluentWindow
{
    public ObservableCollection<ThirdPartyPackageInfo> Packages { get; } = [.. ThirdPartyNoticeUtilities.Packages];

    public LicensesWindow()
    {
        InitializeComponent();
        App.SetTheme();
        DataContext = this;
    }

    private void BuiltWithButton_Click(object sender, RoutedEventArgs e)
    {
        ThirdPartyNoticeUtilities.OpenBuiltWithFile();
    }

    private void NoticesFolderButton_Click(object sender, RoutedEventArgs e)
    {
        ThirdPartyNoticeUtilities.OpenNoticesDirectory();
    }

    private void NoticeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThirdPartyPackageInfo package })
            ThirdPartyNoticeUtilities.OpenNoticeFile(package);
    }

    private void ProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThirdPartyPackageInfo package })
            ThirdPartyNoticeUtilities.OpenProjectUrl(package);
    }
}
