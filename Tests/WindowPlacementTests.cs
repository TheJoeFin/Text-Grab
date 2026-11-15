using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;
using static Tests.EditTextWindowTestHost;

namespace Tests;

[Collection("History service")]
public class WindowPlacementTests
{
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
