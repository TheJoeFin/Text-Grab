# Built With

Text Grab depends on the direct NuGet packages listed below.

- **Versions** are intentionally omitted here. The project files are the single source of truth — see [`Text-Grab/Text-Grab.csproj`](Text-Grab/Text-Grab.csproj), [`Text-Grab.Core/Text-Grab.Core.csproj`](Text-Grab.Core/Text-Grab.Core.csproj), [`Text-Grab.Core.Windows/Text-Grab.Core.Windows.csproj`](Text-Grab.Core.Windows/Text-Grab.Core.Windows.csproj) and [`Tests/Tests.csproj`](Tests/Tests.csproj) for the exact version of each package.
- **Scope** identifies whether a package is used by the app, the tests, or both.
- **Notice** links to a bundled local notice file when one ships with Text Grab, or to the upstream project license file otherwise.
- **Project** links to the upstream project home or repository.
- Test-only packages are documented for completeness, but they are not part of normal end-user app builds.

The same package inventory is also available from the app's **About → Licenses** flow.

| Package | Scope | License | Notice | Project | Notes |
| --- | --- | --- | --- | --- | --- |
| AvalonEdit | App | MIT | [Open](https://github.com/icsharpcode/AvalonEdit/blob/master/LICENSE) | [Project](https://github.com/icsharpcode/AvalonEdit) | Code mode editor in the Edit Text Window. |
| CliWrap | App | MIT | [Open](https://github.com/Tyrrrz/CliWrap/blob/master/License.txt) | [Project](https://github.com/Tyrrrz/CliWrap) | — |
| CommunityToolkit.Mvvm | App | MIT | [Open](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) | [Project](https://github.com/CommunityToolkit/dotnet) | View models of the Edit Text Window's panes. |
| Dapplo.Windows.User32 | App | MIT | [Open](https://github.com/dapplo/Dapplo.Windows/blob/master/LICENSE) | [Project](https://github.com/dapplo/Dapplo.Windows) | — |
| Dirkster.AvalonDock | App | MS-PL | [Open](https://github.com/Dirkster99/AvalonDock/blob/master/LICENSE) | [Project](https://github.com/Dirkster99/AvalonDock) | Hosts the Edit Text Window's panes. Includes its Dirkster.AvalonDock.Core package, under the same license. |
| Humanizer.Core | App | MIT | [Open](https://github.com/Humanizr/Humanizer/blob/main/license.txt) | [Project](https://github.com/Humanizr/Humanizer) | — |
| Magick.NET-Q16-AnyCPU | App | Apache-2.0 | [Open](https://github.com/dlemstra/Magick.NET/blob/main/License.txt) | [Project](https://github.com/dlemstra/Magick.NET) | — |
| Magick.NET.SystemDrawing | App | Apache-2.0 | [Open](https://github.com/dlemstra/Magick.NET/blob/main/License.txt) | [Project](https://github.com/dlemstra/Magick.NET) | — |
| Magick.NET.SystemWindowsMedia | App | Apache-2.0 | [Open](https://github.com/dlemstra/Magick.NET/blob/main/License.txt) | [Project](https://github.com/dlemstra/Magick.NET) | — |
| Markdig | App | BSD-2-Clause | [Open](ThirdPartyNotices/licenses/Markdig-license.txt) | [Project](https://github.com/xoofx/markdig) | Bundled to satisfy BSD-2-Clause binary redistribution notice requirements. |
| Mermaider | App | MIT | [Open](ThirdPartyNotices/licenses/Mermaider-license.txt) | [Project](https://github.com/nullean/mermaider) | Native Mermaid diagram rendering, including its Sugiyama layout engine. |
| Microsoft.Extensions.DependencyInjection | App | MIT | [Open](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) | [Project](https://github.com/dotnet/runtime) | The app's service container. |
| Microsoft.Extensions.DependencyInjection.Abstractions | App | MIT | [Open](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) | [Project](https://github.com/dotnet/runtime) | Service container abstractions used by the core libraries. |
| Microsoft.Recognizers.Text.Choice | App | MIT | [Open](https://github.com/microsoft/Recognizers-Text/blob/master/LICENSE) | [Project](https://github.com/microsoft/Recognizers-Text) | Culture-aware entity recognizers. |
| Microsoft.Recognizers.Text.DateTime | App | MIT | [Open](https://github.com/microsoft/Recognizers-Text/blob/master/LICENSE) | [Project](https://github.com/microsoft/Recognizers-Text) | Culture-aware entity recognizers. |
| Microsoft.Recognizers.Text.Number | App | MIT | [Open](https://github.com/microsoft/Recognizers-Text/blob/master/LICENSE) | [Project](https://github.com/microsoft/Recognizers-Text) | Culture-aware entity recognizers. |
| Microsoft.Recognizers.Text.NumberWithUnit | App | MIT | [Open](https://github.com/microsoft/Recognizers-Text/blob/master/LICENSE) | [Project](https://github.com/microsoft/Recognizers-Text) | Culture-aware entity recognizers. |
| Microsoft.Recognizers.Text.Sequence | App | MIT | [Open](https://github.com/microsoft/Recognizers-Text/blob/master/LICENSE) | [Project](https://github.com/microsoft/Recognizers-Text) | Culture-aware entity recognizers. |
| Microsoft.Toolkit.Uwp.Notifications | App | MIT | [Open](https://github.com/CommunityToolkit/WindowsCommunityToolkit/blob/main/License.md) | [Project](https://github.com/CommunityToolkit/WindowsCommunityToolkit) | — |
| Microsoft.WindowsAppSDK.AI | App | Microsoft license terms | [Open](ThirdPartyNotices/licenses/Microsoft.WindowsAppSDK-license.txt) | [Project](https://github.com/microsoft/windowsappsdk) | Package ships Microsoft Windows App SDK license terms. |
| Microsoft.WindowsAppSDK.Foundation | App | Microsoft license terms | [Open](ThirdPartyNotices/licenses/Microsoft.WindowsAppSDK-license.txt) | [Project](https://github.com/microsoft/windowsappsdk) | Package ships Microsoft Windows App SDK license terms. |
| Microsoft.WindowsAppSDK.InteractiveExperiences | App | Microsoft license terms | [Open](ThirdPartyNotices/licenses/Microsoft.WindowsAppSDK-license.txt) | [Project](https://github.com/microsoft/windowsappsdk) | Package ships Microsoft Windows App SDK license terms. |
| Microsoft.WindowsAppSDK.Runtime | App | Microsoft license terms | [Open](ThirdPartyNotices/licenses/Microsoft.WindowsAppSDK-license.txt) | [Project](https://github.com/microsoft/windowsappsdk) | Package ships Microsoft Windows App SDK license terms. |
| Microsoft.WindowsAppSDK.WinUI | App | Microsoft license terms | [Open](ThirdPartyNotices/licenses/Microsoft.WindowsAppSDK-license.txt) | [Project](https://github.com/microsoft/windowsappsdk) | Package ships Microsoft Windows App SDK license terms. |
| NAudio | App | MIT | [Open](https://github.com/naudio/NAudio/blob/master/license.txt) | [Project](https://github.com/naudio/NAudio) | Audio capture and decoding for transcription. |
| NCalcAsync | App, Tests | MIT | [Open](https://github.com/ncalc/ncalc/blob/master/LICENSE) | [Project](https://github.com/ncalc/ncalc) | Shared by the application and the test project. |
| PdfPig | App | Apache-2.0 | [Open](https://github.com/UglyToad/PdfPig/blob/master/LICENSE) | [Project](https://github.com/UglyToad/PdfPig) | — |
| Svg | App | MIT | [Open](https://github.com/svg-net/SVG/blob/master/license.txt) | [Project](https://github.com/svg-net/SVG) | Renders SVG images in the Markdown editor. |
| System.Configuration.ConfigurationManager | App | MIT | [Open](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) | [Project](https://github.com/dotnet/runtime) | Application settings support in the core library. |
| UnitsNet | App | MIT-0 | [Open](https://github.com/angularsen/UnitsNet/blob/master/LICENSE) | [Project](https://github.com/angularsen/UnitsNet) | — |
| Tesseract.CrossPlatform | App | Apache-2.0 | [Open](ThirdPartyNotices/licenses/Tesseract/tesseract.txt) | [Project](https://github.com/tesseract-ocr/tesseract) | In-process Tesseract 5.5 OCR (with Leptonica and other native libraries). Third-party license texts, and the tessdata_fast English model license, are in `ThirdPartyNotices/licenses/Tesseract`. |
| Vortice.Direct3D11 | App | MIT | [Open](https://github.com/amerkoleci/Vortice.Windows/blob/main/LICENSE) | [Project](https://github.com/amerkoleci/Vortice.Windows) | Direct3D 11 interop for HDR-aware screen capture. |
| Vortice.DXGI | App | MIT | [Open](https://github.com/amerkoleci/Vortice.Windows/blob/main/LICENSE) | [Project](https://github.com/amerkoleci/Vortice.Windows) | DXGI interop for HDR-aware screen capture. |
| Whisper.net | App | MIT | [Open](https://github.com/sandrohanea/whisper.net/blob/main/LICENSE) | [Project](https://github.com/sandrohanea/whisper.net) | Local speech-to-text transcription. |
| Whisper.net.Runtime | App | MIT | [Open](https://github.com/sandrohanea/whisper.net/blob/main/LICENSE) | [Project](https://github.com/sandrohanea/whisper.net) | Native whisper.cpp runtime for Whisper.net. |
| WPF-UI | App | MIT | [Open](https://github.com/lepoco/wpfui/blob/main/LICENSE) | [Project](https://github.com/lepoco/wpfui) | — |
| WPF-UI.Tray | App | MIT | [Open](https://github.com/lepoco/wpfui/blob/main/LICENSE) | [Project](https://github.com/lepoco/wpfui) | — |
| WpfMath | App | MIT; Knuth; OFL-1.1 | [Open](ThirdPartyNotices/licenses/WpfMath-license.txt) | [Project](https://github.com/ForNeVeR/xaml-math) | Native math rendering; includes XAML-Math shared code and font notices. |
| ZXing.Net | App | Apache-2.0 | [Open](https://github.com/micjahn/ZXing.Net/blob/master/COPYING) | [Project](https://github.com/micjahn/ZXing.Net) | — |
| ZXing.Net.Bindings.Windows.Compatibility | App | Apache-2.0 | [Open](https://github.com/micjahn/ZXing.Net/blob/master/COPYING) | [Project](https://github.com/micjahn/ZXing.Net) | — |
| BenchmarkDotNet | Tests | MIT | [Open](https://github.com/dotnet/BenchmarkDotNet/blob/master/LICENSE.md) | [Project](https://github.com/dotnet/BenchmarkDotNet) | Test-only dependency. |
| Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers | Tests | Microsoft license terms | [Open](ThirdPartyNotices/licenses/Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers-LICENSE.md) | [Project](https://learn.microsoft.com/visualstudio/profiling/) | Visual Studio benchmarking tooling; test-only dependency. |
| Xunit.StaFact | Tests | MS-PL | [Open](https://github.com/AArnott/Xunit.StaFact/blob/main/LICENSE) | [Project](https://github.com/AArnott/Xunit.StaFact) | Test-only dependency. |
| xunit.v3 | Tests | Apache-2.0 | [Open](https://github.com/xunit/xunit/blob/main/LICENSE) | [Project](https://github.com/xunit/xunit) | Test-only dependency. |
</content>
</invoke>
