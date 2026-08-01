using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Tests;

public class UiAutomationContractTests
{
    // Runtime pattern checks belong in the future WinApp fixture harness. This
    // source-level contract deliberately keeps selector regressions detectable
    // in the existing test suite without starting the application.
    [Fact]
    public void RequiredAutomationIds_ArePresentAndUniqueInXaml()
    {
        string repositoryRoot = FindRepositoryRoot();
        IReadOnlyDictionary<string, string[]> requiredIds = new Dictionary<string, string[]>
        {
            ["Views\\FirstRunWindow.xaml"] = ["FirstRunWindow", "FirstRun.StartButton", "FirstRun.DefaultFullscreenRadio", "FirstRun.BackgroundToggle"],
            ["Views\\SettingsWindow.xaml"] = ["SettingsWindow", "Settings.Navigation", "Settings.Nav.General", "Settings.Nav.Danger"],
            ["Views\\EditTextWindow.xaml"] = ["EditTextWindow", "EditText.Editor", "EditText.StatusText", "EditText.LoadingStatus", "EditText.Menu.ClipboardWatcher"],
            ["Views\\QuickSimpleLookup.xaml"] = ["QuickLookupWindow", "QuickLookup.Search", "QuickLookup.ResultsGrid", "QuickLookup.CopySelectedButton", "QuickLookup.ErrorStatus"],
            ["Views\\FullscreenGrab.xaml"] = ["FullscreenGrabWindow", "FullscreenGrab.SelectionCanvas", "FullscreenGrab.Language", "FullscreenGrab.AcceptSelectionButton"],
            ["Views\\GrabFrame.xaml"] = ["GrabFrameWindow", "GrabFrame.ZoomSurface", "GrabFrame.WordBordersCanvas", "GrabFrame.GrabButton", "GrabFrame.Status"],
            ["Controls\\NotifyIconWindow.xaml"] = ["NotifyIconWindow", "NotifyIcon", "NotifyIcon.Menu.Settings", "NotifyIcon.Menu.Close"],
            ["Controls\\FindAndReplaceWindow.xaml"] = ["FindReplaceDialog", "FindReplace.Search", "FindReplace.Results"],
            ["Controls\\RegexEditorDialog.xaml"] = ["RegexEditorDialog", "RegexEditor.Pattern", "RegexEditor.Error"],
            ["Controls\\PatternMatchModeDialog.xaml"] = ["PatternMatchDialog", "PatternMatch.Indices", "PatternMatch.IndicesError"],
            ["Pages\\KeysSettings.xaml"] = ["Settings.ShortcutsPage", "Settings.Shortcuts.GlobalHotkeysToggle", "Settings.Shortcuts.FullscreenGrab"],
        };

        Dictionary<string, List<string>> occurrences = [];
        foreach (string xamlPath in Directory.EnumerateFiles(Path.Combine(repositoryRoot, "Text-Grab"), "*.xaml", SearchOption.AllDirectories))
        {
            XDocument document = XDocument.Load(xamlPath, LoadOptions.SetLineInfo);
            foreach (XAttribute attribute in document.Descendants().Attributes().Where(attribute =>
                attribute.Name.LocalName == "AutomationId"
                || attribute.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal)))
            {
                if (!occurrences.TryGetValue(attribute.Value, out List<string>? locations))
                {
                    locations = [];
                    occurrences.Add(attribute.Value, locations);
                }

                locations.Add(xamlPath);
            }
        }

        foreach ((string relativePath, string[] ids) in requiredIds)
        {
            foreach (string id in ids)
            {
                Assert.True(occurrences.TryGetValue(id, out List<string>? locations),
                    $"Required AutomationId '{id}' is missing from {relativePath}.");
                Assert.Single(locations!);
                Assert.EndsWith(relativePath, locations![0], StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach ((string id, List<string> locations) in occurrences)
            Assert.True(locations.Count == 1, $"AutomationId '{id}' must be unique; found in {string.Join(", ", locations)}.");
    }

    [Fact]
    public void WordBorders_ExposeValuePatternThroughDedicatedAutomationPeer()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Text-Grab", "Controls", "WordBorder.xaml.cs"));

        Assert.Contains("OnCreateAutomationPeer", source, StringComparison.Ordinal);
        Assert.Contains("IValueProvider", source, StringComparison.Ordinal);
        Assert.Contains("PatternInterface.Value", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeAutomationSelectors_AreDerivedFromStableOwners()
    {
        string root = FindRepositoryRoot();
        string shortcutSource = File.ReadAllText(Path.Combine(root, "Text-Grab", "Controls", "ShortcutControl.xaml.cs"));
        string wordBorderSource = File.ReadAllText(Path.Combine(root, "Text-Grab", "Controls", "WordBorder.xaml.cs"));

        Assert.Contains("$\"{automationId}.Record\"", shortcutSource, StringComparison.Ordinal);
        Assert.Contains("$\"{automationId}.Enabled\"", shortcutSource, StringComparison.Ordinal);
        Assert.Contains("$\"WordBorder.{ResultRowID}.{ResultColumnID}\"", wordBorderSource, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Text-Grab.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
