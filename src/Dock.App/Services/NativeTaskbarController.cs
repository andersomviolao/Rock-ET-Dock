using System;
using System.Runtime.InteropServices;

namespace Dock.App.Services;

public sealed class NativeTaskbarController
{
    private const int SwHide = 0;
    private const int SwShow = 5;
    private bool _hiddenByRockEtDock;

    public void Apply(bool hide)
    {
        if (hide)
        {
            Hide();
        }
        else
        {
            Restore();
        }
    }

    public void Hide()
    {
        SetTaskbarsVisible(visible: false);
        _hiddenByRockEtDock = true;
    }

    public void Restore()
    {
        if (!_hiddenByRockEtDock)
        {
            return;
        }

        SetTaskbarsVisible(visible: true);
        _hiddenByRockEtDock = false;
    }

    private static void SetTaskbarsVisible(bool visible)
    {
        var command = visible ? SwShow : SwHide;

        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != IntPtr.Zero)
        {
            ShowWindow(primary, command);
        }

        var previous = IntPtr.Zero;
        while (true)
        {
            var secondary = FindWindowEx(IntPtr.Zero, previous, "Shell_SecondaryTrayWnd", null);
            if (secondary == IntPtr.Zero)
            {
                break;
            }

            ShowWindow(secondary, command);
            previous = secondary;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
