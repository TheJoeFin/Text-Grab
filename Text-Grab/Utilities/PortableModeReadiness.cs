using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Text_Grab.Properties;

namespace Text_Grab.Utilities;

/// <summary>One row of the Fully Portable mode readiness checklist in <c>DangerSettings</c>.</summary>
internal readonly record struct PortableReadinessCheck(string Title, string Detail, bool IsSatisfied, bool IsRequired);

/// <summary>
/// Checks and fix-it actions backing the Fully Portable mode readiness checklist. Each check looks
/// at on-disk/registry state that Fully Portable mode can no longer clean up once it's active (see
/// <see cref="SystemIntegrationGate"/>), so the checklist lets the user resolve them beforehand
/// instead of discovering stale data/registrations after the fact.
/// </summary>
internal static class PortableModeReadiness
{
    private static readonly string[] DataSubfolders = ["WhisperModels", "Logs"];

    public static PortableReadinessCheck CheckFileBackedSettings(Settings settings)
    {
        bool ok = settings.EnableFileBackedManagedSettings;
        return new(
            "File-backed settings storage",
            ok
                ? "Enabled."
                : "Settings need to live in a file beside the app instead of the registry before fully portable mode can take over.",
            ok,
            true);
    }

    public static PortableReadinessCheck CheckStartupOnLogin(Settings settings)
    {
        bool ok = !settings.StartupOnLogin;
        return new(
            "Startup on login",
            ok
                ? "Not set to start on login."
                : "Starting on login writes a registry entry that fully portable mode can no longer remove once it's active. Turn it off first.",
            ok,
            true);
    }

    /// <summary>True if any registry-based OS integration is currently registered for this user.</summary>
    public static bool HasRegistryIntegration(Settings settings) =>
        ContextMenuUtilities.IsRegisteredInContextMenu()
        || settings.RegisterOpenWith
        || FileAssociationUtilities.IsGrabFrameFileAssociationRegistered()
        || ProtocolHandlerUtilities.IsProtocolRegistered();

    public static PortableReadinessCheck CheckRegistryIntegration(Settings settings)
    {
        bool ok = !HasRegistryIntegration(settings);
        return new(
            "Registry-based OS integration",
            ok
                ? "No context menu, file association, protocol, or \"Open with\" entries registered."
                : "Context menu, .tggf file association, text-grab:// protocol, or \"Open with\" entries are still registered. Remove them so nothing is left outside Text Grab's folder.",
            ok,
            true);
    }

    /// <summary>True if any downloaded Whisper models or logs still live under AppData.</summary>
    public static bool HasDataToMove()
    {
        foreach (string subfolder in DataSubfolders)
        {
            string source = PortableStorageUtilities.GetAppDataDataDirectory(subfolder);
            if (Directory.Exists(source) && Directory.EnumerateFileSystemEntries(source).Any())
                return true;
        }

        return false;
    }

    public static PortableReadinessCheck CheckTranscriptionModels()
    {
        bool needsMove = HasDataToMove();
        return new(
            "Transcription models and logs",
            needsMove
                ? "Downloaded Whisper models and logs are still stored under AppData. Move them into Text Grab's own folder so they aren't re-downloaded."
                : "Nothing to move.",
            !needsMove,
            true);
    }

    public static PortableReadinessCheck CheckRecentBackup(bool backedUpThisSession)
    {
        return new(
            "Recent settings backup",
            backedUpThisSession
                ? "Backed up this session."
                : "Optional, but recommended before moving data around: export a copy of your settings you can restore from if anything goes wrong.",
            backedUpThisSession,
            false);
    }

    public static void EnableFileBackedSettings(Settings settings)
    {
        settings.EnableFileBackedManagedSettings = true;
        settings.Save();
    }

    public static async Task DisableStartupOnLoginAsync(Settings settings)
    {
        await ImplementAppOptions.ImplementStartupOption(false);
        settings.StartupOnLogin = false;
        settings.Save();
    }

    public static void RemoveRegistryIntegration(Settings settings)
    {
        ContextMenuUtilities.RemoveFromContextMenu(out _);
        settings.AddToContextMenu = false;

        ImplementAppOptions.UnregisterAsImageOpenWithApp();
        settings.RegisterOpenWith = false;

        FileAssociationUtilities.RemoveGrabFrameFileAssociation();
        ProtocolHandlerUtilities.RemoveProtocolRegistration();

        settings.Save();
    }

    /// <summary>
    /// Moves already-downloaded Whisper models and logs from the normal AppData location into the
    /// beside-executable folder Fully Portable mode uses, so they don't need to be re-downloaded
    /// (models) or get orphaned (logs). A file already present at the destination is left as-is.
    /// </summary>
    public static void MoveDataToPortableFolder()
    {
        foreach (string subfolder in DataSubfolders)
        {
            string source = PortableStorageUtilities.GetAppDataDataDirectory(subfolder);
            if (!Directory.Exists(source))
                continue;

            string destination = PortableStorageUtilities.GetPortableDataDirectory(subfolder);
            Directory.CreateDirectory(destination);

            foreach (string filePath in Directory.EnumerateFiles(source))
            {
                string destinationPath = Path.Combine(destination, Path.GetFileName(filePath));
                if (File.Exists(destinationPath))
                    continue;

                File.Move(filePath, destinationPath);
            }

            if (!Directory.EnumerateFileSystemEntries(source).Any())
                Directory.Delete(source);
        }
    }
}
