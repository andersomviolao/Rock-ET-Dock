using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Dock.App.Services;

public static class ShellShortcutService
{
    public static void CreateShortcut(string shortcutPath, string targetPath)
    {
        WithShortcut(shortcutPath, create: true, shortcut =>
        {
            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [targetPath]);
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, [GetWorkingDirectory(targetPath)]);
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        });
    }

    public static string? ResolveShortcutTarget(string shortcutPath)
    {
        if (!File.Exists(shortcutPath) ||
            !Path.GetExtension(shortcutPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? targetPath = null;
        WithShortcut(shortcutPath, create: false, shortcut =>
        {
            targetPath = shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null) as string;
        });

        return string.IsNullOrWhiteSpace(targetPath) ? null : targetPath;
    }

    public static string ResolveLaunchTarget(string path)
    {
        if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return ResolveShortcutTarget(path) ?? path;
            }
            catch
            {
                return path;
            }
        }

        return path;
    }

    private static void WithShortcut(string shortcutPath, bool create, Action<object> action)
    {
        Type? shellType = null;
        object? shell = null;
        object? shortcut = null;

        try
        {
            shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true);
            shell = Activator.CreateInstance(shellType!);
            shortcut = shellType!.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath]);

            if (!create && shortcut is null)
            {
                return;
            }

            action(shortcut!);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    private static string GetWorkingDirectory(string targetPath)
    {
        if (Directory.Exists(targetPath))
        {
            return targetPath;
        }

        return Path.GetDirectoryName(targetPath) ?? UserPaths.UserProfile;
    }
}
