using System.Collections.Generic;

namespace Text_Grab.Controls;

public class CustomButton
{
    public string ButtonText { get; set; } = "";
    public string SymbolText { get; set; } = "";
    public string Background { get; set; } = "Transparent";
    public string Command { get; set; } = "";
    public bool IsSymbol { get; set; } = false;

    public static List<CustomButton> DefaultButtonList { get; set; } = new()
    {
        new()
        {
            ButtonText = "Copy and Close",
            SymbolText = "",
            Background = "#CC7000",
            Command = "CopyClose"
        },
        new()
        {
            ButtonText = "Save to File...",
            SymbolText = "",
            Command = "Save"
        },
        new()
        {
            ButtonText = "Make Single Line",
            SymbolText = "",
            Command = "SingleLine"
        },
        new()
        {
            ButtonText = "New Fullscreen Grab",
            SymbolText = "",
            Command = "NewFullscreen",
            IsSymbol = true
        },
        new()
        {
            ButtonText = "Open Grab Frame",
            SymbolText = "",
            Command = "OpenGrabFrame",
            IsSymbol = true
        },
        new()
        {
            ButtonText = "Find and Replace",
            SymbolText = "",
            Command = "SearchButton",
            IsSymbol = true
        },
    };
}


