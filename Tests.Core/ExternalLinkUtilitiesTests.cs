using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

public class ExternalLinkUtilitiesTests
{
    [Theory]
    [InlineData("https://example.invalid", "https://example.invalid/")]
    [InlineData("http://example.invalid/path?value=1#section", "http://example.invalid/path?value=1#section")]
    [InlineData(" HTTPS://EXAMPLE.INVALID:443/a b?value=1#section ", "https://example.invalid/a%20b?value=1#section")]
    [InlineData("\r\nhttps://example.invalid/\t", "https://example.invalid/")]
    [InlineData("http://example.invalid:8080/", "http://example.invalid:8080/")]
    [InlineData("https://[::1]:8443/path", "https://[::1]:8443/path")]
    public void TryCreate_WebUrl_ReturnsCanonicalAbsoluteUri(string input, string expected)
    {
        Assert.True(ExternalLinkUtilities.TryCreate(input, out Uri? uri));

        Assert.NotNull(uri);
        Assert.Equal(expected, uri.AbsoluteUri);
        Assert.False(uri.IsFile);
        Assert.False(uri.IsUnc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n\t ")]
    [InlineData("not a url at all")]
    [InlineData("example.invalid")]
    [InlineData("/relative/path")]
    [InlineData("//example.invalid/share/evil.jpg")]
    [InlineData(@"\\192.0.2.5\share\evil.jpg")]
    [InlineData(@"                              \\192.0.2.5\share\evil.jpg")]
    [InlineData(@"\\?\UNC\example.invalid\share\evil.jpg")]
    [InlineData("file://example.invalid/share/evil.jpg")]
    [InlineData("                              file://example.invalid/share/evil.jpg")]
    [InlineData(@"C:\Windows\notepad.exe")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData(@"C:relative.txt")]
    [InlineData("ftp://example.invalid/file")]
    [InlineData("mailto:")]
    [InlineData("mailto:?subject=hello")]
    [InlineData("mailto://example.invalid/share")]
    [InlineData("mailto:user@example.invalid/path")]
    [InlineData("mailto:user@example.invalid?attach=file://example.invalid/share/document")]
    [InlineData("mailto:user@example.invalid?%61ttach=C:\\document.txt")]
    [InlineData("mailto:user@example.invalid?bcc=hidden@example.invalid")]
    [InlineData("mailto:user@example.invalid?subject=first&SUBJECT=second")]
    [InlineData("mailto:user@example.invalid?subject=hello%0D%0ABcc:other@example.invalid")]
    [InlineData("mailto:user@example.invalid?body=hello%00world")]
    [InlineData("mailto:user@example.invalid#fragment")]
    [InlineData("mailto:user%0Aother@example.invalid")]
    [InlineData("tel:")]
    [InlineData("tel://example.invalid")]
    [InlineData("tel:alphabetic")]
    [InlineData("tel:++15551234567")]
    [InlineData("tel:+15551234567?call=true")]
    [InlineData("tel:+15551234567#fragment")]
    [InlineData("tel:+15551234567;ext=")]
    [InlineData("tel:+15551234567;ext=abc")]
    [InlineData("tel:+15551234567;postd=123")]
    [InlineData("tel:+15551234567;ext=1;ext=2")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-settings:display")]
    [InlineData("text-grab:Fullscreen")]
    [InlineData("https:")]
    [InlineData("https:///")]
    [InlineData("https://")]
    [InlineData("https://example.invalid:invalid/")]
    [InlineData("https://exa\tmple.invalid/")]
    [InlineData("https://example.invalid/a\r\nb")]
    [InlineData("https://example.invalid/a\0b")]
    public void TryCreate_UnsupportedOrInvalidTarget_ReturnsFalseAndNoUri(string? input)
    {
        Assert.False(ExternalLinkUtilities.TryCreate(input, out Uri? uri));
        Assert.Null(uri);
    }

    [Theory]
    [InlineData("mailto:user@example.invalid", "mailto:user@example.invalid")]
    [InlineData(" MAILTO:user@EXAMPLE.INVALID ", "mailto:user@example.invalid")]
    [InlineData("mailto:user@example.invalid?subject=Hello%20there&body=Line%0D%0Atwo", "mailto:user@example.invalid?subject=Hello%20there&body=Line%0D%0Atwo")]
    [InlineData("tel:+1-555-123-4567", "tel:+1-555-123-4567")]
    [InlineData(" TEL:+15551234567;ext=23 ", "tel:+15551234567;ext=23")]
    [InlineData("tel:555.0123", "tel:555.0123")]
    public void TryCreate_EmailAndTelephoneLinks_RequireConfirmation(string input, string expected)
    {
        Assert.True(ExternalLinkUtilities.TryCreate(input, out Uri? uri));

        Assert.NotNull(uri);
        Assert.Equal(expected, uri.AbsoluteUri);
        Assert.True(ExternalLinkUtilities.RequiresConfirmation(uri));
    }

    [Theory]
    [InlineData("https://example.invalid/")]
    [InlineData("http://example.invalid/")]
    public void RequiresConfirmation_WebLinks_DoNotRequireConfirmation(string input)
    {
        Assert.True(ExternalLinkUtilities.TryCreate(input, out Uri? uri));

        Assert.NotNull(uri);
        Assert.False(ExternalLinkUtilities.RequiresConfirmation(uri));
    }

    [Theory]
    [InlineData(@"\\192.0.2.5\share\evil.jpg")]
    [InlineData("file://example.invalid/share/evil.jpg")]
    [InlineData("ms-settings:display")]
    public void TryCreate_AbsoluteUriAloneDoesNotAuthorizeLaunching(string input)
    {
        Assert.True(Uri.TryCreate(input, UriKind.Absolute, out _));
        Assert.False(ExternalLinkUtilities.TryCreate(input, out _));
    }
}
