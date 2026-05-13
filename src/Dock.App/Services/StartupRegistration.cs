using System.IO;
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
        return System.Environment.ProcessPath ??
               Path.Combine(System.AppContext.BaseDirectory, $"{UserPaths.AppName}.exe");
    }
}
