using System.Linq;
using System.Reflection;
using Text_Grab.Models;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// Structural guards on the Core tier. These are cheap and they fail loudly the moment a move
/// smuggles a WPF dependency into a library that is supposed to be headless - which otherwise
/// only shows up much later, as an unexplained UseWPF flip in a csproj diff.
/// </summary>
public class TierBoundaryTests
{
    private static readonly Assembly CoreAssembly = typeof(RectangleFExtensions).Assembly;
    private static readonly Assembly CoreWindowsAssembly = typeof(IOcrLinesWords).Assembly;

    [Fact]
    public void TextGrabCore_ReferencesNoWpfOrWinRtAssemblies()
    {
        string[] offenders = ReferencedAssemblyNames(CoreAssembly)
            .Where(IsUiOrWindowsAssembly)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Text-Grab.Core must stay platform-neutral but references: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void TextGrabCoreWindows_ReferencesNoWpfAssemblies()
    {
        // Windows APIs are expected here; WPF is not. Core.Windows keeps UseWPF=false so that the
        // OCR, capture and interop code stays usable from a headless host.
        string[] offenders = ReferencedAssemblyNames(CoreWindowsAssembly)
            .Where(IsWpfAssembly)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Text-Grab.Core.Windows must not use WPF but references: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void TextGrabCore_DoesNotReferenceTextGrabCoreWindows()
    {
        // Dependencies point one way: app -> Core.Windows -> Core.
        Assert.DoesNotContain(
            "Text-Grab.Core.Windows",
            ReferencedAssemblyNames(CoreAssembly));
    }

    private static string[] ReferencedAssemblyNames(Assembly assembly)
        => [.. assembly.GetReferencedAssemblies().Select(static name => name.Name ?? string.Empty)];

    private static bool IsWpfAssembly(string name)
        => name is "PresentationCore" or "PresentationFramework" or "WindowsBase" or "System.Xaml";

    private static bool IsUiOrWindowsAssembly(string name)
        => IsWpfAssembly(name)
            || name is "System.Windows.Forms" or "System.Drawing.Common"
            || name.StartsWith("Microsoft.Windows.", System.StringComparison.Ordinal)
            || name.StartsWith("Microsoft.WindowsAppSDK", System.StringComparison.Ordinal);
}
