using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using Text_Grab.Core.Editing;

namespace Text_Grab.Utilities;

/// <summary>
/// Registers per-user file associations for unpackaged installs. Packaged installs declare
/// their associations through the MSIX manifest instead.
/// </summary>
internal static class FileAssociationUtilities
{
    private const string GrabFrameProgId = "TextGrab.GrabFrame";
    private const string GrabFrameProgIdDescription = "Text Grab Frame";
    private const string ApplicationName = "Text Grab";
    private const string CapabilitiesPath = @"Software\JoeFinApps\TextGrab\Capabilities";

    private const string ClassesRoot = @"Software\Classes\";
    private const string GrabFrameExtensionKeyPath = ClassesRoot + GrabFrameFileUtilities.GrabFrameFileExtension;
    private const string GrabFrameProgIdKeyPath = ClassesRoot + GrabFrameProgId;

    private sealed record DocumentAssociation(string IconName, string ProgId, string Description, IEnumerable<string> Extensions);

    // Keep Package.appxmanifest in sync. The Code group is last so the more specific
    // file families take precedence over the editor's highlighting extensions.
    private static readonly DocumentAssociation[] DocumentAssociations =
    [
        new("Text", "TextGrab.TextDocument", "Text Grab Text Document",
            [".asc", ".log", ".nfo", ".srt", ".text", ".txt", ".vtt"]),
        new("Markdown", "TextGrab.Markdown", "Text Grab Markdown Document", IoUtilities.MarkdownExtensions),
        new("Header", "TextGrab.FormattedDocument", "Text Grab Formatted Document",
            [".adoc", ".asciidoc", ".org", ".rst", ".tex"]),
        new("Table", "TextGrab.Table", "Text Grab Table", IoUtilities.SpreadsheetExtensions),
        new("Markup", "TextGrab.Markup", "Text Grab Markup Document",
            [".htm", ".html", ".resx", ".svg", ".xaml", ".xml", ".xsd", ".xsl", ".xslt"]),
        new("Code", "TextGrab.Code", "Text Grab Source File",
            CodeLanguages.All.SelectMany(language => language.Extensions).Concat(
            [
                ".ini", ".cfg", ".conf", ".cnf", ".toml", ".yaml", ".yml", ".env",
                ".editorconfig", ".gitignore", ".gitattributes", ".gitconfig", ".dockerignore",
                ".jsonl", ".ndjson", ".sln", ".slnx", ".http",
                ".sh", ".bash", ".zsh", ".rb", ".go", ".rs", ".kt", ".swift", ".lua", ".fs", ".fsx"
            ]))
    ];

