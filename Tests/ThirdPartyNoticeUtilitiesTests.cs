using System.Linq;
using Text_Grab.Utilities;

namespace Tests;

public class ThirdPartyNoticeUtilitiesTests
{
    [Fact]
    public void PackageCatalog_CoversAllDirectReferences()
    {
        string[] expectedPackageIds =
        [
            "BenchmarkDotNet",
            "CliWrap",
            "coverlet.collector",
            "Dapplo.Windows.User32",
            "Humanizer.Core",
            "Magick.NET-Q16-AnyCPU",
            "Magick.NET.SystemDrawing",
            "Magick.NET.SystemWindowsMedia",
            "Markdig",
            "Microsoft.NET.Test.Sdk",
            "Microsoft.Toolkit.Uwp.Notifications",
            "Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers",
            "Microsoft.WindowsAppSDK.AI",
            "Microsoft.WindowsAppSDK.Foundation",
            "Microsoft.WindowsAppSDK.Runtime",
            "Microsoft.WindowsAppSDK.WinUI",
            "NCalcAsync",
            "PdfPig",
            "UnitsNet",
            "WPF-UI",
            "WPF-UI.Tray",
            "xunit.runner.visualstudio",
            "Xunit.StaFact",
            "xunit.v3",
            "ZXing.Net",
            "ZXing.Net.Bindings.Windows.Compatibility"
        ];

        string[] actualPackageIds = ThirdPartyNoticeUtilities.Packages
            .Select(package => package.PackageId)
            .OrderBy(packageId => packageId)
            .ToArray();

        Assert.Equal(expectedPackageIds.OrderBy(packageId => packageId), actualPackageIds);
    }

    [Fact]
    public void PackageCatalog_ProvidesProjectAndNoticeLinksForEveryEntry()
    {
        Assert.All(
            ThirdPartyNoticeUtilities.Packages,
            package =>
            {
                Assert.True(Uri.IsWellFormedUriString(package.ProjectUrl, UriKind.Absolute), package.PackageId);
                Assert.False(string.IsNullOrWhiteSpace(package.NoticeTarget), package.PackageId);
            });
    }

    [Fact]
    public void PackageCatalog_UsesLocalNoticeForMarkdig()
    {
        var package = ThirdPartyNoticeUtilities.Packages
            .SingleOrDefault(package => package.PackageId == "Markdig");

        Assert.NotNull(package);
        Assert.True(package.NoticeIsLocal);
        Assert.Equal(@"ThirdPartyNotices\licenses\Markdig-license.txt", package.NoticeTarget);
    }
}
