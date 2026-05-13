using System.Diagnostics;
using Dock.App.Models;

namespace Dock.App.Services;

public static class DockLauncher
{
    public static bool Open(DockItem item)
    {
        if (item.Kind == DockItemKind.WindowsButton)
        {
            WindowsButtonService.OpenStartMenu();
            return true;
        }

        if (item.Kind == DockItemKind.RecycleBin)
        {
            RecycleBinService.Open();
            return true;
        }

        if (item.Kind == DockItemKind.Window)
        {
            return WindowMinimizeMonitor.RestoreWindow(item.NativeWindowHandle);
        }

        if (string.IsNullOrWhiteSpace(item.TargetPath))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = item.TargetPath,
            UseShellExecute = true
        };

        Process.Start(startInfo);
        return true;
    }
}
