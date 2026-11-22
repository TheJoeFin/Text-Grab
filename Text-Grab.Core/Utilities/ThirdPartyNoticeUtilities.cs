using System.Collections.Generic;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

/// <summary>
/// Pure package catalog. Resolving notice/license file paths against the running
/// executable's location and opening them needs <c>FileUtilities.GetExePath()</c>, so those
/// methods stay behind in <c>Text-Grab/Utilities/ThirdPartyNoticeLauncher.cs</c>.
/// </summary>
public static class ThirdPartyNoticeUtilities
{
    public const string BuiltWithFileName = "BUILT-WITH.md";
    public const string NoticesDirectoryName = "ThirdPartyNotices";

    private const string MarkdigNoticePath = @"ThirdPartyNotices\licenses\Markdig-license.txt";
    private const string TesseractNoticePath = @"ThirdPartyNotices\licenses\Tesseract\tesseract.txt";
    private const string WindowsAppSdkNoticePath = @"ThirdPartyNotices\licenses\Microsoft.WindowsAppSDK-license.txt";
    private const string DiagnosticsHubNoticePath = @"ThirdPartyNotices\licenses\Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers-LICENSE.md";

    private const string DotNetRuntimeProjectUrl = "https://github.com/dotnet/runtime";
    private const string DotNetRuntimeLicenseUrl = "https://github.com/dotnet/runtime/blob/main/LICENSE.TXT";
    private const string RecognizersProjectUrl = "https://github.com/microsoft/Recognizers-Text";
    private const string RecognizersLicenseUrl = "https://github.com/microsoft/Recognizers-Text/blob/master/LICENSE";
    private const string RecognizersNotes = "Culture-aware entity recognizers.";
    private const string WindowsAppSdkProjectUrl = "https://github.com/microsoft/windowsappsdk";
    private const string WindowsAppSdkNotes = "Package ships Microsoft Windows App SDK license terms.";

