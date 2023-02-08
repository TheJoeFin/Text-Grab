using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Text_Grab.Properties;
using Text_Grab.Views;
// using Screen = System.Windows.Forms.Screen;
using WpfScreenHelper;
using static OSInterop;

namespace Text_Grab.Utilities;

public static class WindowUtilities
{
    public static void AddTextToOpenWindow(string textToAdd)
    {
        WindowCollection allWindows = Application.Current.Windows;

        foreach (Window window in allWindows)
            if (window is EditTextWindow mtw)
                mtw.AddThisText(textToAdd);
    }

    public static void SetWindowPosition(Window passedWindow)
    {
        string storedPostionString = "";

        if (passedWindow is EditTextWindow)
            storedPostionString = Properties.Settings.Default.EditTextWindowSizeAndPosition;

        if (passedWindow is GrabFrame)
            storedPostionString = Properties.Settings.Default.GrabFrameWindowSizeAndPosition;

        List<string> storedPostion = new(storedPostionString.Split(','));

        bool isStoredRectWithinScreen = false;

        if (storedPostion != null
            && storedPostion.Count == 4)
        {
            bool couldParseAll = false;
            couldParseAll = double.TryParse(storedPostion[0], out double parsedX);
            couldParseAll = double.TryParse(storedPostion[1], out double parsedY);
            couldParseAll = double.TryParse(storedPostion[2], out double parsedWid);
            couldParseAll = double.TryParse(storedPostion[3], out double parsedHei);
            Rect storedSize = new((int)parsedX, (int)parsedY, (int)parsedWid, (int)parsedHei);
            IEnumerable<Screen> allScreens = Screen.AllScreens;
            WindowCollection allWindows = Application.Current.Windows;

            if (parsedHei < 10 || parsedWid < 10)
                return;

            foreach (Screen screen in allScreens)
                if (screen.WpfBounds.IntersectsWith(storedSize))
                    isStoredRectWithinScreen = true;

            if (isStoredRectWithinScreen && couldParseAll)
            {
                passedWindow.Left = storedSize.X;
                passedWindow.Top = storedSize.Y;
                passedWindow.Width = storedSize.Width;
                passedWindow.Height = storedSize.Height;

                return;
            }
        }
    }

    public static void LaunchFullScreenGrab(TextBox? destinationTextBox = null)
    {
        IEnumerable<Screen> allScreens = Screen.AllScreens;
        WindowCollection allWindows = Application.Current.Windows;

        List<FullscreenGrab> allFullscreenGrab = new();

        int numberOfScreens = allScreens.Count();

        foreach (Window window in allWindows)
            if (window is FullscreenGrab)
                allFullscreenGrab.Add((FullscreenGrab)window);

        int numberOfFullscreenGrabWindowsToCreate = numberOfScreens - allFullscreenGrab.Count;

        for (int i = 0; i < numberOfFullscreenGrabWindowsToCreate; i++)
        {
            allFullscreenGrab.Add(new FullscreenGrab());
        }

        int count = 0;

        foreach (Screen screen in allScreens)
        {
            FullscreenGrab fullscreenGrab = allFullscreenGrab[count];
            fullscreenGrab.WindowStartupLocation = WindowStartupLocation.Manual;
            fullscreenGrab.Width = 400;
            fullscreenGrab.Height = 200;
            fullscreenGrab.DestinationTextBox = destinationTextBox;
            fullscreenGrab.WindowState = WindowState.Normal;

            System.Windows.Point screenCenterPoint = screen.GetCenterPoint();
            System.Windows.Point windowCenterPoint = fullscreenGrab.GetWindowCenter();

            double virtualScreenTop = SystemParameters.VirtualScreenTop;
            double virtualScreenLeft = SystemParameters.VirtualScreenLeft;
            double virtualScreenWidth = SystemParameters.VirtualScreenWidth;
            double virtualScreenHeight = SystemParameters.VirtualScreenHeight;

            fullscreenGrab.Left = screenCenterPoint.X - windowCenterPoint.X;
            fullscreenGrab.Top = screenCenterPoint.Y - windowCenterPoint.Y;

            fullscreenGrab.Show();
            fullscreenGrab.Activate();

            count++;
        }
    }

    public static System.Windows.Point GetCenterPoint(this Screen screen)
    {
        double x = screen.WpfBounds.Left + (screen.WpfBounds.Width / 2);
        double y = screen.WpfBounds.Top + (screen.WpfBounds.Height / 2);
        return new(x, y);
    }

