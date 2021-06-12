using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Text_Grab.Utilities
{
    public static class AddTextToTextEditWindow
    {
        public static void AddTextToOpenWindow(string textToAdd)
        {
            WindowCollection allWindows = Application.Current.Windows;

            foreach (Window window in allWindows)
            {
                if (window is ManipulateTextWindow mtw)
                {
                    mtw.AddThisText(textToAdd);
                }
            }
        }
    }
}
