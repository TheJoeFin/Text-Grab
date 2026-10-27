using System.IO;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;

namespace Tests;

public class EditTextWindowGrabTemplateMenuTests
{
    [WpfFact]
    public void GrabTemplateMenuItem_BeforeFirstOpen_IsSubmenuHeaderWithLoadingPlaceholder()
    {
        XDocument document = XDocument.Load(GetEditTextWindowPath("xaml"));
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement menuElement = new(document.Descendants().Single(element =>
            (string?)element.Attribute(xamlNamespace + "Name") == "GrabTemplateMenuItem"));

        Assert.Equal("GrabTemplateMenuItem_SubmenuOpened", (string?)menuElement.Attribute("SubmenuOpened"));
        // Load the real menu fragment without constructing a window or resolving its settings.
        menuElement.Attribute("SubmenuOpened")!.Remove();
        MenuItem menu = Assert.IsType<MenuItem>(XamlReader.Parse(menuElement.ToString()));
        MenuItem captureMenu = new() { Header = "_Capture" };
        captureMenu.Items.Add(menu);

        Assert.True(menu.IsEnabled);
        Assert.True(menu.HasItems);
        Assert.Equal(MenuItemRole.SubmenuHeader, menu.Role);
        MenuItem placeholder = Assert.IsType<MenuItem>(Assert.Single(menu.Items.Cast<object>()));
        Assert.Equal("Loading…", placeholder.Header);
        Assert.False(placeholder.IsEnabled);
    }

    [Fact]
    public void ClearGrabTemplateMenuItems_RestoresPlaceholderAfterClearing()
    {
        string source = File.ReadAllText(GetEditTextWindowPath("xaml.cs"));
        int methodStart = source.IndexOf("private void ClearGrabTemplateMenuItems()", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        int methodEnd = source.IndexOf("private void ClearDynamicMenuItems()", methodStart, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart);
        string clearMethod = source[methodStart..methodEnd];

        Assert.Contains(
            "GrabTemplateMenuItem.Items.Clear();\r\n        GrabTemplateMenuItem.Items.Add(new MenuItem { Header = \"Loading…\", IsEnabled = false });",
            clearMethod,
            StringComparison.Ordinal);
    }

    private static string GetEditTextWindowPath(string extension)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Text-Grab.sln")))
                return Path.Combine(directory.FullName, "Text-Grab", "Views", $"EditTextWindow.{extension}");

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Text Grab repository.");
    }
}
