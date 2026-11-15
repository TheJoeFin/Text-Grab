using Dapplo.Windows.User32;
using Fasetto.Word;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Controls;
using Text_Grab.Extensions;
using Text_Grab.Services;
using Text_Grab.Views;
using static OSInterop;

namespace Text_Grab.Utilities;

public static partial class WindowUtilities
{
    private static Dictionary<string, bool>? fullscreenPostGrabActionStates;

    public static void AddTextToOpenWindow(string textToAdd)
    {
        WindowCollection allWindows = Application.Current.Windows;

        foreach (Window window in allWindows)
            if (window is EditTextWindow mtw)
                mtw.AddThisText(textToAdd);
    }

    internal static void ClickScreenPoint(Point screenPoint)
    {
        const int SmXVirtualScreen = 76;
        const int SmYVirtualScreen = 77;
        const int SmCxVirtualScreen = 78;
        const int SmCyVirtualScreen = 79;

        int virtualLeft = OSInterop.GetSystemMetrics(SmXVirtualScreen);
        int virtualTop = OSInterop.GetSystemMetrics(SmYVirtualScreen);
        int virtualWidth = OSInterop.GetSystemMetrics(SmCxVirtualScreen);
        int virtualHeight = OSInterop.GetSystemMetrics(SmCyVirtualScreen);

        if (virtualWidth <= 1 || virtualHeight <= 1)
            throw new InvalidOperationException("No interactive screen is available.");

        int normalizedX = (int)Math.Round((screenPoint.X - virtualLeft) * 65535d / (virtualWidth - 1));
        int normalizedY = (int)Math.Round((screenPoint.Y - virtualTop) * 65535d / (virtualHeight - 1));

        INPUT move = new() { Type = OSInterop.InputType.INPUT_MOUSE };
        move.U.Mi.Dx = Math.Clamp(normalizedX, 0, 65535);
        move.U.Mi.Dy = Math.Clamp(normalizedY, 0, 65535);
        move.U.Mi.DwFlags = MOUSEEVENTF.MOVE | MOUSEEVENTF.ABSOLUTE | MOUSEEVENTF.VIRTUALDESK;

        INPUT down = new() { Type = OSInterop.InputType.INPUT_MOUSE };
        down.U.Mi.DwFlags = MOUSEEVENTF.LEFTDOWN;

        INPUT up = new() { Type = OSInterop.InputType.INPUT_MOUSE };
        up.U.Mi.DwFlags = MOUSEEVENTF.LEFTUP;

        uint sent = SendInput(3, [move, down, up], INPUT.Size);
        if (sent != 3)
            throw new InvalidOperationException($"Could not send the mouse click. Sent {sent} of 3 input events.");
    }

    public static void SetWindowPosition(Window passedWindow)
    {
        string storedPositionString = "";

        if (passedWindow is EditTextWindow)
            storedPositionString = AppUtilities.TextGrabSettings.EditTextWindowSizeAndPosition;

        if (passedWindow is GrabFrame)
            storedPositionString = AppUtilities.TextGrabSettings.GrabFrameWindowSizeAndPosition;

        TryApplyStoredBounds(passedWindow, storedPositionString);
    }

    /// <summary>
    /// The window's normal-state "left,top,width,height,state" as stored in settings.
    /// Capture before Closed, while RestoreBounds is still available. Minimized windows reopen normally.
    /// </summary>
    public static string GetWindowBoundsString(Window window)
    {
        double left = window.Left, top = window.Top, width = window.Width, height = window.Height;

        if (window.WindowState != WindowState.Normal)
        {
            Rect restore = window.RestoreBounds;
            if (restore.IsEmpty)
                throw new InvalidOperationException("Window placement must be captured before the window is closed.");

            (left, top, width, height) = (restore.Left, restore.Top, restore.Width, restore.Height);
        }

        WindowState state = window.WindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
        return FormattableString.Invariant($"{left},{top},{width},{height},{state}");
    }

