using Text_Grab.Core.Actions;
using Wpf.Ui.Controls;

namespace Tests;

/// <summary>
/// Core keeps icon keys as plain strings so it never references Wpf.Ui. The app maps each key to the
/// <see cref="SymbolRegular"/> member of the same name, so every key must name one.
/// </summary>
public class ActionIconKeyTests
{
    [Fact]
    public void EveryRegisteredIconKey_IsASymbolRegularName()
    {
        ActionRegistry registry = new();
        CoreActions.RegisterAll(registry);
        WindowsActions.RegisterAll(registry);

        foreach (ActionDescriptor descriptor in registry.All)
            Assert.True(
                Enum.TryParse(descriptor.IconKey, ignoreCase: false, out SymbolRegular _)
                    && Enum.IsDefined(typeof(SymbolRegular), descriptor.IconKey),
                $"{descriptor.Id} has icon key '{descriptor.IconKey}', which is not a SymbolRegular member.");
    }
}
