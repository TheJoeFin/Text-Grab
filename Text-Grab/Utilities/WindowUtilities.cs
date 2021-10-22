using System.Collections.Generic;
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
            string storedPostionString = "50,50,400,600";
            if (passedWindow is EditTextWindow)
                storedPostionString = Properties.Settings.Default.EditTextWindowSizeAndPosition;

            if (passedWindow is GrabFrame)
                storedPostionString = Properties.Settings.Default.GrabFrameWindowSizeAndPosition;

            List<string> storedPostion = new(storedPostionString.Split(','));

            if (storedPostion != null
                && storedPostion.Count == 4)
            {
                passedWindow.Left = double.Parse(storedPostion[0]);
                passedWindow.Top = double.Parse(storedPostion[1]);
                passedWindow.Width = double.Parse(storedPostion[2]);
                passedWindow.Height = double.Parse(storedPostion[3]);
            }
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
