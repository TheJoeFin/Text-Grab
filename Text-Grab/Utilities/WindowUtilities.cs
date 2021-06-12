using System;
using System.Collections.Generic;
using System.Text;
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
                if (window is ManipulateTextWindow mtw)
                {
                    mtw.AddThisText(textToAdd);
                }
            }
        }

        public static void NormalLaunch(bool openAnyway = false)
        {
            // base.OnActivated(e);

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
                    FullscreenGrab mw = new FullscreenGrab
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Width = 200,
                        Height = 200,
                        IsFromEditWindow = openAnyway,
                        WindowState = WindowState.Normal
                    };

                    if (screen.WorkingArea.Left >= 0)
                        mw.Left = screen.WorkingArea.Left;
                    else
                        mw.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width / 2);

                    if (screen.WorkingArea.Top >= 0)
                        mw.Top = screen.WorkingArea.Top;
                    else
                        mw.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height / 2);

                    mw.Show();
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
            }
        }
    }
}
