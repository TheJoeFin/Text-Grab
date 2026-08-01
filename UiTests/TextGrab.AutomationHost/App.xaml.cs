using System.Windows;

namespace TextGrab.AutomationHost;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        FixtureOptions options = FixtureOptions.Parse(e.Args);
        new MainWindow(options).Show();
    }
}
