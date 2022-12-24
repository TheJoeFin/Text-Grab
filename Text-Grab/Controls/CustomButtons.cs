using System.Collections.Generic;

namespace Text_Grab.Controls;

public class CustomButton
{
    public string ButtonText { get; set; } = "";
    public string SymbolText { get; set; } = "";
    public string Background { get; set; } = "Transparent";
    public string Command { get; set; } = "";
    public string ClickEvent { get; set; } = "";
    public bool IsSymbol { get; set; } = false;

    public static List<CustomButton> DefaultButtonList { get; set; } = new()
    {
        new()
        {
            ButtonText = "Copy and Close",
            SymbolText = "",
            Background = "#CC7000",
            ClickEvent = "CopyCloseBTN_Click"
        },
        new()
        {
            ButtonText = "Save to File...",
            SymbolText = "",
            ClickEvent = "SaveBTN_Click"
        },
        new()
        {
            ButtonText = "Make Single Line",
            SymbolText = "",
            Command = "SingleLineCmd"
        },
        new()
        {
            ButtonText = "New Fullscreen Grab",
            SymbolText = "",
            ClickEvent = "NewFullscreen_Click",
            IsSymbol = true
        },
        new()
        {
            ButtonText = "Open Grab Frame",
            SymbolText = "",
            ClickEvent = "OpenGrabFrame_Click",
            IsSymbol = true
        },
        new()
        {
            ButtonText = "Find and Replace",
            SymbolText = "",
            ClickEvent = "SearchButton_Click",
            IsSymbol = true
        },
    };
}


