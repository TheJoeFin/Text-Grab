using System.Collections.Generic;
using System.Windows.Input;
using Text_Grab.Controls;

namespace Text_Grab.Models;

public class CustomButton
{
    public string ButtonText { get; set; } = "";
    public string SymbolText { get; set; } = "";
    public string Background { get; set; } = "Transparent";
    public string Command { get; set; } = "";
    public string ClickEvent { get; set; } = "";
    public bool IsSymbol { get; set; } = false;

    public CustomButton()
    {

    }
    
    // a constructor which takes a collapisble button
    public CustomButton(CollapsibleButton button)
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
    public CustomButton(string buttonText, string symbolText, string background, string command, string clickEvent, bool isSymbol)
    {
        ButtonText = buttonText;
        SymbolText = symbolText;
        Background = background;
        Command = command;
        ClickEvent = clickEvent;
        IsSymbol = isSymbol;
    }

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

    public static List<CustomButton> AllButtons { get; set; } = new()
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
        },
        new()
        {
            ButtonText = "Fullscreen Grab With Delay",
            SymbolText = "",
            ClickEvent = "FSGDelayMenuItem_Click",
        },
        new()
        {
            ButtonText = "Open Grab Frame",
            SymbolText = "",
            ClickEvent = "OpenGrabFrame_Click",
        },
        new()
        {
            ButtonText = "Find and Replace",
            SymbolText = "",
            ClickEvent = "SearchButton_Click",
        },
        new()
        {
            ButtonText = "Open Settings",
            SymbolText = "",
            ClickEvent = "SettingsMenuItem_Click"
        },
        new()
        {
            ButtonText = "Open File...",
            SymbolText = "",
            ClickEvent = "OpenFileMenuItem_Click"
        },
        new()
        {
            ButtonText = "OCR Paste",
            SymbolText = "",
            Command = "PasteCommand"
        },
        new()
        {
            ButtonText = "Trim Each Line",
            SymbolText = "",
            ClickEvent = "TrimEachLineMenuItem_Click"
        },
        new()
        {
            ButtonText = "Try to make Numbers",
            SymbolText = "",
            ClickEvent = "TryToNumberMenuItem_Click"
        },
        new()
        {
            ButtonText = "Try to make Letters",
            SymbolText = "",
            ClickEvent = "TryToAlphaMenuItem_Click"
        },
        new()
        {
            ButtonText = "Toggle Case",
            SymbolText = "",
            Command = "ToggleCaseCmd"
        },
        new()
        {
            ButtonText = "Remove Duplicate Lines",
            SymbolText = "",
            ClickEvent = "RemoveDuplicateLines_Click"
        },
        new()
        {
            ButtonText = "Replace Reserved Characters",
            SymbolText = "",
            Command = "ReplaceReservedCmd"
        },
        new()
        {
            ButtonText = "Unstack Text (Select Top Row)",
            SymbolText = "",
            Command = "UnstackCmd"
        },
        new()
        {
            ButtonText = "Unstack Text (Select First Column)",
            SymbolText = "",
            Command = "UnstackGroupCmd"
        },
        new()
        {
            ButtonText = "Add or Remove at...",
            SymbolText = "",
            ClickEvent = "AddRemoveAtMenuItem_Click"
        },
        new()
        {
            ButtonText = "Select Word",
            SymbolText = "",
            ClickEvent = "SelectWordMenuItem_Click"
        },
        new()
        {
            ButtonText = "Select Line",
            SymbolText = "",
            ClickEvent = "SelectLineMenuItem_Click"
        },
        new()
        {
            ButtonText = "Move Line Up",
            SymbolText = "",
            ClickEvent = "MoveLineUpMenuItem_Click"
        },
        new()
        {
            ButtonText = "Move Line Down",
            SymbolText = "",
            ClickEvent = "MoveLineDownMenuItem_Click"
        },
        new()
        {
            ButtonText = "Split on Selection",
            SymbolText = "",
            Command = "SplitOnSelectionCmd"
        },
        new()
        {
            ButtonText = "Isolate Selection",
            SymbolText = "",
            Command = "IsolateSelectionCmd"
        },
        new()
        {
            ButtonText = "Delete All of Selection",
            SymbolText = "",
            Command = "DeleteAllSelectionCmd"
        },
        new()
        {
            ButtonText = "Delete All of Pattern",
            SymbolText = "",
            Command = "DeleteAllSelectionPatternCmd"
        },
        new()
        {
            ButtonText = "Insert on Every Line",
            SymbolText = "",
            Command = "InsertSelectionOnEveryLineCmd"
        },
        new()
        {
            ButtonText = "New Quick Simple Lookup",
            SymbolText = "",
            ClickEvent = "LaunchQuickSimpleLookup"
        },
        new()
        {
            ButtonText = "List Files and Folders...",
            SymbolText = "",
            ClickEvent = "ListFilesMenuItem_Click"
        },
        new()
        {
            ButtonText = "Extract Text from Images...",
            SymbolText = "",
            ClickEvent = "ReadFolderOfImages_Click"
        },
        new()
        {
            ButtonText = "Extract Text from Images to txt Files...",
            SymbolText = "",
            ClickEvent = "ReadFolderOfImagesWriteTxtFiles_Click"
        },
        new()
        {
            ButtonText = "New Window",
            SymbolText = "",
            ClickEvent = "NewWindow_Clicked"
        },
        new()
        {
            ButtonText = "New Window from Selection",
            SymbolText = "",
            ClickEvent = "NewWindowWithText_Clicked"
        }
    };
}


