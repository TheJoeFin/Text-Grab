using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using Text_Grab.Properties;

namespace Text_Grab.Utilities;

/// <summary>
/// Decides whether WPF renders through the GPU or on the CPU.
/// </summary>
/// <remarks>
/// On Arm64 devices (Snapdragon / Adreno) WPF's hardware path runs D3D9 on top of D3D9On12 and the
/// GPU driver, which keeps up to ~1 GB of committed GPU memory alive for a tray app that is mostly
/// idle and does not give it back when windows close. Software rendering avoids that path entirely.
/// </remarks>
internal static class RenderingUtilities
{
    internal static RenderingMode ParseRenderingMode(string? value)
        => Enum.TryParse(value, true, out RenderingMode mode) && Enum.IsDefined(mode)
            ? mode
            : RenderingMode.Auto;

    /// <param name="deviceArchitecture">
    /// The architecture of the device, not of this process, so an x64 build emulated on an Arm64
    /// device is still treated as Arm64.
    /// </param>
    internal static bool ShouldUseSoftwareRendering(RenderingMode mode, Architecture deviceArchitecture) => mode switch
    {
        RenderingMode.Software => true,
        RenderingMode.Hardware => false,
        _ => deviceArchitecture == Architecture.Arm64,
    };

    internal static bool ShouldUseSoftwareRendering(Settings settings)
        => ShouldUseSoftwareRendering(ParseRenderingMode(settings.RenderingMode), RuntimeInformation.OSArchitecture);

    /// <summary>
    /// Must run before the first WPF window is created; changes to the setting apply on the next launch.
    /// </summary>
    internal static void ApplyRenderingMode(Settings settings)
    {
        if (ShouldUseSoftwareRendering(settings))
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
    }
}
