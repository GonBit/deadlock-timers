using System.Configuration;
using System.Data;
using System.Windows;
using DeadlockTimers.App.Interop;

namespace DeadlockTimers.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConsoleCloseWatcher.Start();
    }
}

