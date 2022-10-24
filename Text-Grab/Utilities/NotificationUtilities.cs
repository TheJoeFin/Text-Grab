using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Text;

namespace Text_Grab.Utilities;

internal static class NotificationUtilities
{
    internal static void ShowToast(string copiedText)
    {
        int byteSizeCopiedText = System.Text.Encoding.Unicode.GetByteCount(copiedText);
        // Max payload size is 5000, so I will go under that by a little bit
        int maxArgumentSize = 4900;
        if (byteSizeCopiedText > maxArgumentSize)
        {
            double lengthOfCopiedText = copiedText.Length;
            double reductionRatio = maxArgumentSize / lengthOfCopiedText;
            int newTrimmedLenth = (int)(lengthOfCopiedText * reductionRatio);
            copiedText = copiedText.Substring(0, newTrimmedLenth);
        }

        byte[] plainTextBytes = Encoding.UTF8.GetBytes(copiedText);
        string encodedString = Convert.ToHexString(plainTextBytes);

        new ToastContentBuilder()
            .AddArgument("text", encodedString)
            .AddText("Text Grab")
            .AddText(copiedText)
            .Show();
    }
}
