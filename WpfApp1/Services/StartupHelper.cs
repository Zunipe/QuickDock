using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace WpfApp1.Services;

public static class StartupHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "QuickDock";

    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) is string;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetStartup(bool enable)
    {
        if (enable && !IsRunningAsAdmin())
        {
            return RequestAdminAndSetStartup(true);
        }

        return WriteStartupRegistry(enable);
    }

    public static bool HandleStartupCommandLine(string[] args)
    {
        if (args.Contains("--enable-startup"))
        {
            WriteStartupRegistry(true);
            return true;
        }

        if (args.Contains("--disable-startup"))
        {
            WriteStartupRegistry(false);
            return true;
        }

        return false;
    }

    private static bool RequestAdminAndSetStartup(bool enable)
    {
        try
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                return false;
            }

            var arg = enable ? "--enable-startup" : "--disable-startup";
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arg,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool WriteStartupRegistry(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                {
                    return false;
                }

                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
