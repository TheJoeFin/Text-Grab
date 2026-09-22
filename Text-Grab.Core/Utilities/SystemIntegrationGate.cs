using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>
/// Whether registry-touching OS integration (startup-on-login, context menu, file/protocol
/// associations) should be skipped this run.
/// </summary>
internal static class SystemIntegrationGate
{
    /// <summary>
    /// True when Fully Portable mode disallows registry writes - the whole point of the mode is
    /// that nothing lands outside the app's own folder, and the registry is the one place that
    /// can never be redirected there. <see cref="SettingsAccess.IsConfigured"/> is checked first
    /// so callers reached before the app wires up its settings resolver (or from a test host with
    /// none registered) still behave exactly as they did before this gate existed.
    /// </summary>
    internal static bool PortableModeBlocksRegistry =>
        SettingsAccess.IsConfigured && SettingsAccess.Current.FullyPortable;

    /// <summary>
    /// True when either an automation profile disallows system integration or Fully Portable
    /// mode does. Existing call sites already guarded by the automation check can switch to this
    /// without changing behavior for automation; it only adds the new portable-mode case.
    /// </summary>
    internal static bool IsBlocked =>
        AutomationProfile.Current is { AllowsSystemIntegration: false } || PortableModeBlocksRegistry;
}
