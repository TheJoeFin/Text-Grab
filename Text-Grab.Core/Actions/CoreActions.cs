using System;
using Text_Grab.Utilities;

namespace Text_Grab.Core.Actions;

/// <summary>
/// Registers the actions Text-Grab.Core implements: the pure text transforms. Core.Windows adds the
/// Windows-backed ones through its own <c>WindowsActions.RegisterAll</c>.
/// </summary>
/// <remarks>
/// Display names are the Edit Text Window's menu text, and icon keys are the <c>SymbolRegular</c>
/// names its bottom-bar buttons use. The inventory in <c>docs/v5/A1-Action-Registry.md</c> lists
/// where each one comes from.
/// </remarks>
public static class CoreActions
{
    /// <summary>Registers every Core action. Call once per registry; a second call throws on the first duplicate ID.</summary>
    public static void RegisterAll(IActionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(new DelegateTextTransform(
            Transform("textgrab.text.trimLines", "Trim Each Line", "TextCollapse24", ActionCategory.Lines),
            StringMethods.TrimEachLine));

        registry.Register(new DelegateTextTransform(
            Transform("textgrab.text.makeSingleLine", "Make Single Line", "SubtractSquare24", ActionCategory.Lines),
            StringMethods.MakeStringSingleLine));

        registry.Register(new DelegateTextTransform(
            Transform("textgrab.text.removeDuplicateLines", "Remove Duplicate Lines", "MultiselectLtr24", ActionCategory.Lines),
            StringMethods.RemoveDuplicateLines));

        registry.Register(new DelegateTextTransform(
            Transform("textgrab.text.shuffleLines", "Shuffle Lines", "ArrowShuffle24", ActionCategory.Lines),
            static text => text.ShuffleLines()));

        registry.Register(new DelegateTextTransform(
            Transform("textgrab.text.tryMakeNumbers", "Try To Make Numbers", "NumberRow24", ActionCategory.Cleanup),
            StringMethods.TryFixToNumbers));

        registry.Register(new DelegateTextTransform(
            Transform("textgrab.text.tryMakeLetters", "Try To Make Letters", "TextT24", ActionCategory.Cleanup),
            StringMethods.TryFixToLetters));

        registry.Register(new DelegateTextTransform(
            Transform("textgrab.text.correctGuidErrors", "Correct Common GUID/UUID Errors", "TextWholeWord20", ActionCategory.Cleanup),
            StringMethods.CorrectCommonGuidErrors));

        registry.Register(new DelegateTextTransform(
            Transform("textgrab.text.replaceReservedCharacters", "Replace Reserved Characters", "RoadCone24", ActionCategory.Cleanup,
                defaultGesture: "Ctrl+R"),
            StringMethods.ReplaceReservedCharacters));
    }

    private static ActionDescriptor Transform(
        string id,
        string displayName,
        string iconKey,
        ActionCategory category,
        Type? configType = null,
        string? defaultGesture = null)
        => new(id, displayName, iconKey, category, ActionKind.TextTransform, configType, defaultGesture);
}
