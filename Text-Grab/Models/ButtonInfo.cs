using System.Collections.Generic;
using Text_Grab.Controls;

namespace Text_Grab.Models;

public class ButtonInfo
{
    public double OrderNumber { get; set; } = 0.1;
    public string ButtonText { get; set; } = "";
    public string SymbolText { get; set; } = "";
    public string Background { get; set; } = "Transparent";
    public string Command { get; set; } = "";
    public string ClickEvent { get; set; } = "";
    public bool IsSymbol { get; set; } = false;

    public ButtonInfo()
    {

    }

    public override bool Equals(object? obj)
    {
        if (obj is not ButtonInfo otherButton)
            return false;

        return otherButton.GetHashCode() == GetHashCode();
    }

    public override int GetHashCode()
    {
        int hash = 17;
        hash = (hash * 23) + ButtonText.GetHashCode();
        hash = (hash * 23) + SymbolText.GetHashCode();
        hash = (hash * 23) + Background.GetHashCode();
        hash = (hash * 23) + Command.GetHashCode();
        hash = (hash * 23) + ClickEvent.GetHashCode();
        return hash;
    }

    // a constructor which takes a collapisble button
    public ButtonInfo(CollapsibleButton button)
    {
        if (button.CustomButton is not null)
        {
            ButtonText = button.CustomButton.ButtonText;
            SymbolText = button.CustomButton.SymbolText;
            Background = button.CustomButton.Background;
            Command = button.CustomButton.Command;
            ClickEvent = button.CustomButton.ClickEvent;
            IsSymbol = button.CustomButton.IsSymbol;
        }
        else
        {
            ButtonText = button.ButtonText;
            SymbolText = button.SymbolText;
            Background = button.Background.ToString();
            IsSymbol = button.IsSymbol;
        }
    }

    // a constructor with parameters
    public ButtonInfo(string buttonText, string symbolText, string background, string command, string clickEvent, bool isSymbol)
    {
        ButtonText = buttonText;
        SymbolText = symbolText;
        Background = background;
        Command = command;
        ClickEvent = clickEvent;
        IsSymbol = isSymbol;
    }

    public static List<ButtonInfo> DefaultButtonList { get; set; } = new()
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

