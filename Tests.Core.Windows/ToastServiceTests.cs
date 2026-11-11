using System.Xml.Linq;
using Text_Grab.Services;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// <see cref="ToastService"/> (v5 F4). These check what the toast would contain and never call
/// <c>Show</c>: from an unpackaged test host that would register the test runner's own app ID in the
/// registry and put a real toast on the screen.
/// </summary>
public class ToastServiceTests
{
    [Fact]
    public void Toast_CarriesTheTitleAndBody()
    {
        XDocument toast = Xml(ToastService.BuildToast("Workflow finished", "3 files OCR'd"));

        Assert.Equal(["Workflow finished", "3 files OCR'd"], Texts(toast));
    }

    [Fact]
    public void Toast_HasNoActivationArgument_SoAClickDoesNothingNew()
    {
        XElement root = Xml(ToastService.BuildToast("Title", "Body")).Root!;

        Assert.True(string.IsNullOrEmpty((string?)root.Attribute("launch")), $"launch was '{root.Attribute("launch")}'");
        Assert.Empty(root.Descendants("action"));
    }

    [Fact]
    public void LongText_IsTruncatedLikeTheGrabToast()
    {
        string body = new('x', 400);

        string[] texts = Texts(Xml(ToastService.BuildToast("Title", body)));

        Assert.Equal(new string('x', ToastService.MaxTextLength) + "...", texts[1]);
    }

    [Fact]
    public void Truncation_NeverSplitsASurrogatePair()
    {
        // The emoji's high surrogate lands exactly on the last kept position.
        string body = new string('x', ToastService.MaxTextLength - 1) + "😀" + "tail";

        string shown = Texts(Xml(ToastService.BuildToast("Title", body)))[1];

        Assert.Equal(new string('x', ToastService.MaxTextLength - 1) + "...", shown);
    }

    [Fact]
    public void NullText_IsShownAsEmpty()
    {
        Assert.Equal(["", ""], Texts(Xml(ToastService.BuildToast(null, null))));
    }

    private static XDocument Xml(Microsoft.Toolkit.Uwp.Notifications.ToastContentBuilder builder)
        => XDocument.Parse(builder.Content.GetContent());

    private static string[] Texts(XDocument toast)
        => [.. toast.Descendants("text").Select(t => t.Value)];
}
