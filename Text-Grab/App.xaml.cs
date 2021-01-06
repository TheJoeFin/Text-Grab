using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

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
                        if (screen.Bounds.X == window.Left && screen.Bounds.Y == window.Top)
                            screenHasWindow = false;
                    }

                    if(screenHasWindow)
                    {
                        MainWindow mw = new MainWindow();
                        mw.Left = screen.Bounds.X;
                        mw.Top = screen.Bounds.Y;

                        mw.Show();
                    }
                }
            }

        }
    }
}