    public static System.Windows.Point GetWindowCenter(this Window window)
    {
        double x = window.Width / 2;
        double y = window.Height / 2;
        return new(x, y);
    }

    internal static async void CloseAllFullscreenGrabs()
    {
        WindowCollection allWindows = Application.Current.Windows;

        bool isFromEditWindow = false;
        string stringFromOCR = "";

        foreach (Window window in allWindows)
        {
            if (window is FullscreenGrab fsg)
            {
                if (!string.IsNullOrWhiteSpace(fsg.textFromOCR))
                    stringFromOCR = fsg.textFromOCR;

                if (fsg.DestinationTextBox is not null)
                {
                    // TODO 3.0 Find out how to re normaize an ETW when FSG had it minimzed 
                    isFromEditWindow = true;
                    // if (fsg.EditWindow.WindowState == WindowState.Minimized)
                    //     fsg.EditWindow.WindowState = WindowState.Normal;
                }

                fsg.Close();
            }
        }

        if (Settings.Default.TryInsert
            && !string.IsNullOrWhiteSpace(stringFromOCR)
            && !isFromEditWindow)
        {
            await Task.Delay(TimeSpan.FromSeconds(Settings.Default.InsertDelay));
            TryInsertString(stringFromOCR);
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

    internal static void TryInsertString(string stringToInsert)
    {
        List<INPUT> inputs = new List<INPUT>();
        // make sure keys are up.
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.LCONTROL);
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.RCONTROL);
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.LWIN);
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.RWIN);
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.LSHIFT);
        TryInjectModifierKeyUp(ref inputs, VirtualKeyShort.RSHIFT);

        // send Ctrl+V (key downs and key ups)
        INPUT ctrlDown = new();
        ctrlDown.Type = OSInterop.InputType.INPUT_KEYBOARD;
        ctrlDown.U.Ki.WVk = VirtualKeyShort.CONTROL;
        inputs.Add(ctrlDown);

        INPUT vDown = new();
        vDown.Type = OSInterop.InputType.INPUT_KEYBOARD;
        vDown.U.Ki.WVk = VirtualKeyShort.KEY_V;
        inputs.Add(vDown);

        INPUT vUp = new();
        vUp.Type = OSInterop.InputType.INPUT_KEYBOARD;
        vUp.U.Ki.WVk = VirtualKeyShort.KEY_V;
        vUp.U.Ki.DwFlags = KEYEVENTF.KEYUP;
        inputs.Add(vUp);

        INPUT ctrlUp = new();
        ctrlUp.Type = OSInterop.InputType.INPUT_KEYBOARD;
        ctrlUp.U.Ki.WVk = VirtualKeyShort.CONTROL;
        ctrlUp.U.Ki.DwFlags = KEYEVENTF.KEYUP;
        inputs.Add(ctrlUp);

        _ = SendInput((uint)inputs.Count, inputs.ToArray(), INPUT.Size);
    }

    private static void TryInjectModifierKeyUp(ref List<INPUT> inputs, VirtualKeyShort modifier)
    {
        // Most significant bit is set if key is down
        if ((GetAsyncKeyState((int)modifier) & 0x8000) != 0)
        {
            var inputEvent = default(INPUT);
            inputEvent.Type = OSInterop.InputType.INPUT_KEYBOARD;
            inputEvent.U.Ki.WVk = modifier;
            inputEvent.U.Ki.DwFlags = KEYEVENTF.KEYUP;
            inputs.Add(inputEvent);
        }
    }

    internal static T OpenOrActivateWindow<T>() where T : Window, new()
    {
        WindowCollection allWindows = Application.Current.Windows;

        foreach (var window in allWindows)
        {
            if (window is T matchWindow)
            {
                matchWindow.Activate();
                return matchWindow;
            }
        }

        // No Window Found, open a new one
        T newWindow = new T();
        newWindow.Show();
        return newWindow;
    }

    public static void ShouldShutDown()
    {
        bool zeroOpenWindows = Application.Current.Windows.Count < 1;

        bool shouldShutDown = false;

        if (Settings.Default.RunInTheBackground)
        {
            if (App.Current is App app)
            {
                if (app.NumberOfRunningInstances > 1
                    && app.TextGrabIcon == null
                    && zeroOpenWindows)
                    shouldShutDown = true;
            }
        }
        else if (zeroOpenWindows)
            shouldShutDown = true;

        if (shouldShutDown)
            Application.Current.Shutdown();
    }
}
