using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Text_Grab.Utilities;

/// <summary>
/// Utility class for the text-grab:// protocol used by companion apps such as
/// the Text Grab browser extension. The URI is only a command channel; any
/// data payload (like a copied table) travels via the clipboard.
/// Supported URIs:
///   text-grab://paste-spreadsheet      Edit Text window in spreadsheet mode, paste clipboard
///   text-grab://edit-text              Edit Text window with clipboard text
///   text-grab://grab-frame[?path=...]  Grab Frame, optionally opening a local image/PDF
///   text-grab://grab-text?path=...     OCR a local image/PDF straight to the clipboard (no window)
///   text-grab://fullscreen             Fullscreen grab
///   text-grab://quick-lookup           Quick Simple Lookup
///   text-grab://settings               Settings window
/// </summary>
internal static class ProtocolUtilities
{
    internal const string Scheme = "text-grab";

    private const string ProtocolKeyPath = @"Software\Classes\" + Scheme;

    /// <summary>
    /// Returns true when a startup argument looks like a text-grab:// URI.
    /// </summary>
    internal static bool IsProtocolUri(string? argument)
    {
        return argument is not null
            && argument.StartsWith($"{Scheme}:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a text-grab:// URI into a lowercase command and its query parameters.
    /// Accepts both text-grab://command?key=value and text-grab:command forms.
    /// </summary>
    internal static bool TryParseProtocolUri(
        string uriString,
        out string command,
        out Dictionary<string, string> parameters)
    {
        command = string.Empty;
        parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!Uri.TryCreate(uriString, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
            return false;

        // text-grab://paste-spreadsheet puts the command in Host;
        // text-grab:paste-spreadsheet puts it in AbsolutePath.
        string rawCommand = !string.IsNullOrEmpty(uri.Host) ? uri.Host : uri.AbsolutePath;
        command = rawCommand.Trim('/').ToLowerInvariant();
        if (string.IsNullOrEmpty(command))
            return false;

        string query = uri.Query.TrimStart('?');
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0)
                continue;
            string key = Uri.UnescapeDataString(pair[..separatorIndex]);
            string value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
            parameters[key] = value;
        }

        return true;
    }

    /// <summary>
    /// Validates a <c>path=</c> parameter supplied via the text-grab:// protocol and,
    /// when safe, returns its canonical full path. Because any web page can launch the
    /// protocol, the path is treated as untrusted and must clear several gates:
    /// <list type="bullet">
    ///   <item>No UNC or device paths (<c>\\server\share</c>, <c>\\?\</c>, <c>\\.\</c>) — probing
    ///   one would trigger an outbound SMB authentication and leak the user's NTLM credentials.</item>
    ///   <item>Canonicalized with <see cref="Path.GetFullPath(string)"/> so traversal
    ///   (<c>..\..\</c>) and relative paths cannot escape the allowed locations.</item>
    ///   <item>Rooted on a local, non-network drive.</item>
    ///   <item>Located under a folder the companion extension legitimately writes to
    ///   (Downloads, Temp, Pictures), so a page cannot point us at arbitrary files.</item>
    ///   <item>An existing image/PDF file.</item>
    /// </list>
    /// All checks that could touch the path (existence, drive type) run only after the
    /// UNC/device gate, so the dangerous network probe is never performed.
    /// </summary>
    internal static bool TryGetSafeProtocolFilePath(string? rawPath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(rawPath))
            return false;

        // Reject UNC and device/extended-length paths before any filesystem call.
        if (rawPath.StartsWith(@"\\", StringComparison.Ordinal)
            || rawPath.StartsWith("//", StringComparison.Ordinal))
            return false;

        string candidate;
        try
        {
            candidate = Path.GetFullPath(rawPath);
        }
        catch
        {
            return false;
        }

        // GetFullPath can still surface a UNC root for some inputs; re-check.
        if (candidate.StartsWith(@"\\", StringComparison.Ordinal))
            return false;

        // Require a drive-letter root (e.g. "C:\"). This also rejects rooted-but-
        // drive-less paths like "\Windows\..." which resolve against the current drive.
        string? root = Path.GetPathRoot(candidate);
        if (root is null || root.Length < 2 || root[1] != ':')
            return false;

        try
        {
            DriveInfo drive = new(root);
            if (drive.DriveType is DriveType.Network or DriveType.NoRootDirectory or DriveType.Unknown)
                return false;
        }
        catch
        {
            return false;
        }

        if (!IsUnderAllowedRoot(candidate))
            return false;

        if (!IoUtilities.IsVisualDocumentFile(candidate))
            return false;

        fullPath = candidate;
        return true;
    }

    /// <summary>
    /// Folders the text-grab:// protocol is allowed to open files from — the locations a
    /// companion app such as the browser extension realistically deposits a captured image.
    /// </summary>
    private static IEnumerable<string> AllowedFileRoots()
    {
        yield return Path.GetTempPath();

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
            yield return Path.Combine(userProfile, "Downloads");

        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    private static bool IsUnderAllowedRoot(string fullPath)
    {
        foreach (string root in AllowedFileRoots())
        {
            if (string.IsNullOrEmpty(root))
                continue;

            string normalizedRoot;
            try
            {
                normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            }
            catch
            {
                continue;
            }

            if (fullPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return true;

            // Require a separator after the root so "C:\Downloads" does not also
            // match a sibling like "C:\DownloadsEvil".
            if (fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Registers the text-grab:// protocol for the current user when running
    /// unpackaged. Packaged installs register it through the MSIX manifest.
    /// Safe to call on every startup; only writes when missing or stale.
    /// </summary>
    internal static void EnsureProtocolRegistration()
    {
        if (AppUtilities.IsPackaged())
            return;

        string executablePath = FileUtilities.GetExePath();
        if (string.IsNullOrEmpty(executablePath))
            return;

        string expectedCommand = $"\"{executablePath}\" \"%1\"";

        try
        {
            using (RegistryKey? existingCommandKey =
                Registry.CurrentUser.OpenSubKey($@"{ProtocolKeyPath}\shell\open\command"))
            {
                if (existingCommandKey?.GetValue(string.Empty) as string == expectedCommand)
                    return;
            }

            using RegistryKey protocolKey = Registry.CurrentUser.CreateSubKey(ProtocolKeyPath);
            protocolKey.SetValue(string.Empty, "URL:Text Grab Protocol");
            protocolKey.SetValue("URL Protocol", string.Empty);

            using RegistryKey iconKey = protocolKey.CreateSubKey("DefaultIcon");
            iconKey.SetValue(string.Empty, $"\"{executablePath}\",0");

            using RegistryKey commandKey = protocolKey.CreateSubKey(@"shell\open\command");
            commandKey.SetValue(string.Empty, expectedCommand);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"text-grab:// protocol registration failed: {ex.Message}");
        }
    }
}
