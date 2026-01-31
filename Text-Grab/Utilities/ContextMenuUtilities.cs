using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace Text_Grab.Utilities;

/// <summary>
/// Utility class for managing Windows context menu integration.
/// Adds "Grab text with Text Grab" option to the right-click context menu for image files.
/// </summary>
internal static class ContextMenuUtilities
{
    private const string ContextMenuRegistryKeyName = "Text-Grab.GrabText";
    private const string ContextMenuDisplayText = "Grab text with Text Grab";

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
    /// This allows users to right-click on an image and select "Grab text with Text Grab".
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
                RegisterContextMenuForExtension(extension, executablePath);
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
    /// <returns>True if removal was successful, false otherwise.</returns>
    public static bool RemoveFromContextMenu()
    {
        try
        {
            foreach (string extension in ImageExtensions)
            {
                UnregisterContextMenuForExtension(extension);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Context menu unregistration failed: {ex.Message}");
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
                string keyPath = GetShellKeyPath(extension);
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
    /// Registers the context menu entry for a specific file extension.
    /// Uses the Shell registration approach under HKEY_CURRENT_USER for per-user installation.
    /// </summary>
    /// <param name="extension">The file extension (e.g., ".png")</param>
    /// <param name="executablePath">The path to the Text Grab executable</param>
    private static void RegisterContextMenuForExtension(string extension, string executablePath)
    {
        // Register under HKEY_CURRENT_USER\Software\Classes\
        // This approach works for per-user installation without requiring admin rights
        string shellKeyPath = GetShellKeyPath(extension);
        string commandKeyPath = $@"{shellKeyPath}\command";

        // Create the shell key with display name
        using (RegistryKey? shellKey = Registry.CurrentUser.CreateSubKey(shellKeyPath))
        {
            if (shellKey is null)
            {
                Debug.WriteLine($"Failed to create registry key: {shellKeyPath}");
                throw new InvalidOperationException($"Could not create registry key for {extension}");
            }

            shellKey.SetValue(string.Empty, ContextMenuDisplayText);
            shellKey.SetValue("Icon", $"\"{executablePath}\"");
        }

        // Create the command key with the executable path
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
    /// Removes the context menu entry for a specific file extension.
    /// </summary>
    /// <param name="extension">The file extension (e.g., ".png")</param>
    private static void UnregisterContextMenuForExtension(string extension)
    {
        string shellKeyPath = GetShellKeyPath(extension);

        try
        {
            // Delete the entire shell key and its subkeys
            Registry.CurrentUser.DeleteSubKeyTree(shellKeyPath, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            // Log but don't throw - we want to continue trying to remove other extensions
            Debug.WriteLine($"Failed to unregister context menu for {extension}: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the registry path for the shell context menu key for a given extension.
    /// </summary>
    /// <param name="extension">The file extension (e.g., ".png")</param>
    /// <returns>The registry key path</returns>
    internal static string GetShellKeyPath(string extension)
    {
        // Using SystemFileAssociations allows the context menu to work regardless of
        // which application is associated with the file type
        return $@"Software\Classes\SystemFileAssociations\{extension}\shell\{ContextMenuRegistryKeyName}";
    }
}
