using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dock.App.Models;

namespace Dock.App.Services;

public sealed class WindowMinimizeMonitor : IDisposable
{
    private const uint EventSystemMinimizeStart = 0x0016;
    private const uint EventSystemMinimizeEnd = 0x0017;
    private const uint EventObjectDestroy = 0x8001;
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;
    private const int SwRestore = 9;

    private readonly Action<DockItem> _windowMinimized;
    private readonly Action<long> _windowRestoredOrDestroyed;
    private readonly WinEventDelegate _minimizeDelegate;
    private readonly WinEventDelegate _destroyDelegate;
    private IntPtr _minimizeHook;
    private IntPtr _destroyHook;

    public WindowMinimizeMonitor(Action<DockItem> windowMinimized, Action<long> windowRestoredOrDestroyed)
    {
        _windowMinimized = windowMinimized;
        _windowRestoredOrDestroyed = windowRestoredOrDestroyed;
        _minimizeDelegate = HandleMinimizeEvent;
        _destroyDelegate = HandleDestroyEvent;
    }

    public void Start()
    {
        if (_minimizeHook == IntPtr.Zero)
        {
            _minimizeHook = SetWinEventHook(
                EventSystemMinimizeStart,
                EventSystemMinimizeEnd,
                IntPtr.Zero,
                _minimizeDelegate,
                0,
                0,
                WineventOutOfContext | WineventSkipOwnProcess);
        }

        if (_destroyHook == IntPtr.Zero)
        {
            _destroyHook = SetWinEventHook(
                EventObjectDestroy,
                EventObjectDestroy,
                IntPtr.Zero,
                _destroyDelegate,
                0,
                0,
                WineventOutOfContext | WineventSkipOwnProcess);
        }
    }

    public void Stop()
    {
        if (_minimizeHook != IntPtr.Zero)
        {
            UnhookWinEvent(_minimizeHook);
            _minimizeHook = IntPtr.Zero;
        }

        if (_destroyHook != IntPtr.Zero)
        {
            UnhookWinEvent(_destroyHook);
            _destroyHook = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    public static bool RestoreWindow(long nativeWindowHandle)
    {
        var hwnd = new IntPtr(nativeWindowHandle);
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
        {
            return false;
        }

        ShowWindow(hwnd, SwRestore);
        SetForegroundWindow(hwnd);
        return true;
    }

    private void HandleMinimizeEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero || idObject != 0 || idChild != 0)
        {
            return;
        }

        if (eventType == EventSystemMinimizeStart)
        {
            var item = CreateDockItem(hwnd);
            if (item is not null)
            {
                _windowMinimized(item);
            }
        }
        else if (eventType == EventSystemMinimizeEnd)
        {
            _windowRestoredOrDestroyed(hwnd.ToInt64());
        }
    }

    private void HandleDestroyEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (hwnd != IntPtr.Zero && idObject == 0 && idChild == 0)
        {
            _windowRestoredOrDestroyed(hwnd.ToInt64());
        }
    }

    private static DockItem? CreateDockItem(IntPtr hwnd)
    {
        if (!IsWindow(hwnd) || !IsWindowVisible(hwnd))
        {
            return null;
        }

        GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == Environment.ProcessId)
        {
            return null;
        }

        var title = GetWindowTitle(hwnd);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var executablePath = GetProcessPath((int)processId) ?? "";
        return new DockItem
        {
            Id = $"window-{hwnd.ToInt64()}",
            Kind = DockItemKind.Window,
            DisplayName = title,
            TargetPath = executablePath,
            NativeWindowHandle = hwnd.ToInt64(),
            ProcessId = (int)processId,
            IsRuntime = true
        };
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return "";
        }

        var builder = new StringBuilder(length + 1);
        GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string? GetProcessPath(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
