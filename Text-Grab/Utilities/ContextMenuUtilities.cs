using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace Text_Grab.Utilities;

/// <summary>
/// Utility class for managing Windows context menu integration.
/// Adds "Grab text with Text Grab" and "Open in Grab Frame" options to the right-click context menu for image files.
/// </summary>
internal static class ContextMenuUtilities
{
    private const string GrabTextRegistryKeyName = "Text-Grab.GrabText";
    private const string GrabTextDisplayText = "Grab text with Text Grab";
    private const string GrabFrameRegistryKeyName = "Text-Grab.OpenInGrabFrame";
    private const string GrabFrameDisplayText = "Open in Grab Frame";

    /// <summary>
    /// Supported image file extensions for context menu integration.
    /// </summary>
    private static readonly string[] ImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".tiff",
        ".tif"
    ];

    /// <summary>
    /// Adds Text Grab to the Windows context menu for image files.
    /// This allows users to right-click on an image and select "Grab text with Text Grab" or "Open in Grab Frame".
    /// </summary>
    /// <param name="errorMessage">When the method returns false, contains an error message describing the failure.</param>
    /// <returns>True if registration was successful, false otherwise.</returns>
    public static bool AddToContextMenu(out string? errorMessage)
    {
        errorMessage = null;
        string executablePath = FileUtilities.GetExePath();

        if (string.IsNullOrEmpty(executablePath))
        {
            errorMessage = "Could not determine the application executable path.";
            return false;
        }

        try
        {
            foreach (string extension in ImageExtensions)
            {
                RegisterGrabTextContextMenu(extension, executablePath);
                RegisterGrabFrameContextMenu(extension, executablePath);
            }
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Context menu registration failed due to permissions: {ex.Message}");
            errorMessage = "Permission denied. Please run Text Grab as administrator or check your registry permissions.";
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Context menu registration failed: {ex.Message}");
            errorMessage = $"Failed to register context menu: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Removes Text Grab from the Windows context menu for image files.
    /// </summary>
    /// <param name="errorMessage">When the method returns false, contains an error message describing the failure.</param>
    /// <returns>True if removal was successful, false otherwise.</returns>
    public static bool RemoveFromContextMenu(out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            foreach (string extension in ImageExtensions)
            {
                UnregisterContextMenuForExtension(extension, GrabTextRegistryKeyName);
                UnregisterContextMenuForExtension(extension, GrabFrameRegistryKeyName);
            }
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Context menu unregistration failed due to permissions: {ex.Message}");
            errorMessage = "Permission denied. Some context menu entries could not be removed.";
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Context menu unregistration failed: {ex.Message}");
            errorMessage = $"Failed to remove context menu entries: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Checks if Text Grab is currently registered in the context menu.
    /// </summary>
    /// <returns>True if registered, false otherwise.</returns>
    public static bool IsRegisteredInContextMenu()
    {
        try
        {
            // Check if at least one extension has the context menu registered
            foreach (string extension in ImageExtensions)
            {
                string keyPath = GetShellKeyPath(extension, GrabTextRegistryKeyName);
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath);
                if (key is not null)
                    return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Context menu registration check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Registers the "Grab text with Text Grab" context menu entry for a specific file extension.
    /// </summary>
    private static void RegisterGrabTextContextMenu(string extension, string executablePath)
    {
        string shellKeyPath = GetShellKeyPath(extension, GrabTextRegistryKeyName);
        string commandKeyPath = $@"{shellKeyPath}\command";

        using (RegistryKey? shellKey = Registry.CurrentUser.CreateSubKey(shellKeyPath))
        {
            if (shellKey is null)
            {
                Debug.WriteLine($"Failed to create registry key: {shellKeyPath}");
                throw new InvalidOperationException($"Could not create registry key for {extension}");
            }

            shellKey.SetValue(string.Empty, GrabTextDisplayText);
            shellKey.SetValue("Icon", $"\"{executablePath}\"");
        }

        using (RegistryKey? commandKey = Registry.CurrentUser.CreateSubKey(commandKeyPath))
        {
            if (commandKey is null)
            {
                Debug.WriteLine($"Failed to create registry key: {commandKeyPath}");
                throw new InvalidOperationException($"Could not create command registry key for {extension}");
            }

            // %1 is replaced by Windows with the path to the file that was right-clicked
            commandKey.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
        }
    }

    /// <summary>
    /// Registers the "Open in Grab Frame" context menu entry for a specific file extension.
    /// </summary>
    private static void RegisterGrabFrameContextMenu(string extension, string executablePath)
    {
        string shellKeyPath = GetShellKeyPath(extension, GrabFrameRegistryKeyName);
        string commandKeyPath = $@"{shellKeyPath}\command";

        using (RegistryKey? shellKey = Registry.CurrentUser.CreateSubKey(shellKeyPath))
        {
            if (shellKey is null)
            {
                Debug.WriteLine($"Failed to create registry key: {shellKeyPath}");
                throw new InvalidOperationException($"Could not create registry key for {extension}");
            }

            shellKey.SetValue(string.Empty, GrabFrameDisplayText);
            shellKey.SetValue("Icon", $"\"{executablePath}\"");
        }

        using (RegistryKey? commandKey = Registry.CurrentUser.CreateSubKey(commandKeyPath))
        {
            if (commandKey is null)
            {
                Debug.WriteLine($"Failed to create registry key: {commandKeyPath}");
                throw new InvalidOperationException($"Could not create command registry key for {extension}");
            }

            // --grabframe flag opens the image in GrabFrame instead of EditTextWindow
            commandKey.SetValue(string.Empty, $"\"{executablePath}\" --grabframe \"%1\"");
        }
    }

    /// <summary>
    /// Removes a context menu entry for a specific file extension.
    /// </summary>
    private static void UnregisterContextMenuForExtension(string extension, string registryKeyName)
    {
        string shellKeyPath = GetShellKeyPath(extension, registryKeyName);

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(shellKeyPath, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unregister context menu for {extension}: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the registry path for the shell context menu key for a given extension and registry key name.
    /// </summary>
    internal static string GetShellKeyPath(string extension, string registryKeyName = GrabTextRegistryKeyName)
    {
        return $@"Software\Classes\SystemFileAssociations\{extension}\shell\{registryKeyName}";
    }

    /// <summary>
    /// Gets the registry path for the shell context menu key for a given extension.
    /// Uses the default GrabText registry key name for backward compatibility with tests.
    /// </summary>
    internal static string GetShellKeyPath(string extension)
    {
        return GetShellKeyPath(extension, GrabTextRegistryKeyName);
    }
}
