using System;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

// Pure half of the original Tests/ProtocolUtilitiesTests.cs (batch 7a): IsProtocolUri and
// TryParseProtocolUri live on Text-Grab.Core's ProtocolUtilities. The
// TryGetSafeProtocolFilePath tests stayed behind as Tests/ProtocolHandlerUtilitiesTests.cs -
// ProtocolHandlerUtilities is app-side (Text-Grab/Utilities/ProtocolHandlerUtilities.cs). This
// half has 9 methods against that one's 6, so it kept the original name.
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
}
