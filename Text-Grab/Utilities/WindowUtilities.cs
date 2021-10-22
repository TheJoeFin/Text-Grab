using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
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
            Rect defaultSize = new Rect(50, 50, 600, 400);
            if (passedWindow is EditTextWindow)
                storedPostionString = Properties.Settings.Default.EditTextWindowSizeAndPosition;

            if (passedWindow is GrabFrame)
                storedPostionString = Properties.Settings.Default.GrabFrameWindowSizeAndPosition;

            List<string> storedPostion = new(storedPostionString.Split(','));

            bool isStoredRectWithinScreen = false;

            if (storedPostion != null
                && storedPostion.Count == 4)
            {
                Rectangle storedSize = new Rectangle(int.Parse(storedPostion[0]), int.Parse(storedPostion[1]), int.Parse(storedPostion[2]), int.Parse(storedPostion[3]));
                Screen[] allScreens = Screen.AllScreens;
                WindowCollection allWindows = System.Windows.Application.Current.Windows;

                foreach (Screen screen in allScreens)
                {
                    if (screen.WorkingArea.IntersectsWith(storedSize))
                        isStoredRectWithinScreen = true;
                }

                if (isStoredRectWithinScreen == true)
                {
                    passedWindow.Left = storedSize.X;
                    passedWindow.Top = storedSize.Y;
                    passedWindow.Width = storedSize.Width;
                    passedWindow.Height = storedSize.Height;

                    return;
                }
            }

            passedWindow.Left = defaultSize.X;
            passedWindow.Top = defaultSize.Y;
            passedWindow.Width = defaultSize.Width;
            passedWindow.Height = defaultSize.Height;
        }

        public static void LaunchFullScreenGrab(bool openAnyway = false)
        {
            Screen[] allScreens = Screen.AllScreens;
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

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
                        IsFromEditWindow = openAnyway,
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
                }
            }
        }

        internal static void CloseAllFullscreenGrabs()
        {
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

            foreach (Window window in allWindows)
            {
                if (window is FullscreenGrab fsg)
                    fsg.Close();
                if (window is EditTextWindow etw)
                {
                    if (etw.WindowState == WindowState.Minimized)
                        etw.WindowState = WindowState.Normal;
                }

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
    }
}
