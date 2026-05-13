using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Dock.App.Services;

public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(UserPaths.AppName, $"\"{GetExecutablePath()}\"");
        }
        else
        {
            key.DeleteValue(UserPaths.AppName, throwOnMissingValue: false);
        }
    }

    private static string GetExecutablePath()
    {
        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            var appHostPath = Path.ChangeExtension(assemblyPath, ".exe");
            if (File.Exists(appHostPath))
            {
                return appHostPath;
            }
        }

        return System.Environment.ProcessPath ?? "";
    }
}
