using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace PHF.Client.Desktop.Utils;

public static class WindowsStartupHelper
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "PunnyaHeartDesk";

    public static bool IsStartupEnabled()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            var value = key?.GetValue(AppName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsStartupHelper] Error checking startup status: {ex.Message}");
            return false;
        }
    }

    public static bool SetStartup(bool enable)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key == null)
                return false;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                    return true;
                }
                return false;
            }
            else
            {
                key.DeleteValue(AppName, false);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsStartupHelper] Error setting startup to {enable}: {ex.Message}");
            return false;
        }
    }
}