    public static List<ButtonInfo> AllButtons { get; set; } = new()
    {
        new()
        {
            OrderNumber = 1.1,
            ButtonText = "Copy and Close",
            SymbolText = "",
            Background = "#CC7000",
            ClickEvent = "CopyCloseBTN_Click"
        },
        new()
        {
            OrderNumber = 1.2,
            ButtonText = "Save to File...",
            SymbolText = "",
            ClickEvent = "SaveBTN_Click"
        },
        new()
        {
            OrderNumber = 1.3,
            ButtonText = "Make Single Line",
            SymbolText = "",
            Command = "SingleLineCmd"
        },
        new()
        {
            OrderNumber = 1.4,
            ButtonText = "New Fullscreen Grab",
            SymbolText = "",
            ClickEvent = "NewFullscreen_Click",
        },
        new()
        {
            OrderNumber = 1.41,
            ButtonText = "Fullscreen Grab With Delay",
            SymbolText = "",
            ClickEvent = "FSGDelayMenuItem_Click",
        },
        new()
        {
            OrderNumber = 1.5,
            ButtonText = "Open Grab Frame",
            SymbolText = "",
            ClickEvent = "OpenGrabFrame_Click",
        },
        new()
        {
            OrderNumber = 1.6,
            ButtonText = "Find and Replace",
            SymbolText = "",
            ClickEvent = "SearchButton_Click",
        },
        new()
        {
            OrderNumber = 2.1,
            ButtonText = "Open Settings",
            SymbolText = "",
            ClickEvent = "SettingsMenuItem_Click"
        },
        new()
        {
            OrderNumber = 2.2,
            ButtonText = "Open File...",
            SymbolText = "",
            ClickEvent = "OpenFileMenuItem_Click"
        },
        new()
        {
            OrderNumber = 2.3,
            ButtonText = "OCR Paste",
            SymbolText = "",
            Command = "PasteCommand"
        },
        new()
        {
            OrderNumber = 3.1,
            ButtonText = "Trim Each Line",
            SymbolText = "",
            ClickEvent = "TrimEachLineMenuItem_Click"
        },
        new()
        {
            OrderNumber = 3.2,
            ButtonText = "Try to make Numbers",
            SymbolText = "",
            ClickEvent = "TryToNumberMenuItem_Click"
        },
        new()
        {
            OrderNumber = 3.3,
            ButtonText = "Try to make Letters",
            SymbolText = "",
            ClickEvent = "TryToAlphaMenuItem_Click"
        },
        new()
        {
            OrderNumber = 3.4,
            ButtonText = "Toggle Case",
            SymbolText = "",
            Command = "ToggleCaseCmd"
        },
        new()
        {
            OrderNumber = 3.5,
            ButtonText = "Remove Duplicate Lines",
            SymbolText = "",
            ClickEvent = "RemoveDuplicateLines_Click"
        },
        new()
        {
            OrderNumber = 3.6,
            ButtonText = "Replace Reserved Characters",
            SymbolText = "",
            Command = "ReplaceReservedCmd"
        },
        new()
        {
            OrderNumber = 3.7,
            ButtonText = "Unstack Text (Select Top Row)",
            SymbolText = "",
            Command = "UnstackCmd"
        },
        new()
        {
            OrderNumber = 3.8,
            ButtonText = "Unstack Text (Select First Column)",
            SymbolText = "",
            Command = "UnstackGroupCmd"
        },
        new()
        {
            OrderNumber = 3.9,
            ButtonText = "Add or Remove at...",
            SymbolText = "",
            ClickEvent = "AddRemoveAtMenuItem_Click"
        },
        new()
        {
            OrderNumber = 4.1,
            ButtonText = "Select Word",
            SymbolText = "",
            ClickEvent = "SelectWordMenuItem_Click"
        },
        new()
        {
            OrderNumber = 4.2,
            ButtonText = "Select Line",
            SymbolText = "",
            ClickEvent = "SelectLineMenuItem_Click"
        },
        new()
        {
            OrderNumber = 4.3,
            ButtonText = "Move Line Up",
            SymbolText = "",
            ClickEvent = "MoveLineUpMenuItem_Click"
        },
        new()
        {
            OrderNumber = 4.4,
            ButtonText = "Move Line Down",
            SymbolText = "",
            ClickEvent = "MoveLineDownMenuItem_Click"
        },
        new()
        {
            OrderNumber = 4.5,
            ButtonText = "Split on Selection",
            SymbolText = "",
            Command = "SplitOnSelectionCmd"
        },
        new()
        {
            OrderNumber = 4.6,
            ButtonText = "Isolate Selection",
            SymbolText = "",
            Command = "IsolateSelectionCmd"
        },
        new()
        {
            OrderNumber = 4.7,
            ButtonText = "Delete All of Selection",
            SymbolText = "",
            Command = "DeleteAllSelectionCmd"
        },
        new()
        {
            OrderNumber = 4.8,
            ButtonText = "Delete All of Pattern",
            SymbolText = "",
            Command = "DeleteAllSelectionPatternCmd"
        },
        new()
        {
            OrderNumber = 4.9,
            ButtonText = "Insert on Every Line",
            SymbolText = "",
            Command = "InsertSelectionOnEveryLineCmd"
        },
        new()
        {
            OrderNumber = 5.1,
            ButtonText = "New Quick Simple Lookup",
            SymbolText = "",
            ClickEvent = "LaunchQuickSimpleLookup"
        },
        new()
        {
            OrderNumber = 5.2,
            ButtonText = "List Files and Folders...",
            SymbolText = "",
            ClickEvent = "ListFilesMenuItem_Click"
        },
        new()
        {
            OrderNumber = 5.3,
            ButtonText = "Extract Text from Images...",
            SymbolText = "",
            ClickEvent = "ReadFolderOfImages_Click"
        },
        new()
        {
            OrderNumber = 5.4,
            ButtonText = "Extract Text from Images to txt Files...",
            SymbolText = "",
            ClickEvent = "ReadFolderOfImagesWriteTxtFiles_Click"
        },
        new()
        {
            OrderNumber = 5.5,
            ButtonText = "New Window",
            SymbolText = "",
            ClickEvent = "NewWindow_Clicked"
        },
        new()
        {
            OrderNumber = 5.6,
            ButtonText = "New Window from Selection",
            SymbolText = "",
            ClickEvent = "NewWindowWithText_Clicked"
        }
    };
}


