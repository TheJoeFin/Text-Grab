using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using static Microsoft.Toolkit.Uwp.Notifications.Internal.InternalNotificationActivator;

namespace Text_Grab
{
    // The GUID CLSID must be unique to your app. Create a new GUID if copying this code.
    [ClassInterface(ClassInterfaceType.None)]
    [ComSourceInterfaces(typeof(INotificationActivationCallback))]
    [Guid("215d64d2-031c-33c7-96e3-61794cd1ee61"), ComVisible(true)]
    public class TextGrabNotificationActivator : NotificationActivator
    {
        public override void OnActivated(string invokedArgs, NotificationUserInput userInput, string appUserModelId)
        {
            ManipulateTextWindow mtw = new ManipulateTextWindow(invokedArgs);
            mtw.Show();
        }
    }
}
