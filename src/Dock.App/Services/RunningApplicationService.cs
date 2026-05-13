using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Dock.App.Models;

namespace Dock.App.Services;

public static class RunningApplicationService
{
    private const int SwRestore = 9;

    public static ISet<string> GetRunningExecutablePaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                var path = TryGetProcessPath(process);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    paths.Add(NormalizePath(path));
                }
            }
        }

        return paths;
    }

    public static bool IsItemRunning(DockItem item, ISet<string> runningExecutablePaths)
    {
        return TryGetExecutablePath(item, out var executablePath) &&
               runningExecutablePaths.Contains(NormalizePath(executablePath));
    }

    public static bool TryActivateExisting(DockItem item)
    {
        if (!TryGetExecutablePath(item, out var executablePath))
        {
            return false;
        }

        var processIds = GetProcessIdsForExecutable(executablePath);
        if (processIds.Count == 0)
        {
            return false;
        }

        var activated = false;
        EnumWindows((handle, _) =>
        {
            if (activated ||
                !IsWindowVisible(handle) ||
                GetWindowTextLength(handle) <= 0)
            {
                return true;
            }

            GetWindowThreadProcessId(handle, out var processId);
            if (!processIds.Contains((int)processId))
            {
                return true;
            }

            ShowWindow(handle, SwRestore);
            SetForegroundWindow(handle);
            activated = true;
            return false;
        }, IntPtr.Zero);

        return activated;
    }

    public static bool TryGetExecutablePath(DockItem item, out string executablePath)
    {
        executablePath = "";
        if (item.Kind is DockItemKind.WindowsButton or DockItemKind.RecycleBin or DockItemKind.Window or
            DockItemKind.AnimatedGif or DockItemKind.DropPlaceholder or DockItemKind.Separator or
            DockItemKind.DockSettings or DockItemKind.Quit)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.TargetPath))
        {
            return false;
        }

        var targetPath = ShellShortcutService.ResolveLaunchTarget(item.TargetPath);
        if (!Path.GetExtension(targetPath).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(targetPath))
        {
            return false;
        }

        executablePath = NormalizePath(targetPath);
        return true;
    }

    private static HashSet<int> GetProcessIdsForExecutable(string executablePath)
    {
        var normalizedExecutablePath = NormalizePath(executablePath);
        var processIds = new HashSet<int>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                var processPath = TryGetProcessPath(process);
                if (processPath is not null &&
                    string.Equals(NormalizePath(processPath), normalizedExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    processIds.Add(process.Id);
                }
            }
        }

        return processIds;
    }

    private static string? TryGetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
}
