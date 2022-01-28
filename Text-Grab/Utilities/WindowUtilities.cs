using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;
using Text_Grab.Properties;
using Text_Grab.Views;

namespace Text_Grab.Utilities
{
    public static class WindowUtilities
    {
        public static void AddTextToOpenWindow(string textToAdd)
        {
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

            foreach (Window window in allWindows)
            {
                if (window is EditTextWindow mtw)
                {
                    mtw.AddThisText(textToAdd);
                }
            }
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
                Rectangle storedSize = new Rectangle((int)parsedX, (int)parsedY, (int)parsedWid, (int)parsedHei);
                Screen[] allScreens = Screen.AllScreens;
                WindowCollection allWindows = System.Windows.Application.Current.Windows;

                if (parsedHei < 10 || parsedWid < 10)
                    return;

                foreach (Screen screen in allScreens)
                {
                    if (screen.WorkingArea.IntersectsWith(storedSize))
                        isStoredRectWithinScreen = true;
                }

                if (isStoredRectWithinScreen == true && couldParseAll == true)
                {
                    passedWindow.Left = storedSize.X;
                    passedWindow.Top = storedSize.Y;
                    passedWindow.Width = storedSize.Width;
                    passedWindow.Height = storedSize.Height;

                    return;
                }
            }
        }

        public static void LaunchFullScreenGrab(bool openAnyway = false,
                                                bool setBackgroundImage = false,
                                                EditTextWindow? editWindow = null)
        {
            Screen[] allScreens = Screen.AllScreens;
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

            List<FullscreenGrab> allFullscreenGrab = new();

            foreach (Screen screen in allScreens)
            {
                bool screenHasWindow = true;

                foreach (Window window in allWindows)
                {
                    System.Drawing.Point windowCenter =
                        new System.Drawing.Point(
                            (int)(window.Left + (window.Width / 2)),
                            (int)(window.Top + (window.Height / 2)));
                    screenHasWindow = screen.Bounds.Contains(windowCenter);

                    // if (window is EditTextWindow)
                    //     isEditWindowOpen = true;
                }

                if (allWindows.Count < 1)
                    screenHasWindow = false;

                if (screenHasWindow == false || openAnyway == true)
                {
                    FullscreenGrab fullscreenGrab = new FullscreenGrab
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Width = 200,
                        Height = 200,
                        EditWindow = editWindow,
                        IsFreeze = setBackgroundImage,
                        WindowState = WindowState.Normal
                    };

                    if (screen.WorkingArea.Left >= 0)
                        fullscreenGrab.Left = screen.WorkingArea.Left;
                    else
                        fullscreenGrab.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width / 2);

                    if (screen.WorkingArea.Top >= 0)
                        fullscreenGrab.Top = screen.WorkingArea.Top;
                    else
                        fullscreenGrab.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height / 2);

                    fullscreenGrab.Show();
                    fullscreenGrab.Activate();
                    allFullscreenGrab.Add(fullscreenGrab);
                }
            }

            if (setBackgroundImage == true)
            {
                foreach (FullscreenGrab fsg in allFullscreenGrab)
                {
                    fsg.SetImageToBackground();
                }
            }
        }

        internal static void CloseAllFullscreenGrabs()
        {
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

            bool isFromEditWindow = false;
            string stringFromOCR = "";

            foreach (Window window in allWindows)
            {
                if (window is FullscreenGrab fsg)
                {
                    if (string.IsNullOrWhiteSpace(fsg.textFromOCR) == false)
                        stringFromOCR = fsg.textFromOCR;

                    if (fsg.EditWindow is not null)
                    {
                        isFromEditWindow = true;
                        if (fsg.EditWindow.WindowState == WindowState.Minimized)
                            fsg.EditWindow.WindowState = WindowState.Normal;
                    }

                    fsg.Close();
                }
            }

            if (Settings.Default.TryInsert == true
                && string.IsNullOrWhiteSpace(stringFromOCR) == false
                && isFromEditWindow == false)
            {
                TryInsertString(stringFromOCR);
            }

            ShouldShutDown();
        }

        internal static void TryInsertString(string stringToInsert)
        {
            string stringToSend = Regex.Replace(stringToInsert, "[+^%~()\\{\\}\\[\\]]", "{$0}");

            try
            {
                SendKeys.SendWait(stringToSend);
            }
            catch (ArgumentException argEx)
            {
                Debug.WriteLine($"Failed to Send Keys: {argEx.Message}");
            }
        }

        internal static void OpenOrActivateWindow<T>() where T : Window, new()
        {
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

            foreach (var window in allWindows)
            {
                if (window is T matchWindow)
                {
                    matchWindow.Activate();
                    return;
                }
            }

            // No Window Found, open a new one
            T newWindow = new T();
            newWindow.Show();
        }

        public static void ShouldShutDown()
        {
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

            bool shouldShutDown = false;

            if (Settings.Default.RunInTheBackground == true)
            {
                if (App.Current is App app)
                {
                    if (app.NumberOfRunningInstances > 1
                        && app.TextGrabIcon == null
                        && allWindows.Count < 1)
                        shouldShutDown = true;
                }
            }
            else
            {
                if (allWindows.Count < 1)
                    shouldShutDown = true;
            }

            if (shouldShutDown == true)
                System.Windows.Application.Current.Shutdown();
        }
    }
}
