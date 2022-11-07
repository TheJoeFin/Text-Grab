using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Text;

namespace Text_Grab.Utilities;

internal static class NotificationUtilities
{
    internal static void ShowToast(string copiedText)
    {
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(copiedText);

        // changed to using base64
        // the padding '=' will be encoded as '%3D' in the toast XML, so remove them
        string encodedString = Convert.ToBase64String(plainTextBytes).TrimEnd('='); 

        // truncate toast body text first, if it is too long
        string toastBody;
        if (copiedText.Length > 150)
            toastBody = copiedText.Substring(0, 150) + "...";
        else
            toastBody = copiedText;

        // build the toast XML
        var toast = new ToastContentBuilder()
            .AddArgument("text", encodedString)
            .AddText("Text Grab")
            .AddText(toastBody);

        int toastSizeInBytes = Encoding.UTF8.GetByteCount(toast.Content.GetContent());
        if (toastSizeInBytes > 5000) // maximum size 5000 bytes
        {
            // the XML is still too large, the copied text itself will have to be truncated and some data will be lost

            int bytesFree = 5000 - (toastSizeInBytes - encodedString.Length); // max length for encodedString

            // 4 chars in a base64 string = 3 bytes, so convert it to max length for plainTextBytes
            int maxTextBytes = bytesFree / 4 * 3; // max length for plainTextBytes

            // as we removed the padding '='s, maybe we can fit in 2 or 3 more base64 chars, which is 1 or 2 text bytes
            if (bytesFree % 4 >= 2)
                maxTextBytes += bytesFree % 4 - 1;

            // convert only as much as bytesFree bytes

            plainTextBytes = new byte[maxTextBytes];
            int bytesUsed = 0;

            // Encoder.Convert() won't fail when the byte array is smaller than the size needed to hold the source string,
            // it will just convert as many characters as possible.
            Encoding.UTF8.GetEncoder().Convert(copiedText.AsSpan(), plainTextBytes.AsSpan(), true, out _, out bytesUsed, out _);

            encodedString = Convert.ToBase64String(plainTextBytes, 0, bytesUsed).TrimEnd('=');

            // rebuild the toast XML
            toast = new ToastContentBuilder()
                .AddArgument("text", encodedString)
                .AddText("Text Grab")
                .AddText(toastBody);
        }

        toast.Show();
    }
}
