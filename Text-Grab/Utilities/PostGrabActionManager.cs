using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Text_Grab.Models;
using Text_Grab.Properties;
using Wpf.Ui.Controls;

namespace Text_Grab.Utilities;

public class PostGrabActionManager
{
    private static readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    /// <summary>
    /// Gets all available post-grab actions from ButtonInfo.AllButtons filtered for FullscreenGrab relevance
    /// </summary>
    public static List<ButtonInfo> GetAvailablePostGrabActions()
    {
        List<ButtonInfo> allPostGrabActions = [.. GetDefaultPostGrabActions()];

        // Add other relevant actions from AllButtons that are marked as relevant for FullscreenGrab
        IEnumerable<ButtonInfo> relevantActions = ButtonInfo.AllButtons
            .Where(button => button.IsRelevantForFullscreenGrab && !allPostGrabActions.Any(b => b.ButtonText == button.ButtonText));
        
        allPostGrabActions.AddRange(relevantActions);

        return [.. allPostGrabActions.OrderBy(b => b.OrderNumber)];
    }

    /// <summary>
    /// Gets the default post-grab actions (the current 6 hardcoded actions)
    /// </summary>
    public static List<ButtonInfo> GetDefaultPostGrabActions()
    {
        return
        [
            new ButtonInfo(
                buttonText: "Fix GUIDs",
                clickEvent: "CorrectGuid_Click",
                symbolIcon: SymbolRegular.Braces24,
                defaultCheckState: DefaultCheckState.Off
            )
            {
                OrderNumber = 6.1
            },
            new ButtonInfo(
                buttonText: "Trim each line",
                clickEvent: "TrimEachLine_Click",
                symbolIcon: SymbolRegular.TextCollapse24,
                defaultCheckState: DefaultCheckState.Off
            )
            {
                OrderNumber = 6.2
            },
            new ButtonInfo(
                buttonText: "Remove duplicate lines",
                clickEvent: "RemoveDuplicateLines_Click",
                symbolIcon: SymbolRegular.MultiselectLtr24,
                defaultCheckState: DefaultCheckState.Off
            )
            {
                OrderNumber = 6.3
            },
            new ButtonInfo(
                buttonText: "Web Search",
                clickEvent: "WebSearch_Click",
                symbolIcon: SymbolRegular.GlobeSearch24,
                defaultCheckState: DefaultCheckState.Off
            )
            {
                OrderNumber = 6.4
            },
            new ButtonInfo(
                buttonText: "Try to insert text",
                clickEvent: "Insert_Click",
                symbolIcon: SymbolRegular.ClipboardTaskAdd24,
                defaultCheckState: DefaultCheckState.Off
            )
            {
                OrderNumber = 6.5
            },
            new ButtonInfo(
                buttonText: "Translate to system language",
                clickEvent: "Translate_Click",
                symbolIcon: SymbolRegular.LocalLanguage24,
                defaultCheckState: DefaultCheckState.Off
            )
            {
                OrderNumber = 6.6
            }
        ];
    }

    /// <summary>
    /// Gets the enabled post-grab actions from settings
    /// </summary>
    public static List<ButtonInfo> GetEnabledPostGrabActions()
    {
        string json = DefaultSettings.PostGrabJSON;

        if (string.IsNullOrWhiteSpace(json))
            return GetDefaultPostGrabActions();

        try
        {
            List<ButtonInfo>? customActions = JsonSerializer.Deserialize<List<ButtonInfo>>(json);
            if (customActions is not null && customActions.Count > 0)
                return customActions;
        }
        catch (JsonException)
        {
            // If deserialization fails, return defaults
        }

        return GetDefaultPostGrabActions();
    }

    /// <summary>
    /// Saves the list of post-grab actions to settings
    /// </summary>
    public static void SavePostGrabActions(List<ButtonInfo> actions)
    {
        string json = JsonSerializer.Serialize(actions);
        DefaultSettings.PostGrabJSON = json;
        DefaultSettings.Save();
    }

    /// <summary>
    /// Gets the check state for a specific action (On/LastUsed/Off)
    /// </summary>
    public static bool GetCheckState(ButtonInfo action)
    {
        // First check if there's a stored check state from last usage
        string statesJson = DefaultSettings.PostGrabCheckStates;

        if (!string.IsNullOrWhiteSpace(statesJson))
        {
            try
            {
                Dictionary<string, bool>? checkStates = JsonSerializer.Deserialize<Dictionary<string, bool>>(statesJson);
                if (checkStates is not null 
                    && checkStates.TryGetValue(action.ButtonText, out bool storedState)
                    && action.DefaultCheckState == DefaultCheckState.LastUsed)
                {
                    // If the action is set to LastUsed, use the stored state
                    return storedState;
                }
            }
            catch (JsonException)
            {
                // If deserialization fails, fall through to default behavior
            }
        }

        // Otherwise use the default check state
        return action.DefaultCheckState == DefaultCheckState.On;
    }

    /// <summary>
    /// Saves the check state for an action (used for LastUsed tracking)
    /// </summary>
    public static void SaveCheckState(ButtonInfo action, bool isChecked)
    {
        string statesJson = DefaultSettings.PostGrabCheckStates;
        Dictionary<string, bool> checkStates = [];

        if (!string.IsNullOrWhiteSpace(statesJson))
        {
            try
            {
                checkStates = JsonSerializer.Deserialize<Dictionary<string, bool>>(statesJson) ?? [];
            }
            catch (JsonException)
            {
                // Start fresh if deserialization fails
            }
        }

        checkStates[action.ButtonText] = isChecked;
        string updatedJson = JsonSerializer.Serialize(checkStates);
        DefaultSettings.PostGrabCheckStates = updatedJson;
        DefaultSettings.Save();
    }

    /// <summary>
    /// Executes a post-grab action on the given text
    /// </summary>
    public static async Task<string> ExecutePostGrabAction(ButtonInfo action, string text)
    {
        string result = text;

        switch (action.ClickEvent)
        {
            case "CorrectGuid_Click":
                result = text.CorrectCommonGuidErrors();
                break;

            case "TrimEachLine_Click":
                string[] stringSplit = text.Split(Environment.NewLine);
                string[] trimmedLines = stringSplit
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .ToArray();

                result = trimmedLines.Length == 0
                    ? string.Empty
                    : string.Join(Environment.NewLine, trimmedLines) + Environment.NewLine;
                break;

            case "RemoveDuplicateLines_Click":
                result = text.RemoveDuplicateLines();
                break;

            case "WebSearch_Click":
                string searchStringUrlSafe = WebUtility.UrlEncode(text);
                WebSearchUrlModel searcher = Singleton<WebSearchUrlModel>.Instance.DefaultSearcher;
                Uri searchUri = new($"{searcher.Url}{searchStringUrlSafe}");
                _ = await Windows.System.Launcher.LaunchUriAsync(searchUri);
                // Don't modify the text for web search
                break;

            case "Insert_Click":
                // This will be handled separately in FullscreenGrab after closing
                // Don't modify the text
                break;

            case "Translate_Click":
                if (WindowsAiUtilities.CanDeviceUseWinAI())
                {
                    string systemLanguage = LanguageUtilities.GetSystemLanguageForTranslation();
                    result = await WindowsAiUtilities.TranslateText(text, systemLanguage);
                }
                break;

            default:
                // Unknown action - return text unchanged
                break;
        }

        return result;
    }
}