    /// <summary>
    /// Applies stored normal bounds and optional state when the bounds still intersect a screen.
    /// Older four-value placements restore as normal windows.
    /// </summary>
    /// <returns>True when the bounds were applied.</returns>
    public static bool TryApplyStoredBounds(Window passedWindow, string? storedPositionString)
    {
        if (string.IsNullOrWhiteSpace(storedPositionString))
            return false;

        List<string> storedPosition = [.. storedPositionString.Split(',')];

        if (storedPosition.Count is not (4 or 5))
        {
            Debug.WriteLine("Could not restore window placement: expected four bounds and an optional window state.");
            return false;
        }

        WindowState state = WindowState.Normal;
        if (storedPosition.Count == 5
            && (!Enum.TryParse(storedPosition[4], out state) || state is not (WindowState.Normal or WindowState.Maximized)))
        {
            Debug.WriteLine("Could not restore window placement: invalid window state.");
            return false;
        }

        CultureInfo culture = storedPosition.Count == 5 ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
        bool couldParseX = double.TryParse(storedPosition[0], NumberStyles.Float, culture, out double parsedX);
        bool couldParseY = double.TryParse(storedPosition[1], NumberStyles.Float, culture, out double parsedY);
        bool couldParseW = double.TryParse(storedPosition[2], NumberStyles.Float, culture, out double parsedWid);
        bool couldParseH = double.TryParse(storedPosition[3], NumberStyles.Float, culture, out double parsedHei);

        if (!(couldParseX && couldParseY && couldParseW && couldParseH)
            || !double.IsFinite(parsedX) || !double.IsFinite(parsedY)
            || !double.IsFinite(parsedWid) || !double.IsFinite(parsedHei)
            || parsedHei < 10 || parsedWid < 10)
        {
            Debug.WriteLine("Could not restore window placement: invalid window bounds.");
            return false;
        }

        Rect storedSize = new(parsedX, parsedY, parsedWid, parsedHei);
        bool isStoredRectWithinScreen = false;

        foreach (DisplayInfo screen in DisplayInfo.AllDisplayInfos)
        {
            Rect screenRect = new(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height);
            DpiScale dpi = System.Windows.Media.VisualTreeHelper.GetDpi(passedWindow);
            screenRect = screenRect.GetScaledDownByDpi(dpi);
            if (screenRect.IntersectsWith(storedSize))
                isStoredRectWithinScreen = true;
        }

        if (!isStoredRectWithinScreen)
        {
            Debug.WriteLine("Could not restore window placement: the saved bounds are no longer on a screen.");
            return false;
        }

        passedWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        passedWindow.WindowState = WindowState.Normal;
        passedWindow.Left = storedSize.X;
        passedWindow.Top = storedSize.Y;
        passedWindow.Width = storedSize.Width;
        passedWindow.Height = storedSize.Height;
        passedWindow.WindowState = state;
        return true;
    }

    public static void LaunchFullScreenGrab(TextBox? destinationTextBox = null)
    {
        LaunchFullScreenGrab(destinationTextBox, null);
    }

    public static void LaunchFullScreenGrab(TextBox? destinationTextBox, string? preselectedTemplateId)
    {
        DisplayInfo[] allScreens = DisplayInfo.AllDisplayInfos;
        WindowCollection allWindows = Application.Current.Windows;

        List<FullscreenGrab> allFullscreenGrab = [];

        int numberOfScreens = allScreens.Length;

        foreach (Window window in allWindows)
            if (window is FullscreenGrab grab)
                allFullscreenGrab.Add(grab);

        if (allFullscreenGrab.Count == 0)
            ClearFullscreenPostGrabActionStates();

        int numberOfFullscreenGrabWindowsToCreate = numberOfScreens - allFullscreenGrab.Count;

        for (int i = 0; i < numberOfFullscreenGrabWindowsToCreate; i++)
        {
            allFullscreenGrab.Add(new FullscreenGrab());
        }

        int count = 0;

        double sideLength = 40;

        foreach (DisplayInfo screen in allScreens)
        {
            FullscreenGrab fullScreenGrab = allFullscreenGrab[count];
            fullScreenGrab.WindowStartupLocation = WindowStartupLocation.Manual;
            fullScreenGrab.Width = sideLength;
            fullScreenGrab.Height = sideLength;
            fullScreenGrab.DestinationTextBox = destinationTextBox;
            fullScreenGrab.PreselectedTemplateId = preselectedTemplateId;
            fullScreenGrab.WindowState = WindowState.Normal;

            Point screenCenterPoint = screen.ScaledCenterPoint();

            fullScreenGrab.Left = screenCenterPoint.X - (sideLength / 2);
            fullScreenGrab.Top = screenCenterPoint.Y - (sideLength / 2);

            fullScreenGrab.Show();
            fullScreenGrab.Activate();

            count++;
        }
    }

