using System.Reflection;
using Text_Grab;
using Text_Grab.Models;

namespace Tests;

public class EditTextWindowActionCatalogTests
{
    private readonly record struct ExpectedButtonAction(string ButtonText, string? Command = null, string? ClickEvent = null);

    [Fact]
    public void AllButtons_UsesResolvableEditTextCommandsAndClickEvents()
    {
        HashSet<string> commandNames = [.. EditTextWindow.GetRoutedCommands().Keys];
        HashSet<string> methodNames = [.. typeof(EditTextWindow)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(method => method.Name)];

        foreach (ButtonInfo button in ButtonInfo.AllButtons)
        {
            if (!string.IsNullOrWhiteSpace(button.Command))
                Assert.Contains(button.Command, commandNames);

            if (!string.IsNullOrWhiteSpace(button.ClickEvent))
                Assert.Contains(button.ClickEvent, methodNames);
        }
    }
}