    internal static void EnsureTextFileAssociations()
    {
        if (PackageIdentity.IsPackaged())
            return;

        string executablePath = FileUtilities.GetExePath();
        if (string.IsNullOrEmpty(executablePath))
        {
            Debug.WriteLine("Cannot register text file associations without an executable path.");
            return;
        }

        try
        {
            string iconsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Text Grab", "FileIcons");
            if (RegisterTextFileAssociations(Registry.CurrentUser, executablePath, iconsDirectory))
            {
                const int ShcneAssocChanged = 0x08000000;
                NativeMethods.SHChangeNotify(ShcneAssocChanged, 0, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            Debug.WriteLine($"Text file association registration failed: {ex.Message}");
        }
    }

    internal static bool RegisterTextFileAssociations(RegistryKey userRoot, string executablePath, string iconsDirectory)
    {
        string command = $"\"{executablePath}\" \"%1\"";

        // Keep the executable registration valid for anyone who already selected it as their default.
        using RegistryKey application = userRoot.CreateSubKey($@"{ClassesRoot}Applications\{Path.GetFileName(executablePath)}");
        using RegistryKey applicationCommand = application.CreateSubKey(@"shell\open\command");
        using RegistryKey? supportedTypes = application.OpenSubKey("SupportedTypes", writable: true);
        using RegistryKey capabilities = userRoot.CreateSubKey(CapabilitiesPath);
        using RegistryKey fileAssociations = capabilities.CreateSubKey("FileAssociations");

        bool changed = SetStringValueIfChanged(application, "FriendlyAppName", ApplicationName);
        changed |= SetStringValueIfChanged(applicationCommand, "", command);
        changed |= SetStringValueIfChanged(capabilities, "ApplicationName", ApplicationName);
        changed |= SetStringValueIfChanged(capabilities, "ApplicationDescription", "Open and edit text documents in Text Grab.");

        Directory.CreateDirectory(iconsDirectory);
        HashSet<string> registeredExtensions = new(StringComparer.OrdinalIgnoreCase);
        foreach (DocumentAssociation association in DocumentAssociations)
        {
            string iconFileName = association.IconName + ".ico";
            string iconPath = Path.Combine(iconsDirectory, iconFileName);
            changed |= EnsureIconFile(iconFileName, iconPath);

            using RegistryKey progId = userRoot.CreateSubKey(ClassesRoot + association.ProgId);
            using RegistryKey progIdCommand = progId.CreateSubKey(@"shell\open\command");
            using RegistryKey progIdIcon = progId.CreateSubKey("DefaultIcon");
            changed |= SetStringValueIfChanged(progId, "", association.Description);
            changed |= SetStringValueIfChanged(progIdCommand, "", command);
            changed |= SetStringValueIfChanged(progIdIcon, "", $"\"{iconPath}\",0");

            foreach (string extension in association.Extensions)
            {
                if (!registeredExtensions.Add(extension))
                    continue;

                // Advertise the icon-bearing ProgID in both pickers without setting any defaults.
                using RegistryKey openWith = userRoot.CreateSubKey($@"{ClassesRoot}{extension}\OpenWithProgids");
                changed |= SetStringValueIfChanged(openWith, association.ProgId, "");
                changed |= SetStringValueIfChanged(fileAssociations, extension, association.ProgId);

                if (supportedTypes?.GetValue(extension) is not null)
                {
                    supportedTypes.DeleteValue(extension, throwOnMissingValue: false);
                    changed = true;
                }
            }
        }

        using RegistryKey registeredApplications = userRoot.CreateSubKey(@"Software\RegisteredApplications");
        changed |= SetStringValueIfChanged(registeredApplications, ApplicationName, CapabilitiesPath);
        return changed;
    }

    private static bool EnsureIconFile(string fileName, string path)
    {
        // Explorer needs a stable file path even for the standalone single-file executable.
        using Stream iconResource = typeof(FileAssociationUtilities).Assembly.GetManifestResourceStream($"Text_Grab.FileIcons.{fileName}")
            ?? throw new FileNotFoundException($"Embedded file association icon {fileName} was not found.");
        using MemoryStream iconData = new();
        iconResource.CopyTo(iconData);
        byte[] icon = iconData.ToArray();
        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(icon))
            return false;

        string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(tempPath, icon);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
        return true;
    }

    private static bool SetStringValueIfChanged(RegistryKey key, string name, string value)
    {
        if (key.GetValue(name) as string == value)
            return false;

        key.SetValue(name, value, RegistryValueKind.String);
        return true;
    }

    /// <summary>
    /// Registers the .tggf file association for the current user when running unpackaged.
    /// Safe to call on every startup; only writes when the registration is missing or stale.
    /// </summary>
    internal static void EnsureGrabFrameFileAssociation()
    {
        if (PackageIdentity.IsPackaged())
            return;

        string executablePath = FileUtilities.GetExePath();
        if (string.IsNullOrEmpty(executablePath))
            return;

        string expectedCommand = $"\"{executablePath}\" \"%1\"";

        try
        {
            using (RegistryKey? existingCommandKey =
                Registry.CurrentUser.OpenSubKey($@"{GrabFrameProgIdKeyPath}\shell\open\command"))
            using (RegistryKey? existingExtensionKey =
                Registry.CurrentUser.OpenSubKey(GrabFrameExtensionKeyPath))
            {
                if (existingCommandKey?.GetValue(string.Empty) as string == expectedCommand
                    && existingExtensionKey?.GetValue(string.Empty) as string == GrabFrameProgId)
                {
                    return;
                }
            }

            using (RegistryKey extensionKey = Registry.CurrentUser.CreateSubKey(GrabFrameExtensionKeyPath))
                extensionKey.SetValue(string.Empty, GrabFrameProgId);

            using RegistryKey progIdKey = Registry.CurrentUser.CreateSubKey(GrabFrameProgIdKeyPath);
            progIdKey.SetValue(string.Empty, GrabFrameProgIdDescription);

            using (RegistryKey iconKey = progIdKey.CreateSubKey("DefaultIcon"))
                iconKey.SetValue(string.Empty, $"\"{executablePath}\",0");

            using RegistryKey commandKey = progIdKey.CreateSubKey(@"shell\open\command");
            commandKey.SetValue(string.Empty, expectedCommand);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($".tggf file association registration failed: {ex.Message}");
        }
    }
}
