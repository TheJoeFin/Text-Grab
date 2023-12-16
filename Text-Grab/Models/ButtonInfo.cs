using System.Collections.Generic;
using Text_Grab.Controls;
using Wpf.Ui.Controls;

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

    public SymbolRegular SymbolIcon { get; set; } = SymbolRegular.Diamond24;

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

    // a constructor which takes a collapsible button
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
            ClickEvent = "CopyCloseBTN_Click",
            SymbolIcon = SymbolRegular.Copy24
        },
        new()
        {
            ButtonText = "Save to File...",
            SymbolText = "",
            ClickEvent = "SaveBTN_Click",
            SymbolIcon = SymbolRegular.Save24
        },
        new()
        {
            ButtonText = "Make Single Line",
            SymbolText = "",
            Command = "SingleLineCmd",
            SymbolIcon = SymbolRegular.SubtractSquare24
        },
        new()
        {
            ButtonText = "New Fullscreen Grab",
            SymbolText = "",
            ClickEvent = "NewFullscreen_Click",
            IsSymbol = true,
            SymbolIcon = SymbolRegular.SlideAdd24
        },
        new()
        {
            ButtonText = "Open Grab Frame",
            SymbolText = "",
            ClickEvent = "OpenGrabFrame_Click",
            IsSymbol = true,
            SymbolIcon = SymbolRegular.PanelBottom20
        },
        new()
        {
            ButtonText = "Find and Replace",
            SymbolText = "",
            ClickEvent = "SearchButton_Click",
            IsSymbol = true,
            SymbolIcon = SymbolRegular.Search24
        },
        new()
        {
            ButtonText = "Edit Bottom Bar",
            SymbolText = "",
            ClickEvent = "EditBottomBarMenuItem_Click",
            IsSymbol = true,
            SymbolIcon = SymbolRegular.CalendarEdit24
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
            ClickEvent = "CopyCloseBTN_Click",
            SymbolIcon = SymbolRegular.Copy24
        },
        new()
        {
            OrderNumber = 1.11,
            ButtonText = "Close and Insert",
            SymbolText = "",
            Background = "#CC7000",
            ClickEvent = "CopyClosePasteBTN_Click",
            SymbolIcon = SymbolRegular.ClipboardTaskAdd24
        },
        new()
        {
            OrderNumber = 1.2,
            ButtonText = "Save to File...",
            SymbolText = "",
            ClickEvent = "SaveBTN_Click",
            SymbolIcon = SymbolRegular.DocumentSave24
        },
        new()
        {
            OrderNumber = 1.3,
            ButtonText = "Make Single Line",
            SymbolText = "",
            Command = "SingleLineCmd",
            SymbolIcon = SymbolRegular.SubtractSquare24
        },
        new()
        {
            OrderNumber = 1.4,
            ButtonText = "New Fullscreen Grab",
            SymbolText = "",
            ClickEvent = "NewFullscreen_Click",
            SymbolIcon = SymbolRegular.SlideAdd24
        },
        new()
        {
            OrderNumber = 1.41,
            ButtonText = "Fullscreen Grab With Delay",
            SymbolText = "",
            ClickEvent = "FSGDelayMenuItem_Click",
            SymbolIcon = SymbolRegular.Timer324
        },
        new()
        {
            OrderNumber = 1.5,
            ButtonText = "Open Grab Frame",
            SymbolText = "",
            ClickEvent = "OpenGrabFrame_Click",
            SymbolIcon = SymbolRegular.PanelBottom20
        },
        new()
        {
            OrderNumber = 1.6,
            ButtonText = "Find and Replace",
            SymbolText = "",
            ClickEvent = "SearchButton_Click",
            SymbolIcon = SymbolRegular.Search24
        },
        new()
        {
            OrderNumber = 2.1,
            ButtonText = "Open Settings",
            SymbolText = "",
            ClickEvent = "SettingsMenuItem_Click",
            SymbolIcon = SymbolRegular.Settings24
        },
        new()
        {
            OrderNumber = 2.2,
            ButtonText = "Open File...",
            SymbolText = "",
            ClickEvent = "OpenFileMenuItem_Click",
            SymbolIcon = SymbolRegular.DocumentArrowRight24
        },
        new()
        {
            OrderNumber = 2.3,
            ButtonText = "OCR Paste",
            SymbolText = "",
            Command = "PasteCommand",
            SymbolIcon = SymbolRegular.ClipboardImage24
        },
        new()
        {
            OrderNumber = 2.4,
            ButtonText = "Launch URL",
            SymbolText = "",
            Command = "LaunchCmd",
            SymbolIcon = SymbolRegular.Globe24
        },
        new()
        {
            OrderNumber = 3.1,
            ButtonText = "Trim Each Line",
            SymbolText = "",
            ClickEvent = "TrimEachLineMenuItem_Click",
            SymbolIcon = SymbolRegular.TextCollapse24
        },
        new()
        {
            OrderNumber = 3.2,
            ButtonText = "Try to make Numbers",
            SymbolText = "",
            ClickEvent = "TryToNumberMenuItem_Click",
            SymbolIcon = SymbolRegular.NumberRow24
        },
        new()
        {
            OrderNumber = 3.3,
            ButtonText = "Try to make Letters",
            SymbolText = "",
            ClickEvent = "TryToAlphaMenuItem_Click",
            SymbolIcon = SymbolRegular.TextT24
        },
        new()
        {
            OrderNumber = 3.4,
            ButtonText = "Toggle Case",
            SymbolText = "",
            Command = "ToggleCaseCmd",
            SymbolIcon = SymbolRegular.TextChangeCase24
        },
        new()
        {
            OrderNumber = 3.5,
            ButtonText = "Remove Duplicate Lines",
            SymbolText = "",
            ClickEvent = "RemoveDuplicateLines_Click",
            SymbolIcon = SymbolRegular.MultiselectLtr24
        },
        new()
        {
            OrderNumber = 3.6,
            ButtonText = "Replace Reserved Characters",
            SymbolText = "",
            Command = "ReplaceReservedCmd",
            SymbolIcon = SymbolRegular.RoadCone24
        },
        new()
        {
            OrderNumber = 3.7,
            ButtonText = "Unstack Text (Select Top Row)",
            SymbolText = "",
            Command = "UnstackCmd",
            SymbolIcon = SymbolRegular.TableStackAbove24
        },
        new()
        {
            OrderNumber = 3.8,
            ButtonText = "Unstack Text (Select First Column)",
            SymbolText = "",
            Command = "UnstackGroupCmd",
            SymbolIcon = SymbolRegular.TableStackLeft24
        },
        new()
        {
            OrderNumber = 3.9,
            ButtonText = "Add or Remove at...",
            SymbolText = "",
            ClickEvent = "AddRemoveAtMenuItem_Click",
            SymbolIcon = SymbolRegular.ArrowSwap24
        },
        new()
        {
            OrderNumber = 4.1,
            ButtonText = "Select Word",
            SymbolText = "",
            ClickEvent = "SelectWordMenuItem_Click",
            SymbolIcon = SymbolRegular.Highlight24
        },
        new()
        {
            OrderNumber = 4.2,
            ButtonText = "Select Line",
            SymbolText = "",
            ClickEvent = "SelectLineMenuItem_Click",
            SymbolIcon = SymbolRegular.ArrowFit20
        },
        new()
        {
            OrderNumber = 4.3,
            ButtonText = "Move Line Up",
            SymbolText = "",
            ClickEvent = "MoveLineUpMenuItem_Click",
            SymbolIcon = SymbolRegular.ArrowUpload24
        },
        new()
        {
            OrderNumber = 4.4,
            ButtonText = "Move Line Down",
            SymbolText = "",
            ClickEvent = "MoveLineDownMenuItem_Click",
            SymbolIcon = SymbolRegular.ArrowDownload24
        },
        new()
        {
            OrderNumber = 4.5,
            ButtonText = "Split on Selection",
            SymbolText = "",
            Command = "SplitOnSelectionCmd",
            SymbolIcon = SymbolRegular.TextWrap24
        },
        new()
        {
            OrderNumber = 4.6,
            ButtonText = "Isolate Selection",
            SymbolText = "",
            Command = "IsolateSelectionCmd",
            SymbolIcon = SymbolRegular.ShapeExclude24
        },
        new()
        {
            OrderNumber = 4.7,
            ButtonText = "Delete All of Selection",
            SymbolText = "",
            Command = "DeleteAllSelectionCmd",
            SymbolIcon = SymbolRegular.Delete24
        },
        new()
        {
            OrderNumber = 4.8,
            ButtonText = "Delete All of Pattern",
            SymbolText = "",
            Command = "DeleteAllSelectionPatternCmd",
            SymbolIcon = SymbolRegular.DeleteLines20
        },
        new()
        {
            OrderNumber = 4.9,
            ButtonText = "Insert on Every Line",
            SymbolText = "",
            Command = "InsertSelectionOnEveryLineCmd",
            SymbolIcon = SymbolRegular.TextIndentIncreaseLtr24
        },
        new()
        {
            OrderNumber = 5.1,
            ButtonText = "New Quick Simple Lookup",
            SymbolText = "",
            ClickEvent = "LaunchQuickSimpleLookup",
            SymbolIcon = SymbolRegular.SlideSearch24
        },
        new()
        {
            OrderNumber = 5.2,
            ButtonText = "List Files and Folders...",
            SymbolText = "",
            ClickEvent = "ListFilesMenuItem_Click",
            SymbolIcon = SymbolRegular.DocumentBulletListMultiple24
        },
        new()
        {
            OrderNumber = 5.3,
            ButtonText = "Extract Text from Images...",
            SymbolText = "",
            ClickEvent = "ReadFolderOfImages_Click",
            SymbolIcon = SymbolRegular.ImageMultiple24
        },
        new()
        {
            OrderNumber = 5.4,
            ButtonText = "Extract Text from Images to txt Files...",
            SymbolText = "",
            ClickEvent = "ReadFolderOfImagesWriteTxtFiles_Click",
            SymbolIcon = SymbolRegular.TabDesktopImage24
        },
        new()
        {
            OrderNumber = 5.5,
            ButtonText = "New Window",
            SymbolText = "",
            ClickEvent = "NewWindow_Clicked",
            SymbolIcon = SymbolRegular.WindowNew24
        },
        new()
        {
            OrderNumber = 5.6,
            ButtonText = "New Window from Selection",
            SymbolText = "",
            ClickEvent = "NewWindowWithText_Clicked",
            SymbolIcon = SymbolRegular.WindowLocationTarget20
        },
        new()
        {
            OrderNumber = 5.7,
            ButtonText = "Make QR Code",
            SymbolText = "",
            Command = "MakeQrCodeCmd",
            SymbolIcon = SymbolRegular.QrCode24
        },
        new()
        {
            ButtonText = "Edit Bottom Bar",
            ClickEvent = "EditBottomBarMenuItem_Click",
            SymbolIcon = SymbolRegular.CalendarEdit24
        },
        new()
        {
            ButtonText = "Settings",
            ClickEvent = "SettingsMenuItem_Click",
            SymbolIcon = SymbolRegular.Settings24
        },
    };
}


