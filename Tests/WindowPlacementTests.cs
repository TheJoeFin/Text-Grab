using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Text_Grab.Core.Layouts;
using static Tests.EditTextWindowTestHost;

namespace Tests;

[Collection("History service")]
public class WindowPlacementTests
{
    [WpfFact]
    public async Task BottomBarPreference_FollowsFileTypeAndBlankWindow()
    {
        string root = Path.Combine(Path.GetTempPath(), $"text-grab-bottom-bar-{Guid.NewGuid():N}");
        var settings = AppUtilities.TextGrabSettings;
        string originalPreferences = settings.EditWindowBottomBarHiddenByFileType;
        bool originalHidden = settings.EditWindowBottomBarIsHidden;
        bool originalUseHistory = settings.UseHistory;
        try
        {
            settings.EditWindowBottomBarHiddenByFileType = string.Empty;
            settings.EditWindowBottomBarIsHidden = false;
            settings.UseHistory = false;
            Assert.True(BottomBarFileTypePreferences.SetHidden(settings, null, true));
            Assert.True(BottomBarFileTypePreferences.SetHidden(settings, "notes.md", false));
            Assert.True(BottomBarFileTypePreferences.SetHidden(settings, "notes.txt", true));

            Directory.CreateDirectory(root);
            string markdown = Path.Combine(root, "first.MD");
            string text = Path.Combine(root, "second.txt");
            File.WriteAllText(markdown, "# Hello");
            File.WriteAllText(text, "Hello");
            EditTextWindow window = CreateWindow(string.Empty);
            Invoke(window, "RestoreBottomBarForFileType");
            AssertBottomBar(window, hidden: true);

            await window.OpenPath(markdown);
            AssertBottomBar(window, hidden: false);
            await window.OpenPath(text);
            AssertBottomBar(window, hidden: true);

            Invoke(window, "SetOpenedFileState", Path.Combine(root, "new.md"));
            AssertBottomBar(window, hidden: false);
            Invoke(window, "SetOpenedFileState", (object?)null);
            AssertBottomBar(window, hidden: true);
            Assert.False(BottomBarFileTypePreferences.IsHidden(settings, "unknown.csv"));
        }
        finally
        {
            settings.EditWindowBottomBarHiddenByFileType = originalPreferences;
            settings.EditWindowBottomBarIsHidden = originalHidden;
            settings.UseHistory = originalUseHistory;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BottomBarPreference_InvalidDataIsNotOverwritten()
    {
        Text_Grab.Properties.Settings settings = new()
        {
            EditWindowBottomBarIsHidden = true,
            EditWindowBottomBarHiddenByFileType = "{invalid",
        };

        Assert.True(BottomBarFileTypePreferences.IsHidden(settings, "notes.txt"));
        Assert.False(BottomBarFileTypePreferences.SetHidden(settings, "notes.txt", false));
        Assert.Equal("{invalid", settings.EditWindowBottomBarHiddenByFileType);
    }

    [WpfTheory]
    [InlineData(EtwEditorMode.Markdown, null, false)]
    [InlineData(EtwEditorMode.Text, null, true)]
    [InlineData(EtwEditorMode.Text, ".md", false)]
    [InlineData(EtwEditorMode.Markdown, ".txt", true)]
    public async Task RecentHistory_UsesDocumentTypeForBottomBar(EtwEditorMode mode, string? extension, bool hidden)
    {
        var settings = AppUtilities.TextGrabSettings;
        string originalPreferences = settings.EditWindowBottomBarHiddenByFileType;
        bool originalHidden = settings.EditWindowBottomBarIsHidden;
        try
        {
            settings.EditWindowBottomBarHiddenByFileType = string.Empty;
            settings.EditWindowBottomBarIsHidden = false;
            Assert.True(BottomBarFileTypePreferences.SetHidden(settings, null, false));
            Assert.True(BottomBarFileTypePreferences.SetHidden(settings, "notes.md", false));
            Assert.True(BottomBarFileTypePreferences.SetHidden(settings, "notes.txt", true));
            HistoryInfo history = new()
            {
                ID = "history",
                TextContent = "Some text",
                EditorMode = mode,
                BottomBarFileExtension = extension,
            };

            EditTextWindow newWindow = new(history);
            Invoke(newWindow, "RestoreBottomBarForFileType");
            AssertBottomBar(newWindow, hidden);

            EditTextWindow reusedWindow = CreateWindow(string.Empty);
            await reusedWindow.OpenRecentItemAsync(new TrayFlyoutItem(TrayFlyoutItemKind.EditText, history), inNewWindow: false);
            AssertBottomBar(reusedWindow, hidden);
            Assert.Equal(extension ?? (mode == EtwEditorMode.Markdown ? ".md" : ".txt"),
                reusedWindow.AsHistoryItem().BottomBarFileExtension);
        }
        finally
        {
            settings.EditWindowBottomBarHiddenByFileType = originalPreferences;
            settings.EditWindowBottomBarIsHidden = originalHidden;
        }
    }

    [WpfTheory]
    [InlineData(".md", false)]
    [InlineData(".txt", true)]
    public async Task RecentFile_UsesDocumentTypeForBottomBar(string extension, bool hidden)
    {
        string root = Path.Combine(Path.GetTempPath(), $"text-grab-recent-{Guid.NewGuid():N}");
        var settings = AppUtilities.TextGrabSettings;
        string originalPreferences = settings.EditWindowBottomBarHiddenByFileType;
        bool originalHidden = settings.EditWindowBottomBarIsHidden;
        bool originalUseHistory = settings.UseHistory;
        try
        {
            settings.EditWindowBottomBarHiddenByFileType = string.Empty;
            settings.EditWindowBottomBarIsHidden = false;
            settings.UseHistory = false;
            Assert.True(BottomBarFileTypePreferences.SetHidden(settings, "notes.md", false));
            Assert.True(BottomBarFileTypePreferences.SetHidden(settings, "notes.txt", true));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "notes" + extension);
            File.WriteAllText(path, "Some text");
            EditTextWindow window = CreateWindow(string.Empty);
            await window.OpenRecentItemAsync(
                new TrayFlyoutItem(TrayFlyoutItemKind.File, new HistoryInfo { SourcePath = path }), inNewWindow: false);
            AssertBottomBar(window, hidden);

            EditTextWindow flyoutWindow = new(new LayoutRequest(FilePath: path));
            Invoke(flyoutWindow, "RestoreBottomBarForFileType");
            await flyoutWindow.OpenPath(path);
            AssertBottomBar(flyoutWindow, hidden);
        }
        finally
        {
            settings.EditWindowBottomBarHiddenByFileType = originalPreferences;
            settings.EditWindowBottomBarIsHidden = originalHidden;
            settings.UseHistory = originalUseHistory;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertBottomBar(EditTextWindow window, bool hidden)
    {
        FrameworkElement bar = (FrameworkElement)window.FindName("BottomBar");
        MenuItem menu = (MenuItem)window.FindName("HideBottomBarMenuItem");
        Assert.Equal(hidden ? Visibility.Collapsed : Visibility.Visible, bar.Visibility);
        Assert.Equal(hidden, menu.IsChecked);
    }

    private static void Invoke(EditTextWindow window, string name, params object?[] args)
        => typeof(EditTextWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);

    [WpfTheory]
    [InlineData(WindowState.Normal, false)]
    [InlineData(WindowState.Maximized, false)]
    [InlineData(WindowState.Minimized, false)]
    [InlineData(WindowState.Normal, true)]
    [InlineData(WindowState.Maximized, true)]
    [InlineData(WindowState.Minimized, true)]
    public void RoundTrip_RestoresNormalBoundsAndSavedState(WindowState state, bool restoreAfterShow)
    {
        Window source = CreatePlacementWindow();
        Window restored = CreatePlacementWindow();
        try
        {
            source.Show();
            DoEvents();
            Rect normalBounds = source.RestoreBounds;
            source.WindowState = state;
            DoEvents();

            string placement = WindowUtilities.GetWindowBoundsString(source);
            source.Close();

            if (restoreAfterShow)
            {
                restored.Show();
                restored.WindowState = WindowState.Maximized;
                DoEvents();
            }

            Assert.True(WindowUtilities.TryApplyStoredBounds(restored, placement));
            if (!restoreAfterShow)
                restored.Show();
            DoEvents();

            Assert.Equal(state == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal, restored.WindowState);
            restored.WindowState = WindowState.Normal;
            DoEvents();
            AssertBounds(normalBounds, restored.RestoreBounds);
        }
        finally
        {
            source.Close();
            restored.Close();
        }
    }

    [WpfFact]
    public void TryApplyStoredBounds_LegacyBoundsRestoreAsNormal()
    {
        Window window = CreatePlacementWindow();
        try
        {
            window.Show();
            DoEvents();
            Rect normalBounds = window.RestoreBounds;
            string legacyPlacement = FormattableString.Invariant(
                $"{normalBounds.Left},{normalBounds.Top},{normalBounds.Width},{normalBounds.Height}");
            window.WindowState = WindowState.Maximized;

            Assert.True(WindowUtilities.TryApplyStoredBounds(window, legacyPlacement));
            DoEvents();

            Assert.Equal(WindowState.Normal, window.WindowState);
            AssertBounds(normalBounds, window.RestoreBounds);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0,0,640")]
    [InlineData("0,0,640,420,Maximized,extra")]
    [InlineData("0,0,640,420,Minimized")]
    [InlineData("0,0,640,420,Unknown")]
    [InlineData("NaN,0,640,420,Maximized")]
    [InlineData("0,0,Infinity,420,Maximized")]
    [InlineData("0,0,-640,420,Maximized")]
    [InlineData("-100000,-100000,640,420,Maximized")]
    public void TryApplyStoredBounds_InvalidPlacementDoesNotChangeWindow(string? placement)
    {
        Window window = CreatePlacementWindow();
        try
        {
            Rect bounds = new(window.Left, window.Top, window.Width, window.Height);
            window.WindowState = WindowState.Maximized;

            Assert.False(WindowUtilities.TryApplyStoredBounds(window, placement));

            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.Equal(bounds, new Rect(window.Left, window.Top, window.Width, window.Height));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void RoundTrip_PreservesFractionalBoundsWithCommaDecimalSeparator()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        Window source = CreatePlacementWindow();
        Window restored = CreatePlacementWindow();
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            source.Left += 0.5;
            source.Top += 0.5;
            source.Width += 0.5;
            source.Height += 0.5;

            string placement = WindowUtilities.GetWindowBoundsString(source);

            Assert.Equal(5, placement.Split(',').Length);
            Assert.True(WindowUtilities.TryApplyStoredBounds(restored, placement));
            Assert.Equal(source.Left, restored.Left);
            Assert.Equal(source.Top, restored.Top);
            Assert.Equal(source.Width, restored.Width);
            Assert.Equal(source.Height, restored.Height);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            source.Close();
            restored.Close();
        }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditTextWindow_ClosingPersistsMaximizedPlacementBeforeClosed(bool perFile)
    {
        string root = Path.Combine(Path.GetTempPath(), $"text-grab-placement-{Guid.NewGuid():N}");
        AutomationProfile profile = AutomationProfile.TryCreate(
            ["Text-Grab.exe"],
            name => name == AutomationProfile.ProfileEnvironmentVariable ? root : null)
            ?? throw new InvalidOperationException("Failed to create automation profile.");
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(profile);
        var settings = AppUtilities.TextGrabSettings;
        bool originalRememberFilePlacement = settings.EtwRememberFilePlacement;
        bool originalUseHistory = settings.UseHistory;
        string originalPlacement = settings.EditTextWindowSizeAndPosition;
        EditTextWindow? source = null;
        Window restored = CreatePlacementWindow();
        try
        {
            settings.EtwRememberFilePlacement = true;
            settings.UseHistory = false;
            settings.EditTextWindowSizeAndPosition = string.Empty;
            string path = Path.Combine(root, "document.txt");
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "Document text");

            source = new EditTextWindow { ShowActivated = false, ShowInTaskbar = false };
            // Keep the real Closing handler, without Loaded's clipboard setup or Closed's app shutdown.
            source.Loaded -= WindowHandler<RoutedEventHandler>(source, "Window_Loaded");
            source.Closed -= WindowHandler<EventHandler>(source, "Window_Closed");
            if (perFile)
                await source.OpenPath(path);

            SetNormalBounds(source);
            source.Show();
            DoEvents();
            Rect normalBounds = source.RestoreBounds;
            source.WindowState = WindowState.Maximized;
            DoEvents();
            source.Close();

            string? placement = perFile
                ? FileWindowPlacementStore.GetBounds(path)
                : settings.EditTextWindowSizeAndPosition;
            Assert.False(string.IsNullOrEmpty(placement));
            Assert.True(WindowUtilities.TryApplyStoredBounds(restored, placement));
            restored.Show();
            DoEvents();
            Assert.Equal(WindowState.Maximized, restored.WindowState);
            restored.WindowState = WindowState.Normal;
            DoEvents();
            AssertBounds(normalBounds, restored.RestoreBounds);

            if (perFile)
                Assert.Empty(settings.EditTextWindowSizeAndPosition);
        }
        finally
        {
            source?.Close();
            restored.Close();
            settings.EtwRememberFilePlacement = originalRememberFilePlacement;
            settings.UseHistory = originalUseHistory;
            settings.EditTextWindowSizeAndPosition = originalPlacement;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static Window CreatePlacementWindow()
    {
        Window window = new() { ShowInTaskbar = false };
        SetNormalBounds(window);
        return window;
    }

    private static void SetNormalBounds(Window window)
    {
        Rect workArea = SystemParameters.WorkArea;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = workArea.Left + 32;
        window.Top = workArea.Top + 32;
        window.Width = 640;
        window.Height = 420;
    }

    private static void AssertBounds(Rect expected, Rect actual)
    {
        Assert.Equal(expected.Left, actual.Left, precision: 1);
        Assert.Equal(expected.Top, actual.Top, precision: 1);
        Assert.Equal(expected.Width, actual.Width, precision: 1);
        Assert.Equal(expected.Height, actual.Height, precision: 1);
    }

    private static T WindowHandler<T>(EditTextWindow window, string name) where T : Delegate
        => typeof(EditTextWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<T>(window);
}
