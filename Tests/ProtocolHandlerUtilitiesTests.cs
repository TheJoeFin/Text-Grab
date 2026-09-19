using System;
using System.IO;
using Text_Grab.Utilities;

namespace Tests;

// App-side half of the original Tests/ProtocolUtilitiesTests.cs (batch 7a): ProtocolHandlerUtilities
// (Text-Grab/Utilities/ProtocolHandlerUtilities.cs) is internal and app-side by design - it
// validates a companion app's path= parameter against the filesystem. The pure IsProtocolUri/
// TryParseProtocolUri tests moved to Tests.Core/ProtocolUtilitiesTests.cs, which kept the name.
public class ProtocolHandlerUtilitiesTests
{
    [Fact]
    public void TryGetSafeProtocolFilePath_AcceptsImageInTempFolder()
    {
        string tempImage = Path.Combine(Path.GetTempPath(), $"text-grab-test-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(tempImage, [0]);
        try
        {
            bool safe = ProtocolHandlerUtilities.TryGetSafeProtocolFilePath(tempImage, out string fullPath);

            Assert.True(safe);
            Assert.Equal(Path.GetFullPath(tempImage), fullPath);
        }
        finally
        {
            File.Delete(tempImage);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"\\server\share\image.png")]      // UNC: would trigger an SMB credential leak
    [InlineData("//server/share/image.png")]       // forward-slash UNC
    [InlineData(@"\\?\C:\Windows\image.png")]       // extended-length device path
    [InlineData(@"\\.\PhysicalDrive0")]             // device namespace
    public void TryGetSafeProtocolFilePath_RejectsUncDeviceAndEmptyPaths(string? path)
    {
        Assert.False(ProtocolHandlerUtilities.TryGetSafeProtocolFilePath(path, out _));
    }

    [Fact]
    public void TryGetSafeProtocolFilePath_RejectsPathOutsideAllowedRoots()
    {
        // The Windows folder is never an allowed root; rejection happens before any
        // existence check, so the file need not exist.
        string outside = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            $"text-grab-{Guid.NewGuid():N}.png");

        Assert.False(ProtocolHandlerUtilities.TryGetSafeProtocolFilePath(outside, out _));
    }

    [Fact]
    public void TryGetSafeProtocolFilePath_RejectsTraversalEscapingAllowedRoot()
    {
        // Starts inside Temp but climbs out to the Windows folder.
        string traversal = Path.Combine(Path.GetTempPath(), "..", "..", "..", "Windows", "image.png");

        Assert.False(ProtocolHandlerUtilities.TryGetSafeProtocolFilePath(traversal, out _));
    }

    [Fact]
    public void TryGetSafeProtocolFilePath_RejectsNonImageExtensionInAllowedRoot()
    {
        string tempText = Path.Combine(Path.GetTempPath(), $"text-grab-test-{Guid.NewGuid():N}.txt");
        File.WriteAllText(tempText, "hello");
        try
        {
            Assert.False(ProtocolHandlerUtilities.TryGetSafeProtocolFilePath(tempText, out _));
        }
        finally
        {
            File.Delete(tempText);
        }
    }

    [Fact]
    public void TryGetSafeProtocolFilePath_RejectsNonexistentImageInAllowedRoot()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"text-grab-missing-{Guid.NewGuid():N}.png");

        Assert.False(ProtocolHandlerUtilities.TryGetSafeProtocolFilePath(missing, out _));
    }
}
