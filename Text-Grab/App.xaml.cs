using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Windows.System.UserProfile;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public static List<string> InstalledLanguages => GlobalizationPreferences.Languages.ToList();

        void appStartup(object sender, StartupEventArgs e)
        {
            // Register COM server and activator type
            DesktopNotificationManagerCompat.RegisterActivator<TextGrabNotificationActivator>();

            for (int i = 0; i != e.Args.Length; ++i)
            {
                if (e.Args[i] == "Settings")
                {
                    SettingsWindow sw = new SettingsWindow();
                    sw.Show();
                }
                if (e.Args[i] == "GrabFrame")
                {
                    GrabFrame gf = new GrabFrame();
                    gf.Show();
                }
                if (e.Args[i] == "Fullscreen")
                {
                    WindowUtilities.NormalLaunch();
                }
                if (e.Args[i] == "EditText")
                {
                    ManipulateTextWindow manipulateTextWindow = new ManipulateTextWindow();
                    manipulateTextWindow.Show();
                }
            }

            if(e.Args.Length == 0 && Settings.Default.FirstRun == false)
            {
                switch (Settings.Default.DefaultLaunch)
                {
                    case "Fullscreen":
                        WindowUtilities.NormalLaunch();
                        break;
                    case "GrabFrame":
                        GrabFrame gf = new GrabFrame();
                        gf.Show();
                        break;
                    case "EditText":
                        ManipulateTextWindow manipulateTextWindow = new ManipulateTextWindow();
                        manipulateTextWindow.Show();
                        break;
                    default:
                        WindowUtilities.NormalLaunch();
                        break;
                }
            }

            // if (true)
            if (Settings.Default.FirstRun)
            {
                FirstRunWindow frw = new FirstRunWindow();
                frw.Show();

                Settings.Default.FirstRun = false;
                Settings.Default.Save();
            }
        }

        
    }
}
