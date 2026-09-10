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
            try
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PHFWorkManagement", "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "wpf_error.log"), $"[{DateTime.UtcNow:O}] Dispatcher Exception: {args.Exception}\n");
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PHFWorkManagement", "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "wpf_error.log"), $"[{DateTime.UtcNow:O}] AppDomain Exception: {args.ExceptionObject}\n");
            }
            catch { }
        };
    }
}

