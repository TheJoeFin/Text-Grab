using System.Windows;
using System.Windows.Forms;
using Text_Grab.Properties;
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
            DesktopNotificationManagerCompat.RegisterActivator<TextGrabNotificationActivator>();

            if (e.Args != null && e.Args.Length > 0 && e.Args[0] == "-ToastActivated")
            {
                // ManipulateTextWindow mtw = new ManipulateTextWindow(e.Args[1]);
                // mtw.Show();
            }
            
            
            for (int i = 0; i != e.Args.Length; ++i)
            {
                if (e.Args[i] == "Settings")
                {
                    SettingsWindow sw = new SettingsWindow();
                    sw.Show();
                }
                if(e.Args[i] == "GrabFrame")
                {
                    GrabFrame gf = new GrabFrame();
                    gf.Show();
                }
            }

            if(e.Args.Length == 0)
            {
                GrabFrame gf = new GrabFrame();
                gf.Show();
            }
                // NormalLaunch();
        }
        
        protected void NormalLaunch()
        {
            // base.OnActivated(e);

            var allScreens = Screen.AllScreens;
            var allWindows = System.Windows.Application.Current.Windows;

            foreach (Screen screen in allScreens)
            {
                bool screenHasWindow = true;

                foreach (Window window in allWindows)
                {
                    System.Drawing.Point windowCenter = 
                        new System.Drawing.Point(
                            (int)(window.Left + (window.Width / 2)), 
                            (int)(window.Top + (window.Height / 2)));
                    screenHasWindow = screen.Bounds.Contains(windowCenter);
                }

                if (allWindows.Count < 1)
                    screenHasWindow = false;

                if(screenHasWindow == false)
                {
                    MainWindow mw = new MainWindow();
                    mw.WindowStartupLocation = WindowStartupLocation.Manual;
                    mw.Width = 200;
                    mw.Height = 200;

                    mw.WindowState = WindowState.Normal;

                    if(screen.WorkingArea.Left >= 0)
                        mw.Left = screen.WorkingArea.Left;
                    else
                        mw.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width / 2);

                    if (screen.WorkingArea.Top >= 0)
                        mw.Top = screen.WorkingArea.Top;
                    else
                        mw.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height / 2);

                    mw.Show();
                }
            }

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
