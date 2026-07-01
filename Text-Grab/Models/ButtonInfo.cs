using System.Collections.Generic;
using System.Text.Json.Serialization;
using Text_Grab.Controls;
using Wpf.Ui.Controls;

namespace Text_Grab.Models;

public enum DefaultCheckState
{
    Off = 0,
    LastUsed = 1,
    On = 2
}

public class ButtonInfo
{
    public double OrderNumber { get; set; } = 0.1;
    public string ButtonText { get; set; } = "";
    public string SymbolText { get; set; } = "";
    public string Background { get; set; } = "Transparent";
    public string Command { get; set; } = "";
    public string ClickEvent { get; set; } = "";
    public bool IsSymbol { get; set; } = false;

    [JsonIgnore]
    public SymbolRegular SymbolIcon { get; set; } = SymbolRegular.Diamond24;

    // Post-grab action properties
    public bool IsRelevantForFullscreenGrab { get; set; } = false;
    public bool IsRelevantForEditWindow { get; set; } = true; // Default to true for backward compatibility
    public DefaultCheckState DefaultCheckState { get; set; } = DefaultCheckState.Off;

    /// <summary>
    /// When this ButtonInfo represents a Grab Template action, this holds the template's
    /// unique ID so the executor can look it up. Empty for non-template actions.
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// When true, this button requires a Copilot+ PC (Windows AI capable device) to function.
    /// </summary>
    public bool RequiresCopilotPlus { get; set; } = false;

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
        return System.HashCode.Combine(
            ButtonText,
            SymbolText,
            Background,
            Command,
            ClickEvent,
            IsRelevantForFullscreenGrab,
            IsRelevantForEditWindow,
            DefaultCheckState);
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
            IsRelevantForFullscreenGrab = button.CustomButton.IsRelevantForFullscreenGrab;
            IsRelevantForEditWindow = button.CustomButton.IsRelevantForEditWindow;
            DefaultCheckState = button.CustomButton.DefaultCheckState;
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

    // Constructor for post-grab actions
    public ButtonInfo(string buttonText, string clickEvent, SymbolRegular symbolIcon, DefaultCheckState defaultCheckState)
    {
        ButtonText = buttonText;
        ClickEvent = clickEvent;
        SymbolIcon = symbolIcon;
        IsSymbol = true;
        IsRelevantForFullscreenGrab = true;
        IsRelevantForEditWindow = false;
        DefaultCheckState = defaultCheckState;
    }

