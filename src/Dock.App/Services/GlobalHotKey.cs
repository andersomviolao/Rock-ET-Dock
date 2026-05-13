using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Dock.App.Services;

public sealed class GlobalHotKey : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWindows = 0x0008;

    private static int s_nextId = 0x4A00;

    private readonly HwndSource _source;
    private readonly int _id;
    private readonly Action _callback;
    private bool _disposed;

    private GlobalHotKey(HwndSource source, int id, Action callback)
    {
        _source = source;
        _id = id;
        _callback = callback;
        _source.AddHook(WndProc);
    }

    public static GlobalHotKey? Register(IntPtr windowHandle, ModifierKeys modifiers, Key key, Action callback)
    {
        if (windowHandle == IntPtr.Zero ||
            HwndSource.FromHwnd(windowHandle) is not { } source)
        {
            return null;
        }

        var id = System.Threading.Interlocked.Increment(ref s_nextId);
        var nativeModifiers = ToNativeModifiers(modifiers);
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

        if (!RegisterHotKey(windowHandle, id, nativeModifiers, virtualKey))
        {
            return null;
        }

        return new GlobalHotKey(source, id, callback);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _source.RemoveHook(WndProc);
        UnregisterHotKey(_source.Handle, _id);
        _disposed = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotKey && wParam.ToInt32() == _id)
        {
            handled = true;
            _callback();
        }

        return IntPtr.Zero;
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        var native = 0u;
        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            native |= ModAlt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            native |= ModControl;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            native |= ModShift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            native |= ModWindows;
        }

        return native;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