    public static Point GetCenterPoint(this DisplayInfo screen)
    {
        Rect screenRect = new(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height);
        double x = screenRect.Left + (screenRect.Width / 2);
        double y = screenRect.Top + (screenRect.Height / 2);
        return new(x, y);
    }

    public static System.Windows.Point GetWindowCenter(this Window window)
    {
        double x = window.Width / 2;
        double y = window.Height / 2;
        return new(x, y);
    }

    public static void CenterOverThisWindow(this Window newWindow, Window bottomWindow)
    {
        System.Windows.Point newWindowCenter = newWindow.GetWindowCenter();
        System.Windows.Point bottomWindowCenter = bottomWindow.GetWindowCenter();

        double newWindowTop = (bottomWindow.Top + bottomWindowCenter.Y) - newWindowCenter.Y;
        double newWindowLeft = (bottomWindow.Left + bottomWindowCenter.X) - newWindowCenter.X;

        newWindow.Top = newWindowTop;
        newWindow.Left = newWindowLeft;
    }

    internal static async void CloseAllFullscreenGrabs()
    {
        WindowCollection allWindows = Application.Current.Windows;
        ClearFullscreenPostGrabActionStates();

        bool isFromEditWindow = false;
        string stringFromOCR = "";

        foreach (Window window in allWindows)
        {
            if (window is FullscreenGrab fsg)
            {
                if (!string.IsNullOrWhiteSpace(fsg.TextFromOCR))
                    stringFromOCR = fsg.TextFromOCR;

                if (fsg.DestinationTextBox is not null)
                {
                    // TODO 3.0 Find out how to re normalize an ETW when FSG had it minimized 
                    isFromEditWindow = true;
                    // if (fsg.EditWindow.WindowState == WindowState.Minimized)
                    //     fsg.EditWindow.WindowState = WindowState.Normal;
                }

                fsg.Close();
            }
        }

        if (AppUtilities.TextGrabSettings.TryInsert
            && !string.IsNullOrWhiteSpace(stringFromOCR)
            && !isFromEditWindow)
        {
            await TryInsertString(stringFromOCR);
        }

        GC.Collect();
        ShouldShutDown();
    }

    internal static void FullscreenKeyDown(Key key, bool? isActive = null)
    {
        WindowCollection allWindows = Application.Current.Windows;

        if (key == Key.Escape)
            CloseAllFullscreenGrabs();

        foreach (Window window in allWindows)
            if (window is FullscreenGrab fsg)
                fsg.KeyPressed(key, isActive);
    }

    internal static void SyncFullscreenPostGrabActionStates(IReadOnlyDictionary<string, bool> actionStates, FullscreenGrab? sourceWindow = null)
    {
        fullscreenPostGrabActionStates = new Dictionary<string, bool>(actionStates);

        WindowCollection allWindows = Application.Current.Windows;
        foreach (Window window in allWindows)
        {
            if (window is FullscreenGrab fsg && fsg != sourceWindow)
                fsg.ApplyPostGrabActionSnapshot(fullscreenPostGrabActionStates);
        }
    }