    public static IReadOnlyList<ThirdPartyPackageInfo> Packages { get; } =
    [
        new("AvalonEdit", "6.3.1.120", "App", "MIT", "https://github.com/icsharpcode/AvalonEdit", "https://github.com/icsharpcode/AvalonEdit/blob/master/LICENSE", false, "Code mode editor in the Edit Text Window."),
        new("CliWrap", "3.10.5", "App", "MIT", "https://github.com/Tyrrrz/CliWrap", "https://github.com/Tyrrrz/CliWrap/blob/master/License.txt"),
        new("CommunityToolkit.Mvvm", "8.4.0", "App", "MIT", "https://github.com/CommunityToolkit/dotnet", "https://github.com/CommunityToolkit/dotnet/blob/main/License.md", false, "View models of the Edit Text Window's panes."),
        new("Dapplo.Windows.User32", "3.23.3", "App", "MIT", "https://github.com/dapplo/Dapplo.Windows", "https://github.com/dapplo/Dapplo.Windows/blob/master/LICENSE"),
        new("Dirkster.AvalonDock", "5.0.2", "App", "MS-PL", "https://github.com/Dirkster99/AvalonDock", "https://github.com/Dirkster99/AvalonDock/blob/master/LICENSE", false, "Hosts the Edit Text Window's panes. Includes its Dirkster.AvalonDock.Core package, under the same license."),
        new("Humanizer.Core", "3.0.10", "App", "MIT", "https://github.com/Humanizr/Humanizer", "https://github.com/Humanizr/Humanizer/blob/main/license.txt"),
        new("Magick.NET-Q16-AnyCPU", "15.1.0", "App", "Apache-2.0", "https://github.com/dlemstra/Magick.NET", "https://github.com/dlemstra/Magick.NET/blob/main/License.txt"),
        new("Magick.NET.SystemDrawing", "9.0.1", "App", "Apache-2.0", "https://github.com/dlemstra/Magick.NET", "https://github.com/dlemstra/Magick.NET/blob/main/License.txt"),
        new("Magick.NET.SystemWindowsMedia", "9.0.1", "App", "Apache-2.0", "https://github.com/dlemstra/Magick.NET", "https://github.com/dlemstra/Magick.NET/blob/main/License.txt"),
        new("Markdig", "1.6.0", "App", "BSD-2-Clause", "https://github.com/xoofx/markdig", MarkdigNoticePath, true, "Bundled to satisfy BSD-2-Clause binary redistribution notice requirements."),
        new("Mermaider", "0.17.0", "App", "MIT", "https://github.com/nullean/mermaider", @"ThirdPartyNotices\licenses\Mermaider-license.txt", true, "Native Mermaid diagram rendering, including its Sugiyama layout engine."),
        new("Microsoft.Extensions.DependencyInjection", "10.0.15", "App", "MIT", DotNetRuntimeProjectUrl, DotNetRuntimeLicenseUrl, false, "The app's service container."),
        new("Microsoft.Extensions.DependencyInjection.Abstractions", "10.0.15", "App", "MIT", DotNetRuntimeProjectUrl, DotNetRuntimeLicenseUrl, false, "Service container abstractions used by the core libraries."),
        new("Microsoft.Recognizers.Text.Choice", "1.8.13", "App", "MIT", RecognizersProjectUrl, RecognizersLicenseUrl, false, RecognizersNotes),
        new("Microsoft.Recognizers.Text.DateTime", "1.8.13", "App", "MIT", RecognizersProjectUrl, RecognizersLicenseUrl, false, RecognizersNotes),
        new("Microsoft.Recognizers.Text.Number", "1.8.13", "App", "MIT", RecognizersProjectUrl, RecognizersLicenseUrl, false, RecognizersNotes),
        new("Microsoft.Recognizers.Text.NumberWithUnit", "1.8.13", "App", "MIT", RecognizersProjectUrl, RecognizersLicenseUrl, false, RecognizersNotes),
        new("Microsoft.Recognizers.Text.Sequence", "1.8.13", "App", "MIT", RecognizersProjectUrl, RecognizersLicenseUrl, false, RecognizersNotes),
        new("Microsoft.Toolkit.Uwp.Notifications", "7.1.3", "App", "MIT", "https://github.com/CommunityToolkit/WindowsCommunityToolkit", "https://github.com/CommunityToolkit/WindowsCommunityToolkit/blob/main/License.md"),
        new("Microsoft.WindowsAppSDK.AI", "2.6.5", "App", "Microsoft license terms", WindowsAppSdkProjectUrl, WindowsAppSdkNoticePath, true, WindowsAppSdkNotes),
        new("Microsoft.WindowsAppSDK.Foundation", "2.6.5", "App", "Microsoft license terms", WindowsAppSdkProjectUrl, WindowsAppSdkNoticePath, true, WindowsAppSdkNotes),
        new("Microsoft.WindowsAppSDK.InteractiveExperiences", "2.6.5", "App", "Microsoft license terms", WindowsAppSdkProjectUrl, WindowsAppSdkNoticePath, true, WindowsAppSdkNotes),
        new("Microsoft.WindowsAppSDK.Runtime", "2.6.5", "App", "Microsoft license terms", WindowsAppSdkProjectUrl, WindowsAppSdkNoticePath, true, WindowsAppSdkNotes),
        new("Microsoft.WindowsAppSDK.WinUI", "2.6.5", "App", "Microsoft license terms", WindowsAppSdkProjectUrl, WindowsAppSdkNoticePath, true, WindowsAppSdkNotes),
        new("NAudio", "3.3.0", "App", "MIT", "https://github.com/naudio/NAudio", "https://github.com/naudio/NAudio/blob/master/license.txt", false, "Audio capture and decoding for transcription."),
        new("NCalcAsync", "7.4.0", "App, Tests", "MIT", "https://github.com/ncalc/ncalc", "https://github.com/ncalc/ncalc/blob/master/LICENSE", false, "Shared by the application and the test project."),
        new("PdfPig", "0.1.16", "App", "Apache-2.0", "https://github.com/UglyToad/PdfPig", "https://github.com/UglyToad/PdfPig/blob/master/LICENSE"),
        new("Svg", "3.4.8", "App", "MIT", "https://github.com/svg-net/SVG", "https://github.com/svg-net/SVG/blob/master/license.txt", false, "Renders SVG images in the Markdown editor."),
        new("System.Configuration.ConfigurationManager", "10.0.15", "App", "MIT", DotNetRuntimeProjectUrl, DotNetRuntimeLicenseUrl, false, "Application settings support in the core library."),
        new("Tesseract.CrossPlatform", "5.5.2.200", "App", "Apache-2.0", "https://github.com/tesseract-ocr/tesseract", TesseractNoticePath, true, "In-process Tesseract 5.5 OCR (with Leptonica and other native libraries). Third-party license texts, and the tessdata_fast English model license, are in ThirdPartyNotices\\licenses\\Tesseract."),
        new("UnitsNet", "5.75.1", "App", "MIT-0", "https://github.com/angularsen/UnitsNet", "https://github.com/angularsen/UnitsNet/blob/master/LICENSE"),
        new("Vortice.Direct3D11", "3.8.3", "App", "MIT", "https://github.com/amerkoleci/Vortice.Windows", "https://github.com/amerkoleci/Vortice.Windows/blob/main/LICENSE", false, "Direct3D 11 interop for HDR-aware screen capture."),
        new("Vortice.DXGI", "3.8.3", "App", "MIT", "https://github.com/amerkoleci/Vortice.Windows", "https://github.com/amerkoleci/Vortice.Windows/blob/main/LICENSE", false, "DXGI interop for HDR-aware screen capture."),
        new("Whisper.net", "1.9.1", "App", "MIT", "https://github.com/sandrohanea/whisper.net", "https://github.com/sandrohanea/whisper.net/blob/main/LICENSE", false, "Local speech-to-text transcription."),
        new("Whisper.net.Runtime", "1.9.1", "App", "MIT", "https://github.com/sandrohanea/whisper.net", "https://github.com/sandrohanea/whisper.net/blob/main/LICENSE", false, "Native whisper.cpp runtime for Whisper.net."),
        new("WPF-UI", "4.3.0", "App", "MIT", "https://github.com/lepoco/wpfui", "https://github.com/lepoco/wpfui/blob/main/LICENSE"),
        new("WPF-UI.Tray", "4.3.0", "App", "MIT", "https://github.com/lepoco/wpfui", "https://github.com/lepoco/wpfui/blob/main/LICENSE"),
        new("WpfMath", "2.1.0", "App", "MIT; Knuth; OFL-1.1", "https://github.com/ForNeVeR/xaml-math", @"ThirdPartyNotices\licenses\WpfMath-license.txt", true, "Native math rendering; includes XAML-Math shared code and font notices."),
        new("ZXing.Net", "0.16.11", "App", "Apache-2.0", "https://github.com/micjahn/ZXing.Net", "https://github.com/micjahn/ZXing.Net/blob/master/COPYING"),
        new("ZXing.Net.Bindings.Windows.Compatibility", "0.16.17", "App", "Apache-2.0", "https://github.com/micjahn/ZXing.Net", "https://github.com/micjahn/ZXing.Net/blob/master/COPYING"),
        new("BenchmarkDotNet", "0.15.8", "Tests", "MIT", "https://github.com/dotnet/BenchmarkDotNet", "https://github.com/dotnet/BenchmarkDotNet/blob/master/LICENSE.md", false, "Test-only dependency."),
        new("Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers", "18.13.37903.2", "Tests", "Microsoft license terms", "https://learn.microsoft.com/visualstudio/profiling/", DiagnosticsHubNoticePath, true, "Visual Studio benchmarking tooling; test-only dependency."),
        new("Xunit.StaFact", "4.0.24", "Tests", "MS-PL", "https://github.com/AArnott/Xunit.StaFact", "https://github.com/AArnott/Xunit.StaFact/blob/main/LICENSE", false, "Test-only dependency."),
        new("xunit.v3", "4.0.3", "Tests", "Apache-2.0", "https://github.com/xunit/xunit", "https://github.com/xunit/xunit/blob/main/LICENSE", false, "Test-only dependency."),
    ];
}
