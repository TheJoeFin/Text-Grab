using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

public static class ThirdPartyNoticeUtilities
{
    public const string BuiltWithFileName = "BUILT-WITH.md";
    public const string NoticesDirectoryName = "ThirdPartyNotices";

    private const string MarkdigNoticePath = @"ThirdPartyNotices\licenses\Markdig-license.txt";
    private const string WindowsAppSdkNoticePath = @"ThirdPartyNotices\licenses\Microsoft.WindowsAppSDK-license.txt";
    private const string DiagnosticsHubNoticePath = @"ThirdPartyNotices\licenses\Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers-LICENSE.md";

    public static IReadOnlyList<ThirdPartyPackageInfo> Packages { get; } =
    [
        new("CliWrap", "3.10.1", "App", "MIT", "https://github.com/Tyrrrz/CliWrap", "https://github.com/Tyrrrz/CliWrap/blob/master/License.txt"),
        new("Dapplo.Windows.User32", "2.0.89", "App", "MIT", "https://github.com/dapplo/Dapplo.Windows", "https://github.com/dapplo/Dapplo.Windows/blob/master/LICENSE"),
        new("Humanizer.Core", "3.0.10", "App", "MIT", "https://github.com/Humanizr/Humanizer", "https://github.com/Humanizr/Humanizer/blob/main/license.txt"),
        new("Magick.NET-Q16-AnyCPU", "14.12.0", "App", "Apache-2.0", "https://github.com/dlemstra/Magick.NET", "https://github.com/dlemstra/Magick.NET/blob/main/License.txt"),
        new("Magick.NET.SystemDrawing", "8.0.20", "App", "Apache-2.0", "https://github.com/dlemstra/Magick.NET", "https://github.com/dlemstra/Magick.NET/blob/main/License.txt"),
        new("Magick.NET.SystemWindowsMedia", "8.0.20", "App", "Apache-2.0", "https://github.com/dlemstra/Magick.NET", "https://github.com/dlemstra/Magick.NET/blob/main/License.txt"),
        new("Markdig", "1.1.3", "App", "BSD-2-Clause", "https://github.com/xoofx/markdig", MarkdigNoticePath, true, "Bundled to satisfy BSD-2-Clause binary redistribution notice requirements."),
        new("Microsoft.Toolkit.Uwp.Notifications", "7.1.3", "App", "MIT", "https://github.com/CommunityToolkit/WindowsCommunityToolkit", "https://github.com/CommunityToolkit/WindowsCommunityToolkit/blob/main/License.md"),
        new("Microsoft.WindowsAppSDK.AI", "1.8.70", "App", "Microsoft license terms", "https://github.com/microsoft/windowsappsdk", WindowsAppSdkNoticePath, true, "Package ships Microsoft Windows App SDK license terms."),
        new("Microsoft.WindowsAppSDK.Foundation", "1.8.260415000", "App", "Microsoft license terms", "https://github.com/microsoft/windowsappsdk", WindowsAppSdkNoticePath, true, "Package ships Microsoft Windows App SDK license terms."),
        new("Microsoft.WindowsAppSDK.Runtime", "1.8.260416003", "App", "Microsoft license terms", "https://github.com/microsoft/windowsappsdk", WindowsAppSdkNoticePath, true, "Package ships Microsoft Windows App SDK license terms."),
        new("Microsoft.WindowsAppSDK.WinUI", "1.8.260415005", "App", "Microsoft license terms", "https://github.com/microsoft/windowsappsdk", WindowsAppSdkNoticePath, true, "Package ships Microsoft Windows App SDK license terms."),
        new("NCalcAsync", "5.12.0", "App, Tests", "MIT", "https://github.com/ncalc/ncalc", "https://github.com/ncalc/ncalc/blob/master/LICENSE", false, "Shared by the application and the test project."),
        new("PdfPig", "0.1.14", "App", "Apache-2.0", "https://github.com/UglyToad/PdfPig", "https://github.com/UglyToad/PdfPig/blob/master/LICENSE"),
        new("UnitsNet", "5.75.0", "App", "MIT-0", "https://github.com/angularsen/UnitsNet", "https://github.com/angularsen/UnitsNet/blob/master/LICENSE"),
        new("WPF-UI", "4.2.1", "App", "MIT", "https://github.com/lepoco/wpfui", "https://github.com/lepoco/wpfui/blob/main/LICENSE"),
        new("WPF-UI.Tray", "4.2.1", "App", "MIT", "https://github.com/lepoco/wpfui", "https://github.com/lepoco/wpfui/blob/main/LICENSE"),
        new("ZXing.Net", "0.16.11", "App", "Apache-2.0", "https://github.com/micjahn/ZXing.Net", "https://github.com/micjahn/ZXing.Net/blob/master/COPYING"),
        new("ZXing.Net.Bindings.Windows.Compatibility", "0.16.14", "App", "Apache-2.0", "https://github.com/micjahn/ZXing.Net", "https://github.com/micjahn/ZXing.Net/blob/master/COPYING"),
        new("BenchmarkDotNet", "0.15.8", "Tests", "MIT", "https://github.com/dotnet/BenchmarkDotNet", "https://github.com/dotnet/BenchmarkDotNet/blob/master/LICENSE.md", false, "Test-only dependency."),
        new("coverlet.collector", "10.0.0", "Tests", "MIT", "https://github.com/coverlet-coverage/coverlet", "https://github.com/coverlet-coverage/coverlet/blob/master/LICENSE", false, "Test-only dependency."),
        new("Microsoft.NET.Test.Sdk", "18.4.0", "Tests", "MIT", "https://github.com/microsoft/vstest", "https://github.com/microsoft/vstest/blob/main/LICENSE", false, "Test-only dependency."),
        new("Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers", "18.7.37220.1", "Tests", "Microsoft license terms", "https://learn.microsoft.com/visualstudio/profiling/", DiagnosticsHubNoticePath, true, "Visual Studio benchmarking tooling; test-only dependency."),
        new("xunit.runner.visualstudio", "3.1.5", "Tests", "Apache-2.0", "https://github.com/xunit/visualstudio.xunit", "https://github.com/xunit/visualstudio.xunit/blob/main/License.txt", false, "Test-only dependency."),
        new("Xunit.StaFact", "3.0.13", "Tests", "MS-PL", "https://github.com/AArnott/Xunit.StaFact", "https://github.com/AArnott/Xunit.StaFact/blob/main/LICENSE", false, "Test-only dependency."),
        new("xunit.v3", "3.2.2", "Tests", "Apache-2.0", "https://github.com/xunit/xunit", "https://github.com/xunit/xunit/blob/main/LICENSE", false, "Test-only dependency."),
    ];

