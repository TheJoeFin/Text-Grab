using System;
using System.Linq;
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

            if(allScreens.Count() > 1)
            {
                foreach (Screen screen in allScreens)
                {
                    if (screen.Bounds.X == 0 && screen.Bounds.Y == 0)
                        continue;

                    MainWindow mw = new MainWindow();
                    mw.Left = screen.Bounds.X;
                    mw.Top = screen.Bounds.Y;

                    mw.Show();
                }
            }

        }
    }
}
