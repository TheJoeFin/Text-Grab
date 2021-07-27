using System;
using System.Runtime.InteropServices;

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
            System.Windows.Application.Current.Dispatcher.Invoke(delegate
            {
                // Tapping on the top-level header launches with empty args
                if (invokedArgs.Length != 0)
                {
                    // Perform a normal launch
                    EditTextWindow mtw = new EditTextWindow(invokedArgs);
                    mtw.Show();
                    return;
                }
            });
        }
    }
}
