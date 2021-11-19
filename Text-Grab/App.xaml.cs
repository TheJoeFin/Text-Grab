using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        void appStartup(object sender, StartupEventArgs e)
        {
            // Register COM server and activator type
            bool handledArgument = false;

            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                string argsInvoked = toastArgs.Argument;
                // Need to dispatch to UI thread if performing UI operations
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (String.IsNullOrWhiteSpace(argsInvoked) == false)
                    {
                        EditTextWindow mtw = new EditTextWindow(argsInvoked);
                        mtw.Show();
                        handledArgument = true;
                    }
                }));
            };

            SetupNotifyIcon();

            Current.DispatcherUnhandledException += CurrentDispatcherUnhandledException;

            for (int i = 0; i != e.Args.Length && !handledArgument; ++i)
            {
                Debug.WriteLine($"ARG {i}:{e.Args[i]}");
                if (e.Args[i].Contains("ToastActivated"))
                {
                    Debug.WriteLine("Launched from toast");
                    handledArgument = true;
                }
                else if (e.Args[i] == "Settings")
                {
                    SettingsWindow sw = new SettingsWindow();
                    sw.Show();
                    handledArgument = true;
                }
                else if (e.Args[i] == "GrabFrame")
                {
                    GrabFrame gf = new GrabFrame();
                    gf.Show();
                    handledArgument = true;
                }
                else if (e.Args[i] == "Fullscreen")
                {
                    WindowUtilities.LaunchFullScreenGrab();
                    handledArgument = true;
                }
                else if (e.Args[i] == "EditText")
                {
                    EditTextWindow manipulateTextWindow = new EditTextWindow();
                    manipulateTextWindow.Show();
                    handledArgument = true;
                }
                else if (File.Exists(e.Args[i]))
                {
                    EditTextWindow manipulateTextWindow = new EditTextWindow();
                    manipulateTextWindow.OpenThisPath(e.Args[i]);
                    manipulateTextWindow.Show();
                    handledArgument = true;
                }
            }

            if (!handledArgument)
            {
                if (Settings.Default.FirstRun)
                {
                    FirstRunWindow frw = new FirstRunWindow();
                    frw.Show();

                    Settings.Default.FirstRun = false;
                    Settings.Default.Save();
                }
                else
                {
                    switch (Settings.Default.DefaultLaunch)
                    {
                        case "Fullscreen":
                            WindowUtilities.LaunchFullScreenGrab();
                            break;
                        case "GrabFrame":
                            GrabFrame gf = new GrabFrame();
                            gf.Show();
                            break;
                        case "EditText":
                            EditTextWindow manipulateTextWindow = new EditTextWindow();
                            manipulateTextWindow.Show();
                            break;
                        default:
                            EditTextWindow editTextWindow = new EditTextWindow();
                            editTextWindow.Show();
                            break;
                    }
                }
            }
        }

        private void SetupNotifyIcon()
        {
            NotifyIcon icon = new NotifyIcon();
            icon.Text = "Text Grab";
            icon.Icon = new System.Drawing.Icon(System.Windows.Application.GetResourceStream(new Uri("/t_ICON2.ico", UriKind.Relative)).Stream);
            icon.Visible = true;

            ContextMenuStrip? contextMenu = new();

            ToolStripMenuItem? settingsItem = new("&Settings");
            settingsItem.Click += (s, e) => { SettingsWindow sw = new(); sw.Show(); };
            ToolStripMenuItem? fullscreenGrabItem = new("&Fullscreen Grab");
            fullscreenGrabItem.Click += (s, e) => { WindowUtilities.LaunchFullScreenGrab(true); };
            ToolStripMenuItem? grabFrameItem = new("&Grab Frame");
            grabFrameItem.Click += (s, e) => { GrabFrame gf = new(); gf.Show(); };
            ToolStripMenuItem? editTextWindowItem = new("&Edit Text Window");
            editTextWindowItem.Click += (s, e) => { EditTextWindow etw = new(); etw.Show(); };

            ToolStripMenuItem? exitItem = new("&Close");
            exitItem.Click += (s, e) => { icon.Dispose(); System.Windows.Application.Current.Shutdown(); };

            contextMenu.Items.AddRange(
                new ToolStripMenuItem[] {
                    settingsItem,
                    fullscreenGrabItem,
                    grabFrameItem,
                    editTextWindowItem,
                    exitItem
                }
            );
            icon.ContextMenuStrip = contextMenu;

            icon.MouseClick += notifyIcon_Click;

            HotKeyManager.RegisterHotKey(Keys.F, KeyModifiers.Windows | KeyModifiers.Shift);
            HotKeyManager.RegisterHotKey(Keys.E, KeyModifiers.Windows | KeyModifiers.Shift);
            HotKeyManager.RegisterHotKey(Keys.G, KeyModifiers.Windows | KeyModifiers.Shift);
            HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);
        }

        static void HotKeyManager_HotKeyPressed(object? sender, HotKeyEventArgs e)
        {
            switch (e.Key)
            {
                case Keys.E:
                    System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { EditTextWindow etw = new(); etw.Show(); }));
                    break;
                case Keys.F:
                    System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { WindowUtilities.LaunchFullScreenGrab(true); }));
                    break;
                case Keys.G:
                    System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { GrabFrame gf = new(); gf.Show(); }));
                    break;
                default:
                    break;
            }
        }

        private void notifyIcon_Click(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            WindowUtilities.LaunchFullScreenGrab(true);
        }

        private void CurrentDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // unhandled exceptions thrown from UI thread
            Debug.WriteLine($"Unhandled exception: {e.Exception}");
            e.Handled = true;
            Current.Shutdown();
        }
    }
}
