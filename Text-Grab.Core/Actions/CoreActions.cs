using System;
using Text_Grab.Models;
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

        registry.Register(new DelegateTextTransform<CleanUpTextConfig>(
            Transform("textgrab.text.cleanUp", "Clean Up Text", "TextClearFormatting24", ActionCategory.Cleanup,
                typeof(CleanUpTextConfig)),
            static (text, config) => text.CleanUpText(config.CorrectToLatin),
            new CleanUpTextConfig()));

        // The window's Unstack commands count the selected lines to get Columns or Rows. These
        // take the count as config; the selection-driven commands are E2's.
        registry.Register(new DelegateTextTransform<UnstackConfig>(
            Transform("textgrab.text.unstack", "Unstack Text (Select Top Row)", "TableStackAbove24", ActionCategory.Table,
                typeof(UnstackConfig)),
            static (text, config) => text.UnstackStrings(
                AtLeastOne(config.Columns, "textgrab.text.unstack", nameof(UnstackConfig.Columns)))));

        registry.Register(new DelegateTextTransform<UnstackGroupsConfig>(
            Transform("textgrab.text.unstackGroups", "Unstack Text (Select First Column)", "TableStackLeft24", ActionCategory.Table,
                typeof(UnstackGroupsConfig)),
            static (text, config) => text.UnstackGroups(
                AtLeastOne(config.Rows, "textgrab.text.unstackGroups", nameof(UnstackGroupsConfig.Rows)))));

        registry.Register(new DelegateTextTransform<ApplyPatternConfig>(
            Transform("textgrab.text.applyPattern", "Apply Pattern", "TextEffects24", ActionCategory.Extract,
                typeof(ApplyPatternConfig)),
            ApplyPattern));

        registry.Register(new DelegateTextTransform<ApplyRecognizerConfig>(
            Transform("textgrab.text.applyRecognizer", "Apply Smart Pattern", "DocumentSearch24", ActionCategory.Extract,
                typeof(ApplyRecognizerConfig)),
            ApplyRecognizer));
    }

    /// <summary>Runs a regex the way the window's Patterns menu runs a saved pattern.</summary>
    private static string ApplyPattern(string text, ApplyPatternConfig config)
    {
        if (string.IsNullOrEmpty(config.Pattern))
            throw new ArgumentException("'textgrab.text.applyPattern' needs a pattern.");

        PatternItem pattern = new(new StoredRegex { Pattern = config.Pattern });
        return PatternExecutor.Apply(pattern, text, config.Mode, config.Separator);
    }

    /// <summary>Runs a built-in recognizer the way the window's Smart Patterns menu does.</summary>
    private static string ApplyRecognizer(string text, ApplyRecognizerConfig config)
    {
        BuiltInRecognizer recognizer = BuiltInRecognizer.GetById(config.RecognizerId)
            ?? throw new ArgumentException($"'textgrab.text.applyRecognizer' has no recognizer with the ID '{config.RecognizerId}'.");

        return RecognizerExecutor.ApplyRecognizer(recognizer, text, config.Mode, config.Separator, config.Output);
    }

    private static int AtLeastOne(int value, string actionId, string name)
        => value >= 1 ? value : throw new ArgumentException($"'{actionId}' needs {name} of at least 1, not {value}.");

    private static ActionDescriptor Transform(
        string id,
        string displayName,
        string iconKey,
        ActionCategory category,
        Type? configType = null,
        string? defaultGesture = null)
        => new(id, displayName, iconKey, category, ActionKind.TextTransform, configType, defaultGesture);
}