    private static List<ButtonInfo>? _defaultButtonList;
    public static List<ButtonInfo> DefaultButtonList
    {
        get
        {
            if (_defaultButtonList is not null)
                return _defaultButtonList;

            _defaultButtonList =
            [
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
            SymbolText = "",
            ClickEvent = "EditBottomBarMenuItem_Click",
            IsSymbol = true,
                    SymbolIcon = SymbolRegular.CalendarSettings24
                },
                    ];

            return _defaultButtonList;
        }
    }

    private static List<ButtonInfo>? _allButtons;
    public static List<ButtonInfo> AllButtons
    {
        get
        {
            if (_allButtons is not null)
                return _allButtons;

            _allButtons =
            [
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
            OrderNumber = 1.21,
            ButtonText = "Save As...",
            SymbolText = "",
            ClickEvent = "SaveAsBTN_Click",
            SymbolIcon = SymbolRegular.DocumentEdit24
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
            OrderNumber = 1.31,
            ButtonText = "Join Lines...",
            SymbolText = "",
            ClickEvent = "JoinLinesMenuItem_Click",
            SymbolIcon = SymbolRegular.Merge24
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
            OrderNumber = 1.42,
            ButtonText = "Manage Grab Templates...",
            SymbolText = "",
            ClickEvent = "ManageGrabTemplates_Click",
            SymbolIcon = SymbolRegular.GridDots24
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
            OrderNumber = 1.61,
            ButtonText = "Regex Manager",
            SymbolText = "",
            ClickEvent = "RegexManagerMenuItem_Click",
            SymbolIcon = SymbolRegular.Book24
        },
        new()
        {
            OrderNumber = 1.7,
            ButtonText = "Web Search",
            SymbolText = "",
            Command = "DefaultWebSearchCmd",
            SymbolIcon = SymbolRegular.GlobeSearch24
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
            Command = "OcrPasteCommand",
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
            OrderNumber = 3.51,
            ButtonText = "Shuffle Lines",
            SymbolText = "",
            ClickEvent = "ShuffleLinesMenuItem_Click",
            SymbolIcon = SymbolRegular.ArrowShuffle24
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
            OrderNumber = 4.51,
            ButtonText = "Split Lines After Each Selection",
            SymbolText = "",
            Command = "SplitAfterSelectionCmd",
            SymbolIcon = SymbolRegular.TextWrapOff24
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
            ButtonText = "Write .txt File For Each Image",
            SymbolText = "",
            ClickEvent = "ToggleWriteTxtFileForEachImage_Click",
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
            OrderNumber = 6.1,
            ButtonText = "Close",
            ClickEvent = "CloseMenuItem_Click",
            SymbolIcon = SymbolRegular.WindowAdOff20
        },
        new()
        {
            OrderNumber = 6.2,
            ButtonText = "Correct Common GUID/UUID Errors",
            ClickEvent = "CorrectGuid_Click",
            SymbolIcon = SymbolRegular.TextWholeWord20
        },
        new()
        {
            OrderNumber = 6.3,
            ButtonText = "Transpose Table",
            Command = "TransposeTableCmd",
            SymbolIcon = SymbolRegular.TableSwitch24
        },
        new()
        {
            OrderNumber = 6.4,
            ButtonText = "Add Spreadsheet Row",
            ClickEvent = "AddSpreadsheetRowMenuItem_Click",
            SymbolIcon = SymbolRegular.TableInsertRow24
        },
        new()
        {
            OrderNumber = 6.5,
            ButtonText = "Add Spreadsheet Column",
            ClickEvent = "AddSpreadsheetColumnMenuItem_Click",
            SymbolIcon = SymbolRegular.TableInsertColumn24
        },
        new()
        {
            OrderNumber = 6.6,
            ButtonText = "Copy Selected Spreadsheet Cells",
            ClickEvent = "CopySpreadsheetSelectionMenuItem_Click",
            SymbolIcon = SymbolRegular.CopySelect20
        },
        new()
        {
            OrderNumber = 6.7,
            ButtonText = "Copy Selected Spreadsheet Rows",
            ClickEvent = "CopySpreadsheetRowsMenuItem_Click",
            SymbolIcon = SymbolRegular.TableCopy20
        },
        new()
        {
            OrderNumber = 6.8,
            ButtonText = "Copy Current Spreadsheet Column",
            ClickEvent = "CopySpreadsheetColumnMenuItem_Click",
            SymbolIcon = SymbolRegular.Column20
        },
        new()
        {
            OrderNumber = 6.9,
            ButtonText = "Move Spreadsheet Row Up",
            ClickEvent = "MoveSpreadsheetRowUpMenuItem_Click",
            SymbolIcon = SymbolRegular.TableInsertRow24
        },
        new()
        {
            OrderNumber = 6.91,
            ButtonText = "Move Spreadsheet Row Down",
            ClickEvent = "MoveSpreadsheetRowDownMenuItem_Click",
            SymbolIcon = SymbolRegular.TableInsertRow24
        },
        new()
        {
            OrderNumber = 6.92,
            ButtonText = "Delete Spreadsheet Row",
            ClickEvent = "DeleteSpreadsheetRowMenuItem_Click",
            SymbolIcon = SymbolRegular.TableDeleteRow24
        },
        new()
        {
            OrderNumber = 6.93,
            ButtonText = "Move Spreadsheet Column Left",
            ClickEvent = "MoveSpreadsheetColumnLeftMenuItem_Click",
            SymbolIcon = SymbolRegular.TableMoveLeft24
        },
        new()
        {
            OrderNumber = 6.94,
            ButtonText = "Move Spreadsheet Column Right",
            ClickEvent = "MoveSpreadsheetColumnRightMenuItem_Click",
            SymbolIcon = SymbolRegular.TableMoveRight24
        },
        new()
        {
            OrderNumber = 6.95,
            ButtonText = "Delete Spreadsheet Column",
            ClickEvent = "DeleteSpreadsheetColumnMenuItem_Click",
            SymbolIcon = SymbolRegular.TableDeleteColumn24
        },
        new()
        {
            OrderNumber = 6.96,
            ButtonText = "Enter Raw Text Mode",
            ClickEvent = "EnterRawTextMode_Click",
            SymbolIcon = SymbolRegular.TextT24
        },
        new()
        {
            OrderNumber = 6.97,
            ButtonText = "Enter Spreadsheet Mode",
            ClickEvent = "EnterSpreadsheetMode_Click",
            SymbolIcon = SymbolRegular.Table24
        },
        new()
        {
            OrderNumber = 6.98,
            ButtonText = "Enter Markdown Mode",
            ClickEvent = "EnterMarkdownMode_Click",
            SymbolIcon = SymbolRegular.Markdown20
        },
        new()
        {
            OrderNumber = 7.1,
            ButtonText = "Toggle Show Calc Errors",
            ClickEvent = "ToggleShowMathErrors_Click",
            SymbolIcon = SymbolRegular.MathSymbols24
        },
        new()
        {
            OrderNumber = 7.11,
            ButtonText = "Toggle Calculation Pane",
            ClickEvent = "CalcToggleButton_Click",
            SymbolIcon = SymbolRegular.Calculator24
        },
        new()
        {
            OrderNumber = 7.12,
            ButtonText = "Copy All Calculation Results",
            ClickEvent = "CalcCopyAllButton_Click",
            SymbolIcon = SymbolRegular.CopyAdd24
        },
        new()
        {
            OrderNumber = 7.2,
            ButtonText = "Toggle Always On Top",
            ClickEvent = "ToggleAlwaysOnTop_Click",
            SymbolIcon = SymbolRegular.WindowLocationTarget20
        },
        new()
        {
            OrderNumber = 7.21,
            ButtonText = "Toggle Hide Bottom Bar",
            ClickEvent = "ToggleHideBottomBar_Click",
            SymbolIcon = SymbolRegular.PanelBottomContract20
        },
        new()
        {
            OrderNumber = 7.24,
            ButtonText = "Restore This Window Position",
            ClickEvent = "RestoreThisPosition_Click",
            SymbolIcon = SymbolRegular.WindowWrench24
        },
        new()
        {
            OrderNumber = 7.25,
            ButtonText = "Toggle Margins",
            ClickEvent = "ToggleMargins_Click",
            SymbolIcon = SymbolRegular.DocumentMargins24
        },
        new()
        {
            OrderNumber = 7.26,
            ButtonText = "Toggle Wrap Text",
            ClickEvent = "ToggleWrapText_Click",
            SymbolIcon = SymbolRegular.TextWrap24
        },
        new()
        {
            OrderNumber = 7.27,
            ButtonText = "Font...",
            ClickEvent = "FontMenuItem_Click",
            SymbolIcon = SymbolRegular.TextFont24
        },
        new()
        {
            OrderNumber = 7.3,
            ButtonText = "Grab Previous Region",
            ClickEvent = "PreviousRegion_Click",
            SymbolIcon = SymbolRegular.WindowArrowUp24
        },
        new()
        {
            OrderNumber = 7.31,
            ButtonText = "Edit Last Grab",
            ClickEvent = "OpenLastAsGrabFrameMenuItem_Click",
            SymbolIcon = SymbolRegular.ImageEdit24
        },
        new()
        {
            OrderNumber = 7.4,
            ButtonText = "Select All",
            ClickEvent = "SelectAllMenuItem_Click",
            SymbolIcon = SymbolRegular.SelectAllOn24
        },
        new()
        {
            OrderNumber = 7.41,
            ButtonText = "Select None",
            ClickEvent = "SelectNoneMenuItem_Click",
            SymbolIcon = SymbolRegular.TextClearFormatting24
        },
        new()
        {
            OrderNumber = 7.42,
            ButtonText = "Delete Selected Text",
            ClickEvent = "DeleteSelectedTextMenuItem_Click",
            SymbolIcon = SymbolRegular.Delete24
        },
        new()
        {
            OrderNumber = 7.43,
            ButtonText = "Show Character Details",
            ClickEvent = "CharDetailsButton_Click",
            SymbolIcon = SymbolRegular.TextFontInfo24
        },
        new()
        {
            OrderNumber = 7.44,
            ButtonText = "Find Similar Matches",
            ClickEvent = "SimilarMatchesButton_Click",
            SymbolIcon = SymbolRegular.DocumentSearch24
        },
        new()
        {
            OrderNumber = 7.45,
            ButtonText = "Open Regex Pattern Search",
            ClickEvent = "RegexPatternButton_Click",
            SymbolIcon = SymbolRegular.TextEffects24
        },
        new()
        {
            OrderNumber = 7.46,
            ButtonText = "Save Regex Pattern",
            ClickEvent = "SavePatternMenuItem_Click",
            SymbolIcon = SymbolRegular.SaveCopy24
        },
        new()
        {
            OrderNumber = 8.1,
            ButtonText = "Summarize Paragraph",
            ClickEvent = "SummarizeMenuItem_Click",
            SymbolIcon = SymbolRegular.BotSparkle24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.2,
            ButtonText = "Rewrite with Local AI",
            ClickEvent = "RewriteMenuItem_Click",
            SymbolIcon = SymbolRegular.BotSparkle24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.3,
            ButtonText = "Convert to Table",
            ClickEvent = "ConvertTableMenuItem_Click",
            SymbolIcon = SymbolRegular.BotSparkle24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.4,
            ButtonText = "Translate to System Language",
            ClickEvent = "TranslateToSystemLanguageMenuItem_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.41,
            ButtonText = "Translate to English",
            ClickEvent = "TranslateToEnglish_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.42,
            ButtonText = "Translate to Spanish",
            ClickEvent = "TranslateToSpanish_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.43,
            ButtonText = "Translate to French",
            ClickEvent = "TranslateToFrench_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.44,
            ButtonText = "Translate to German",
            ClickEvent = "TranslateToGerman_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.45,
            ButtonText = "Translate to Italian",
            ClickEvent = "TranslateToItalian_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.46,
            ButtonText = "Translate to Portuguese",
            ClickEvent = "TranslateToPortuguese_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.47,
            ButtonText = "Translate to Russian",
            ClickEvent = "TranslateToRussian_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.48,
            ButtonText = "Translate to Japanese",
            ClickEvent = "TranslateToJapanese_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.49,
            ButtonText = "Translate to Chinese (Simplified)",
            ClickEvent = "TranslateToChineseSimplified_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.5,
            ButtonText = "Translate to Korean",
            ClickEvent = "TranslateToKorean_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.51,
            ButtonText = "Translate to Arabic",
            ClickEvent = "TranslateToArabic_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.52,
            ButtonText = "Translate to Hindi",
            ClickEvent = "TranslateToHindi_Click",
            SymbolIcon = SymbolRegular.Translate24,
            RequiresCopilotPlus = true
        },
        new()
        {
            OrderNumber = 8.6,
            ButtonText = "Extract RegEx",
            ClickEvent = "ExtractRegexMenuItem_Click",
            SymbolIcon = SymbolRegular.TextWholeWord20,
            RequiresCopilotPlus = true
        },
        new()
        {
            ButtonText = "Edit Bottom Bar",
            ClickEvent = "EditBottomBarMenuItem_Click",
            SymbolIcon = SymbolRegular.PanelBottom20
        },
        new()
        {
            ButtonText = "Settings",
            ClickEvent = "SettingsMenuItem_Click",
            SymbolIcon = SymbolRegular.Settings24
        },
       ];

            return _allButtons;
        }
    }
}
