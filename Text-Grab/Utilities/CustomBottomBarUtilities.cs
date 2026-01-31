using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Controls;
using Text_Grab.Models;
using Wpf.Ui.Controls;

namespace Text_Grab.Utilities;

public class CustomBottomBarUtilities
{
    private static readonly JsonSerializerOptions ButtonInfoJsonOptions = new();
    private static readonly Dictionary<Type, List<MethodInfo>> _methodCache = [];
    private static readonly Lock _methodCacheLock = new();
    private static readonly BrushConverter BrushConverter = new();

    public static List<ButtonInfo> GetCustomBottomBarItemsSetting()
    {
        string json = AppUtilities.TextGrabSettings.BottomButtonsJson;

        if (string.IsNullOrWhiteSpace(json))
            return ButtonInfo.DefaultButtonList;

        List<ButtonInfo>? customBottomBarItems = [];

        customBottomBarItems = JsonSerializer.Deserialize<List<ButtonInfo>>(json, ButtonInfoJsonOptions);

        if (customBottomBarItems is null || customBottomBarItems.Count == 0)
            return ButtonInfo.DefaultButtonList;

        // SymbolIcon is not serialized (marked with [JsonIgnore]), so reconstruct it from ButtonText
        Dictionary<string, SymbolRegular> buttonDictionary = ButtonInfo.AllButtons.ToDictionary(button => button.ButtonText, button => button.SymbolIcon);

        foreach (ButtonInfo buttonInfo in customBottomBarItems)
        {
            if (buttonDictionary.TryGetValue(buttonInfo.ButtonText, out SymbolRegular symbol))
                buttonInfo.SymbolIcon = symbol;
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
        string json = JsonSerializer.Serialize(bottomBarButtons, ButtonInfoJsonOptions);
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
        BrushConverter _brushConverter = new();

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
                && _brushConverter.ConvertFromString(buttonItem.Background) is SolidColorBrush solidColorBrush)
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
        Type type = obj.GetType();

        if (_methodCache.TryGetValue(type, out List<MethodInfo>? cachedMethods))
            return cachedMethods;

        lock (_methodCacheLock)
        {
            // Double-check after acquiring lock
            if (_methodCache.TryGetValue(type, out cachedMethods))
                return cachedMethods;

            List<MethodInfo> methods = [.. type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)];
            _methodCache[type] = methods;
            return methods;
        }
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