    public static string? GetBuiltWithFilePath()
    {
        string? executableDirectory = Path.GetDirectoryName(FileUtilities.GetExePath());
        return string.IsNullOrWhiteSpace(executableDirectory)
            ? null
            : Path.Combine(executableDirectory, BuiltWithFileName);
    }

    public static string? GetNoticesDirectoryPath()
    {
        string? executableDirectory = Path.GetDirectoryName(FileUtilities.GetExePath());
        return string.IsNullOrWhiteSpace(executableDirectory)
            ? null
            : Path.Combine(executableDirectory, NoticesDirectoryName);
    }

    public static string? GetNoticeTarget(ThirdPartyPackageInfo package)
    {
        if (!package.NoticeIsLocal)
            return package.NoticeTarget;

        string? executableDirectory = Path.GetDirectoryName(FileUtilities.GetExePath());
        return string.IsNullOrWhiteSpace(executableDirectory)
            ? null
            : Path.Combine(executableDirectory, package.NoticeTarget);
    }

    public static void OpenBuiltWithFile() => OpenTarget(GetBuiltWithFilePath());

    public static void OpenNoticesDirectory() => OpenTarget(GetNoticesDirectoryPath());

    public static void OpenNoticeFile(ThirdPartyPackageInfo package) => OpenTarget(GetNoticeTarget(package));

    public static void OpenProjectUrl(ThirdPartyPackageInfo package) => OpenTarget(package.ProjectUrl);

    private static void OpenTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return;

        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }
}
