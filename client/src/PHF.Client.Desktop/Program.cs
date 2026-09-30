using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PHF.Client.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Debug.WriteLine($"[CRITICAL UNHANDLED EXCEPTION] {ex?.Message}\n{ex?.StackTrace}");
            try
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PHF_Workstation");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "crash.log"), $"[{DateTime.UtcNow:O}] UNHANDLED: {ex}\n\n");
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Debug.WriteLine($"[UNOBSERVED TASK EXCEPTION] {e.Exception?.Message}");
            e.SetObserved();
            try
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PHF_Workstation");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "crash.log"), $"[{DateTime.UtcNow:O}] UNOBSERVED: {e.Exception}\n\n");
            }
            catch { }
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
