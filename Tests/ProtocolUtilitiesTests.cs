using System;
using System.IO;
using Text_Grab.Utilities;

namespace Tests;

public class ProtocolUtilitiesTests
{
    [Theory]
    [InlineData("text-grab://paste-spreadsheet")]
    [InlineData("TEXT-GRAB://EDIT-TEXT")]
    [InlineData("text-grab:grab-frame")]
    public void IsProtocolUri_RecognizesProtocolArguments(string argument)
    {
        Assert.True(ProtocolUtilities.IsProtocolUri(argument));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Settings")]
    [InlineData(@"C:\images\screenshot.png")]
    [InlineData("https://example.com")]
    [InlineData("--windowless")]
    public void IsProtocolUri_RejectsOtherArguments(string? argument)
    {
        Assert.False(ProtocolUtilities.IsProtocolUri(argument));
    }

    [Theory]
    [InlineData("text-grab://paste-spreadsheet", "paste-spreadsheet")]
    [InlineData("text-grab://edit-text", "edit-text")]
    [InlineData("text-grab://grab-frame", "grab-frame")]
    [InlineData("text-grab://grab-text", "grab-text")]
    [InlineData("text-grab://fullscreen", "fullscreen")]
    [InlineData("text-grab://quick-lookup", "quick-lookup")]
    [InlineData("text-grab://settings", "settings")]
    public void TryParseProtocolUri_ParsesCommands(string uri, string expectedCommand)
    {
        bool parsed = ProtocolUtilities.TryParseProtocolUri(uri, out string command, out _);

        Assert.True(parsed);
        Assert.Equal(expectedCommand, command);
    }

    [Theory]
    [InlineData("TEXT-GRAB://Paste-Spreadsheet", "paste-spreadsheet")]
    [InlineData("text-grab://paste-spreadsheet/", "paste-spreadsheet")]
    [InlineData("text-grab:paste-spreadsheet", "paste-spreadsheet")]
    public void TryParseProtocolUri_NormalizesCommandForms(string uri, string expectedCommand)
    {
        bool parsed = ProtocolUtilities.TryParseProtocolUri(uri, out string command, out _);

        Assert.True(parsed);
        Assert.Equal(expectedCommand, command);
    }

    [Fact]
    public void TryParseProtocolUri_ExtractsUrlEncodedPathParameter()
    {
        string localPath = @"C:\Users\joe\Downloads\TextGrab\capture 2026-06-12.png";
        string uri = $"text-grab://grab-frame?path={Uri.EscapeDataString(localPath)}";

        bool parsed = ProtocolUtilities.TryParseProtocolUri(uri, out string command, out Dictionary<string, string> parameters);

        Assert.True(parsed);
        Assert.Equal("grab-frame", command);
        Assert.Equal(localPath, parameters["path"]);
    }

    [Fact]
    public void TryParseProtocolUri_ParsesGrabTextWithPath()
    {
        string localPath = @"C:\Users\joe\Downloads\TextGrab\image-2026-06-12.png";
        string uri = $"text-grab://grab-text?path={Uri.EscapeDataString(localPath)}";

        bool parsed = ProtocolUtilities.TryParseProtocolUri(uri, out string command, out Dictionary<string, string> parameters);

        Assert.True(parsed);
        Assert.Equal("grab-text", command);
        Assert.Equal(localPath, parameters["path"]);
    }

    [Fact]
    public void TryParseProtocolUri_ParameterKeysAreCaseInsensitive()
    {
        bool parsed = ProtocolUtilities.TryParseProtocolUri(
            "text-grab://grab-frame?PATH=C%3A%5Cimage.png",
            out _,
            out Dictionary<string, string> parameters);

        Assert.True(parsed);
        Assert.Equal(@"C:\image.png", parameters["path"]);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("not a uri")]
    [InlineData("text-grab://")]
    [InlineData("")]
    public void TryParseProtocolUri_RejectsInvalidUris(string uri)
    {
        Assert.False(ProtocolUtilities.TryParseProtocolUri(uri, out _, out _));
    }

    [Fact]
    public void TryParseProtocolUri_IgnoresMalformedQueryPairs()
    {
        bool parsed = ProtocolUtilities.TryParseProtocolUri(
            "text-grab://grab-frame?=novalue&path=C%3A%5Ca.png&flag",
            out string command,
            out Dictionary<string, string> parameters);

        Assert.True(parsed);
        Assert.Equal("grab-frame", command);
        Assert.Single(parameters);
        Assert.Equal(@"C:\a.png", parameters["path"]);
    }

    // ── TryGetSafeProtocolFilePath ────────────────────────────────────────────

    [Fact]
    public void TryGetSafeProtocolFilePath_AcceptsImageInTempFolder()
    {
        string tempImage = Path.Combine(Path.GetTempPath(), $"text-grab-test-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(tempImage, [0]);
        try
        {
            bool safe = ProtocolUtilities.TryGetSafeProtocolFilePath(tempImage, out string fullPath);

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
        Assert.False(ProtocolUtilities.TryGetSafeProtocolFilePath(path, out _));
    }

    [Fact]
    public void TryGetSafeProtocolFilePath_RejectsPathOutsideAllowedRoots()
    {
        // The Windows folder is never an allowed root; rejection happens before any
        // existence check, so the file need not exist.
        string outside = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            $"text-grab-{Guid.NewGuid():N}.png");

        Assert.False(ProtocolUtilities.TryGetSafeProtocolFilePath(outside, out _));
    }

    [Fact]
    public void TryGetSafeProtocolFilePath_RejectsTraversalEscapingAllowedRoot()
    {
        // Starts inside Temp but climbs out to the Windows folder.
        string traversal = Path.Combine(Path.GetTempPath(), "..", "..", "..", "Windows", "image.png");

        Assert.False(ProtocolUtilities.TryGetSafeProtocolFilePath(traversal, out _));
    }

    [Fact]
    public void TryGetSafeProtocolFilePath_RejectsNonImageExtensionInAllowedRoot()
    {
        string tempText = Path.Combine(Path.GetTempPath(), $"text-grab-test-{Guid.NewGuid():N}.txt");
        File.WriteAllText(tempText, "hello");
        try
        {
            Assert.False(ProtocolUtilities.TryGetSafeProtocolFilePath(tempText, out _));
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

        Assert.False(ProtocolUtilities.TryGetSafeProtocolFilePath(missing, out _));
    }
}