    internal static IReadOnlyDictionary<string, bool>? GetFullscreenPostGrabActionStates()
    {
        if (fullscreenPostGrabActionStates is null || fullscreenPostGrabActionStates.Count == 0)
            return null;

        return new Dictionary<string, bool>(fullscreenPostGrabActionStates);
    }

    internal static void ClearFullscreenPostGrabActionStates()
    {
        fullscreenPostGrabActionStates = null;
    }

    internal static async Task TryInsertString(string stringToInsert)
    {
        await Task.Delay(TimeSpan.FromSeconds(AppUtilities.TextGrabSettings.InsertDelay));

        List<INPUT> inputs = [];
        // make sure keys are up.
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.LCONTROL);
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.RCONTROL);
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.LWIN);
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.RWIN);
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.LSHIFT);
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.RSHIFT);

        // send Ctrl+V (key downs and key ups)
        INPUT ctrlDown = new()
        {
            Type = OSInterop.InputType.INPUT_KEYBOARD
        };
        ctrlDown.U.Ki.WVk = VirtualKeyShort.CONTROL;
        inputs.Add(ctrlDown);

        INPUT vDown = new()
        {
            Type = OSInterop.InputType.INPUT_KEYBOARD
        };
        vDown.U.Ki.WVk = VirtualKeyShort.KEY_V;
        inputs.Add(vDown);

        INPUT vUp = new()
        {
            Type = OSInterop.InputType.INPUT_KEYBOARD
        };
        vUp.U.Ki.WVk = VirtualKeyShort.KEY_V;
        vUp.U.Ki.DwFlags = KEYEVENTF.KEYUP;
        inputs.Add(vUp);

        INPUT ctrlUp = new()
        {
            Type = OSInterop.InputType.INPUT_KEYBOARD
        };
        ctrlUp.U.Ki.WVk = VirtualKeyShort.CONTROL;
        ctrlUp.U.Ki.DwFlags = KEYEVENTF.KEYUP;
        inputs.Add(ctrlUp);

        _ = SendInput((uint)inputs.Count, [.. inputs], INPUT.Size);
        await Task.CompletedTask;
    }

    private static void TryInjectModifierKeyUp(ref List<INPUT> inputs, VirtualKeyShort modifier)
    {
        // Most significant bit is set if key is down
        if ((GetAsyncKeyState((int)modifier) & 0x8000) != 0)
        {
            INPUT inputEvent = default;
            inputEvent.Type = OSInterop.InputType.INPUT_KEYBOARD;
            inputEvent.U.Ki.WVk = modifier;
            inputEvent.U.Ki.DwFlags = KEYEVENTF.KEYUP;
            inputs.Add(inputEvent);
        }
    }

    internal static bool ShouldOpenNewEtwInSpreadsheetMode(bool isTableModeSelected, bool hasExistingEditTextWindow)
    {
        return isTableModeSelected && !hasExistingEditTextWindow;
    }

    /// <summary>
    /// When a new grab (Grab Frame or Full Screen Grab) is launched from an Edit Text Window
    /// that is already in Spreadsheet mode, Table mode should be preselected so the OCR
    /// result comes back column-aware instead of being stuffed into a single cell.
    /// </summary>
    internal static bool ShouldForceTableModeForNewGrab(
        bool hasDestinationTextBox,
        bool isDestinationSpreadsheetMode,
        bool isTableModeAvailable)
    {
        return hasDestinationTextBox && isDestinationSpreadsheetMode && isTableModeAvailable;
    }

    internal static EditTextWindow OpenOrActivateEditTextWindow(bool isTableModeSelected = false)
    {
        WindowCollection allWindows = Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is EditTextWindow matchWindow)
            {
                matchWindow.Activate();
                return matchWindow;
            }
        }

        EditTextWindow newWindow = new();
        if (ShouldOpenNewEtwInSpreadsheetMode(isTableModeSelected, hasExistingEditTextWindow: false))
            newWindow.EnterSpreadsheetMode();

        try
        {
            newWindow.Show();
        }
        catch (Exception ex)
        {
            _ = new Wpf.Ui.Controls.MessageBox
            {
                Title = ex.Message,
                Content = "An error occurred while trying to open a new window. Please try again.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }

        return newWindow;
    }

    /// <summary>
    /// Always opens a fresh Edit Text Window holding <paramref name="text"/>, optionally already
    /// in spreadsheet mode, regardless of whether other Edit Text Windows are open.
    /// </summary>
    internal static EditTextWindow? OpenTextInNewEditTextWindow(string text, bool enterSpreadsheetMode = false)
    {
        EditTextWindow newWindow = new(text, isEncoded: false);

        try
        {
            // Switch modes before Show(): the mode switch only touches XAML elements that
            // InitializeComponent() already wired up, and the text is parsed into cells on entry.
            if (enterSpreadsheetMode)
                newWindow.EnterSpreadsheetMode();

            newWindow.Show();
            return newWindow;
        }
        catch (Exception ex)
        {
            _ = new Wpf.Ui.Controls.MessageBox
            {
                Title = ex.Message,
                Content = "An error occurred while trying to open a new window. Please try again.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return null;
        }
    }

    internal static T OpenOrActivateWindow<T>() where T : Window, new()
    {
        WindowCollection allWindows = Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is T matchWindow)
            {
                matchWindow.Activate();
                return matchWindow;
            }
        }

        // No Window Found, open a new one
        T newWindow = new();

        try
        {
            newWindow.Show();
        }
        catch (Exception ex)
        {
            _ = new Wpf.Ui.Controls.MessageBox
            {
                Title = ex.Message,
                Content = "An error occurred while trying to open a new window. Please try again.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
        return newWindow;
    }

    public static void ShouldShutDown()
    {
        // The tray flyout is created once and then only hidden, so while hidden it must not
        // keep the process alive after Run in the background is turned off.
        bool zeroOpenWindows = !Application.Current.Windows
            .OfType<Window>()
            .Any(window => window is not TrayFlyoutWindow || window.IsVisible);

        bool shouldShutDown = false;

        if (AppUtilities.TextGrabSettings.RunInTheBackground)
        {
            AppRuntimeState state = AppServices.Provider.GetRequiredService<AppRuntimeState>();

            if (state.NumberOfRunningInstances > 1
                && state.TextGrabIcon == null
                && zeroOpenWindows)
                shouldShutDown = true;

            if (state.TextGrabIcon is not null
                && state.TextGrabIcon.Icon is null
                && zeroOpenWindows)
                shouldShutDown = true;
        }
        else if (zeroOpenWindows)
            shouldShutDown = true;

        if (shouldShutDown)
        {
            // Let any queued/in-flight speech finish before exiting. RunWhenIdle
            // runs the callback immediately when idle, or once the queue drains;
            // registration is atomic with the idle check so shutdown can't be
            // missed if speech finishes right as we ask.
            Singleton<TtsService>.Instance.RunWhenIdle(
                () => Application.Current.Dispatcher.Invoke(Application.Current.Shutdown));
        }
    }

    public static bool GetMousePosition(out Point mousePosition)
    {
        if (GetCursorPos(out POINT point))
        {
            mousePosition = new Point(point.X, point.Y);
            return true;
        }
        mousePosition = default;
        return false;
    }

    public static bool IsMouseInWindow(this Window window)
    {
        GetMousePosition(out Point mousePosition);

        DpiScale dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
        Point absPosPoint = window.GetAbsolutePosition();
        Rect windowRect = new(absPosPoint.X, absPosPoint.Y,
            window.ActualWidth * dpi.DpiScaleX,
            window.ActualHeight * dpi.DpiScaleY);
        return windowRect.Contains(mousePosition);
    }

    public static ScrollViewer? GetScrollViewer(DependencyObject obj)
    {
        if (obj is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(obj, i);
            ScrollViewer? result = GetScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }

    #region DLLImport

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT lpPoint);

    #endregion
}
