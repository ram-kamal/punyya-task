using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace PHF.Client.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            File.AppendAllText("wpf_error.log", $"[{DateTime.UtcNow:O}] Dispatcher Exception: {args.Exception}\n");
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            File.AppendAllText("wpf_error.log", $"[{DateTime.UtcNow:O}] AppDomain Exception: {args.ExceptionObject}\n");
        };
    }
}

