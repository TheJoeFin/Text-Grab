using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Windows.Input;
using Text_Grab.Properties;
using Windows.UI.Notifications;

namespace Text_Grab.Utilities
{
    internal static class NotificationUtilities
    {
        internal static void ShowToast(string copiedText)
        {
            string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;

            new ToastContentBuilder()
                .AddArgument("inputLang", inputLang)
                .AddArgument("text", copiedText)
                .AddText("Text Grab")
                .AddText(copiedText)
                .Show();
        }
    }
}
