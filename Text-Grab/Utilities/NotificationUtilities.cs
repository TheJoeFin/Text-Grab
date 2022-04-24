using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Text;

namespace Text_Grab.Utilities;

internal static class NotificationUtilities
{
    internal static void ShowToast(string copiedText)
    {
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(copiedText);
        string encodedString = Convert.ToHexString(plainTextBytes);

        new ToastContentBuilder()
            .AddArgument("text", encodedString)
            .AddText("Text Grab")
            .AddText(copiedText)
            .Show();
    }
}
