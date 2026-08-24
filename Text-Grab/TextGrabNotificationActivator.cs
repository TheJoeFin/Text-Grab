using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace Text_Grab;

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
                if (invokedArgs.StartsWith("windowId=", StringComparison.Ordinal)
                    && Guid.TryParse(invokedArgs["windowId=".Length..], out Guid windowId))
                {
                    // A transcription-complete toast: re-activate the specific window that received
                    // the transcript rather than opening a new one (see NotificationUtilities).
                    foreach (Window window in System.Windows.Application.Current.Windows)
                    {
                        if (window is EditTextWindow etw && etw.WindowId == windowId)
                        {
                            if (etw.WindowState == WindowState.Minimized)
                                etw.WindowState = WindowState.Normal;
                            etw.Activate();
                            return;
                        }
                    }

                    // The window was already closed; nothing to re-activate.
                    return;
                }

                // Perform a normal launch
                EditTextWindow mtw = new(invokedArgs);
                mtw.Show();
                return;
            }
        });
    }
}
