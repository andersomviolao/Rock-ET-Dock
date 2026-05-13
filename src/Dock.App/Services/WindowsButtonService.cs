using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace Dock.App.Services;

public static class WindowsButtonService
{
    private const ushort VkLWin = 0x5B;
    private const ushort VkX = 0x58;
    private const ushort VkEscape = 0x1B;
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventFExtendedKey = 0x0001;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint MapvkVkToVsc = 0;
    private const uint MouseEventFRightDown = 0x0008;
    private const uint MouseEventFRightUp = 0x0010;

    public static void OpenStartMenu()
    {
        SendKeyChord(VkLWin);
    }

    public static void CloseStartMenu()
    {
        SendKeyChord(VkEscape);
    }

    public static bool IsStartMenuOpen()
    {
        return TryFindVisibleStartMenuWindow() || TryFindVisibleStartMenuAutomationElement();
    }

    public static void OpenPowerUserMenu()
    {
        if (TryOpenPowerUserMenuThroughNativeStartButton())
        {
            return;
        }

        SendKeyChord(VkLWin, VkX);
    }

    private static bool TryOpenPowerUserMenuThroughNativeStartButton()
    {
        try
        {
            var startButton = FindVisibleStartButton();
            if (startButton is null)
            {
                return false;
            }

            var bounds = startButton.Current.BoundingRectangle;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            var x = (int)Math.Round(bounds.Left + bounds.Width / 2);
            var y = (int)Math.Round(bounds.Top + bounds.Height / 2);
            return SendMouseRightClick(x, y);
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(ex, "TryOpenPowerUserMenuThroughNativeStartButton");
            return false;
        }
    }

    private static bool TryFindVisibleStartMenuWindow()
    {
        var found = false;
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle))
            {
                return true;
            }

            var className = GetWindowClassName(handle);
            var title = GetWindowTitle(handle);
            if (LooksLikeStartMenu(title, className) && HasUsableBounds(handle))
            {
                found = true;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static bool TryFindVisibleStartMenuAutomationElement()
    {
        try
        {
            var topLevelWindows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
            for (var index = 0; index < topLevelWindows.Count; index++)
            {
                var element = topLevelWindows[index];
                if (element.Current.IsOffscreen)
                {
                    continue;
                }

                if (LooksLikeStartMenu(element.Current.Name, element.Current.ClassName) &&
                    !element.Current.BoundingRectangle.IsEmpty)
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool LooksLikeStartMenu(string title, string className)
    {
        var hasStartTitle =
            title.Equals("Start", StringComparison.OrdinalIgnoreCase) ||
            title.Equals("Start menu", StringComparison.OrdinalIgnoreCase) ||
            title.Equals("Iniciar", StringComparison.OrdinalIgnoreCase) ||
            title.Equals("Menu Iniciar", StringComparison.OrdinalIgnoreCase);

        if (!hasStartTitle)
        {
            return false;
        }

        return className.Contains("CoreWindow", StringComparison.OrdinalIgnoreCase) ||
               className.Contains("Xaml", StringComparison.OrdinalIgnoreCase) ||
               className.Contains("Start", StringComparison.OrdinalIgnoreCase) ||
               className.Contains("ApplicationFrame", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasUsableBounds(IntPtr handle)
    {
        return GetWindowRect(handle, out var rect) &&
               rect.Right > rect.Left &&
               rect.Bottom > rect.Top;
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var buffer = new StringBuilder(256);
        GetWindowText(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string GetWindowClassName(IntPtr handle)
    {
        var buffer = new StringBuilder(256);
        GetClassName(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static AutomationElement? FindVisibleStartButton()
    {
        var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, "StartButton");
        var elements = AutomationElement.RootElement.FindAll(TreeScope.Descendants, condition);

        for (var index = 0; index < elements.Count; index++)
        {
            var element = elements[index];
            if (element.Current.IsOffscreen)
            {
                continue;
            }

            var bounds = element.Current.BoundingRectangle;
            if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
            {
                return element;
            }
        }

        return null;
    }

    private static bool SendMouseRightClick(int x, int y)
    {
        var restoreCursor = GetCursorPos(out var previousPosition);
        if (!SetCursorPos(x, y))
        {
            return false;
        }

        var inputs = new[]
        {
            CreateMouseInput(MouseEventFRightDown),
            CreateMouseInput(MouseEventFRightUp)
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        Thread.Sleep(40);

        if (restoreCursor)
        {
            SetCursorPos(previousPosition.X, previousPosition.Y);
        }

        return sent == inputs.Length;
    }

    private static bool SendKeyChord(params ushort[] keys)
    {
        var inputs = new Input[keys.Length * 2];
        var index = 0;

        foreach (var key in keys)
        {
            inputs[index++] = CreateKeyboardInput(key, keyUp: false);
        }

        for (var keyIndex = keys.Length - 1; keyIndex >= 0; keyIndex--)
        {
            inputs[index++] = CreateKeyboardInput(keys[keyIndex], keyUp: true);
        }

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length;
    }

    private static Input CreateKeyboardInput(ushort virtualKey, bool keyUp)
    {
        return new Input
        {
            type = InputKeyboard,
            u = new InputUnion
            {
                ki = new KeyboardInput
                {
                    wVk = virtualKey,
                    wScan = (ushort)MapVirtualKey(virtualKey, MapvkVkToVsc),
                    dwFlags = (IsExtendedKey(virtualKey) ? KeyEventFExtendedKey : 0) |
                              (keyUp ? KeyEventFKeyUp : 0)
                }
            }
        };
    }

    private static Input CreateMouseInput(uint flags)
    {
        return new Input
        {
            type = InputMouse,
            u = new InputUnion
            {
                mi = new MouseInput
                {
                    dwFlags = flags
                }
            }
        };
    }

    private static bool IsExtendedKey(ushort virtualKey)
    {
        return virtualKey is 0x21 or 0x22 or 0x23 or 0x24 or
            0x25 or 0x26 or 0x27 or 0x28 or
            0x2D or 0x2E or 0x5B or 0x5C or
            0x6F or 0x90 or 0x91;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetClassName(IntPtr handle, StringBuilder className, int count);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput mi;

        [FieldOffset(0)]
        public KeyboardInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
