using System.Collections.Generic;
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

        public static void LaunchFullScreenGrab(bool openAnyway = false, bool setBackgroundImage = false)
        {
            Screen[] allScreens = Screen.AllScreens;
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

            List<FullscreenGrab> allFullscreenGrab = new();

            foreach (Screen screen in allScreens)
            {
                bool screenHasWindow = true;
                bool isEditWindowOpen = false;

                foreach (Window window in allWindows)
                {
                    System.Drawing.Point windowCenter =
                        new System.Drawing.Point(
                            (int)(window.Left + (window.Width / 2)),
                            (int)(window.Top + (window.Height / 2)));
                    screenHasWindow = screen.Bounds.Contains(windowCenter);

                    if (window is EditTextWindow)
                        isEditWindowOpen = true;
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
                        IsFromEditWindow = isEditWindowOpen,
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

            ShouldShutDown();
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
            if (allWindows.Count <= 1
                && Settings.Default.RunInTheBackground == false)
                System.Windows.Application.Current.Shutdown();
        }
    }
}
