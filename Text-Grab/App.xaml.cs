using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Text_Grab.Properties;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            var allScreens = Screen.AllScreens;
            var allWindows = System.Windows.Application.Current.Windows;

            if (allScreens.Count() > 1)
            {
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

                    if(screenHasWindow == false)
                    {
                        MainWindow mw = new MainWindow();
                        mw.WindowStartupLocation = WindowStartupLocation.Manual;
                        mw.Width = 200;
                        mw.Height = 200;

                        mw.WindowState = WindowState.Normal;

                        if(screen.WorkingArea.Left > 0)
                            mw.Left = screen.WorkingArea.Left;
                        else
                            mw.Left = screen.WorkingArea.Left / 2;

                        if(screen.WorkingArea.Top > 0)
                            mw.Top = screen.WorkingArea.Top;
                        else
                            mw.Top = screen.WorkingArea.Top / 2;

                        mw.Show();
                    }
                }
            }

            
            if(Settings.Default.FirstRun)
            {
                FirstRunWindow frw = new FirstRunWindow();
                frw.Show();

                Settings.Default.FirstRun = false;
                Settings.Default.Save();
            }

        }
    }
}
