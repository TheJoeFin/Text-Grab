using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Text_Grab.Controls;

public class CustomButtons
{
    string ExpandedText { get; set; } = "";
    string Icon { get; set; } = "";
    string Background { get; set; } = "Transparent";
    string Command { get; set; } = "";
    bool IsSymbol { get; set; } = false;

    static List<CustomButtons> ButtonList { get; set; } = new()
    {
        new()
        {
            ExpandedText = "Copy and Close",
            Icon = "",
            Background = "#CC7000",
            Command = "CopyClose"
        },
        new()
        {
            ExpandedText = "Save to File...",
            Icon = "",
            Command = "Save"
        },
        new()
        {
            ExpandedText = "Make Single Line",
            Icon = "",
            Command = "SingleLine"
        },
        new()
        {
            ExpandedText = "New Fullscreen Grab",
            Icon = "",
            Command = "NewFullscreen",
            IsSymbol = true
        },
        new()
        {
            ExpandedText = "Open Grab Frame",
            Icon = "",
            Command = "OpenGrabFrame",
            IsSymbol = true
        },
        new()
        {
            ExpandedText = "Find and Replace",
            Icon = "",
            Command = "SearchButton",
            IsSymbol = true
        },
    };
}


