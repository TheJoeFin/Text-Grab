using System.Runtime.CompilerServices;
using Text_Grab;
using Text_Grab.Utilities;
using Xunit.Sdk;
using Xunit.v3;

// WPF and WinRT tests share native graphics resources; concurrent collections
// can leave the ARM64 graphics driver crashing during process teardown.
[assembly: Parallelization(Mode = ParallelMode.None)]

namespace Tests;

internal static class TestHostInitializer
{
    // Tests create WPF windows without App startup, so apply its ARM64 rendering
    // policy before any window can initialize the GPU driver.
    [ModuleInitializer]
    internal static void Initialize()
        => RenderingUtilities.ApplyRenderingMode(new() { RenderingMode = nameof(RenderingMode.Auto) });
}
