using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Controls;
using Text_Grab.Models;
using Wpf.Ui.Controls;

namespace Text_Grab.Utilities;

public class CustomBottomBarUtilities
{
    public static List<ButtonInfo> GetCustomBottomBarItemsSetting()
    {
        string json = AppUtilities.TextGrabSettings.BottomButtonsJson;

        if (string.IsNullOrWhiteSpace(json))
            return ButtonInfo.DefaultButtonList;

        List<ButtonInfo>? customBottomBarItems = [];

        customBottomBarItems = JsonSerializer.Deserialize<List<ButtonInfo>>(json);

        if (customBottomBarItems is null || customBottomBarItems.Count == 0)
            return ButtonInfo.DefaultButtonList;

        // check to see if the first element is using the default symbol of Diamond24
        // which is unused by any button
        if (customBottomBarItems.First().SymbolIcon == SymbolRegular.Diamond24)
        {
            // Migrate to the new SymbolRegular instead of the old symbols.
            Dictionary<string, SymbolRegular> buttonDictionary = ButtonInfo.AllButtons.ToDictionary(button => button.ButtonText, button => button.SymbolIcon);

            foreach (ButtonInfo buttonInfo in customBottomBarItems)
                buttonInfo.SymbolIcon = buttonDictionary[buttonInfo.ButtonText];
        }

        return customBottomBarItems;
    }

    public static void SaveCustomBottomBarItemsSetting(List<CollapsibleButton> bottomBarButtons)
    {
        List<ButtonInfo> customButtons = [];

        foreach (CollapsibleButton collapsible in bottomBarButtons)
            customButtons.Add(new(collapsible));

        SaveCustomBottomBarItemsSetting(customButtons);
    }

    public static void SaveCustomBottomBarItemsSetting(List<ButtonInfo> bottomBarButtons)
    {
        string json = JsonSerializer.Serialize(bottomBarButtons);
        AppUtilities.TextGrabSettings.BottomButtonsJson = json;
        AppUtilities.TextGrabSettings.Save();
    }

    public static List<CollapsibleButton> GetBottomBarButtons(EditTextWindow editTextWindow)
    {
        List<CollapsibleButton> bottomBarButtons = [];
        Dictionary<string, RoutedCommand> _localRoutedCommands = [];
        List<MethodInfo> methods = GetMethods(editTextWindow);
        Dictionary<string, RoutedCommand> routedCommands = EditTextWindow.GetRoutedCommands();

        int index = 1;

        foreach (ButtonInfo buttonItem in GetCustomBottomBarItemsSetting())
        {
            CollapsibleButton button = new()
            {
                ButtonText = buttonItem.ButtonText,
                IsSymbol = buttonItem.IsSymbol,
                CustomButton = buttonItem,
                ToolTip = $"{buttonItem.ButtonText} (ctrl + {index})",
                ButtonSymbol = buttonItem.SymbolIcon
            };

            if (buttonItem.Background != "Transparent"
                && new BrushConverter()
                .ConvertFromString(buttonItem.Background) is SolidColorBrush solidColorBrush)
            {
                button.Background = solidColorBrush;
            }

            if (GetMethodInfoForName(buttonItem.ClickEvent, methods) is MethodInfo method
                && method.CreateDelegate(typeof(RoutedEventHandler), editTextWindow) is RoutedEventHandler routedEventHandler)
                button.Click += routedEventHandler;
            else
                if (GetCommandBinding(buttonItem.Command, routedCommands) is RoutedCommand routedCommand)
                button.Command = routedCommand;

            bottomBarButtons.Add(button);
            index++;
        }

        return bottomBarButtons;
    }

    private static List<MethodInfo> GetMethods(object obj)
    {
        return [.. obj.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)];
    }

    private static MethodInfo? GetMethodInfoForName(string methodName, List<MethodInfo> methods)
    {
        foreach (MethodInfo method in methods)
            if (method.Name == methodName)
                return method;

        return null;
    }

    private static RoutedCommand? GetCommandBinding(string commandName, Dictionary<string, RoutedCommand> routedCommands)
    {
        foreach (string commandKey in routedCommands.Keys)
            if (commandKey == commandName)
                return routedCommands[commandKey];

        return null;
    }
}
