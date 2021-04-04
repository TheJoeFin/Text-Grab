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
            // Construct the content
            ToastContent content = new ToastContentBuilder()
                .AddToastActivationInfo(copiedText + ',' + inputLang, ToastActivationType.Foreground)
                .SetBackgroundActivation()
                .AddText(copiedText)
                .GetToastContent();
            content.Duration = ToastDuration.Short;

            // Create the toast notification
            var toastNotif = new ToastNotification(content.GetXml());

            // And send the notification
            try
            {
                ToastNotificationManager.CreateToastNotifier().Show(toastNotif);
            }
            catch (Exception)
            {
                Settings.Default.ShowToast = false;
                Settings.Default.Save();
            }
        }
    }
}
