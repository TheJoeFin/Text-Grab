using Microsoft.Win32;
using System.Xml.Linq;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

public class FileAssociationTests
{
    [Fact]
    public void BothDistributionsAssignTheSameFileIconsWithoutChangingDefaults()
    {
        string testPath = $@"Software\TextGrab.FileAssociationTests.{Guid.NewGuid():N}";
        const string exePath = @"C:\Portable Apps\Text Grab\Text-Grab-arm64.exe";
        const string applicationPath = @"Software\Classes\Applications\Text-Grab-arm64.exe";
        string expectedCommand = $"\"{exePath}\" \"%1\"";
        DirectoryInfo iconsDirectory = Directory.CreateTempSubdirectory("TextGrabFileIcons.");

        try
        {
            // Only write under this test key, never the current user's actual associations.
            using RegistryKey root = Registry.CurrentUser.CreateSubKey(testPath);
            using RegistryKey existingExtension = root.CreateSubKey(@"Software\Classes\.txt");
            existingExtension.SetValue("", "OtherEditor.Document");
            using RegistryKey userChoice = root.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.txt\UserChoice");
            userChoice.SetValue("ProgId", "OtherEditor.Document");
            using RegistryKey existingOpenWith = existingExtension.CreateSubKey("OpenWithProgids");
            existingOpenWith.SetValue("OtherEditor.Document", "");
            using RegistryKey supportedTypes = root.CreateSubKey($@"{applicationPath}\SupportedTypes");
            supportedTypes.SetValue(".png", "");
            supportedTypes.SetValue(".txt", "");

            Assert.True(FileAssociationUtilities.RegisterTextFileAssociations(root, exePath, iconsDirectory.FullName));
            Assert.False(FileAssociationUtilities.RegisterTextFileAssociations(root, exePath, iconsDirectory.FullName));
            Assert.Equal("OtherEditor.Document", existingExtension.GetValue(""));
            Assert.Equal("OtherEditor.Document", userChoice.GetValue("ProgId"));
            Assert.Equal("", existingOpenWith.GetValue("OtherEditor.Document"));

            XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
            XNamespace uap3 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/3";
            XNamespace desktop7 = "http://schemas.microsoft.com/appx/manifest/desktop/windows10/7";
            XDocument manifest = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "TextGrabPackage.appxmanifest"));
            Dictionary<string, string> packagedIcons = new(StringComparer.OrdinalIgnoreCase);
            foreach (XElement association in manifest.Descendants(uap3 + "FileTypeAssociation"))
            {
                if (association.Element(desktop7 + "Logo") is not XElement logo)
                    continue;

                XElement openVerb = Assert.Single(association.Descendants(uap3 + "Verb"));
                Assert.Equal("OpenInTextGrab", (string?)openVerb.Attribute("Id"));
                Assert.Equal("\"%1\"", (string?)openVerb.Attribute("Parameters"));
                foreach (XElement fileType in association.Descendants(uap + "FileType"))
                    packagedIcons.Add(fileType.Value, Path.GetFileName(logo.Value));
            }

            Assert.Equal(93, packagedIcons.Count);
            Assert.Equal("Text.ico", packagedIcons[".txt"]);
            Assert.Equal("Markdown.ico", packagedIcons[".md"]);
            Assert.Equal("Header.ico", packagedIcons[".rst"]);
            Assert.Equal("Table.ico", packagedIcons[".csv"]);
            Assert.Equal("Markup.ico", packagedIcons[".xml"]);
            Assert.Equal("Code.ico", packagedIcons[".cs"]);
            Assert.Equal("Code.ico", packagedIcons[".json"]);
            Assert.Equal("Code.ico", packagedIcons[".ini"]);

            using RegistryKey? registeredApplications = root.OpenSubKey(@"Software\RegisteredApplications");
            string capabilitiesPath = Assert.IsType<string>(registeredApplications?.GetValue("Text Grab"));
            using RegistryKey? fileAssociations = root.OpenSubKey($@"{capabilitiesPath}\FileAssociations");
            Assert.NotNull(fileAssociations);
            Assert.Equal(packagedIcons.Keys.Order(), fileAssociations.GetValueNames().Order());
            Assert.Equal([".png"], supportedTypes.GetValueNames());

            using RegistryKey? openWithCommand = root.OpenSubKey($@"{applicationPath}\shell\open\command");
            Assert.Equal(expectedCommand, openWithCommand?.GetValue(""));
            foreach ((string extension, string iconName) in packagedIcons)
            {
                string progId = Assert.IsType<string>(fileAssociations.GetValue(extension));
                using RegistryKey? openWith = root.OpenSubKey($@"Software\Classes\{extension}\OpenWithProgids");
                Assert.Equal("", openWith?.GetValue(progId));

                using RegistryKey? defaultAppCommand = root.OpenSubKey($@"Software\Classes\{progId}\shell\open\command");
                Assert.Equal(expectedCommand, defaultAppCommand?.GetValue(""));
                using RegistryKey? defaultIcon = root.OpenSubKey($@"Software\Classes\{progId}\DefaultIcon");
                string iconPath = Path.Combine(iconsDirectory.FullName, iconName);
                Assert.Equal($"\"{iconPath}\",0", defaultIcon?.GetValue(""));
                Assert.True(File.Exists(iconPath), $"Expected an extracted icon at {iconPath}");
            }

            string deletedIcon = Path.Combine(iconsDirectory.FullName, "Table.ico");
            File.Delete(deletedIcon);
            Assert.True(FileAssociationUtilities.RegisterTextFileAssociations(root, exePath, iconsDirectory.FullName));
            Assert.True(File.Exists(deletedIcon));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(testPath, throwOnMissingSubKey: false);
            iconsDirectory.Delete(recursive: true);
        }
    }
}
