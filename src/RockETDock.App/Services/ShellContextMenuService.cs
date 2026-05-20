using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using RockETDock.App.Models;

namespace RockETDock.App.Services;

public static class ShellContextMenuService
{
    private const string RecycleBinParsingName = "shell:::{645FF040-5081-101B-9F08-00AA002F954E}";
    private const uint CmfNormal = 0x00000000;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmNonotify = 0x0080;
    private const uint TpmReturnCommand = 0x0100;
    private const uint CmicMaskUnicode = 0x00004000;
    private const uint CmicMaskPtInvoke = 0x20000000;

    public static bool ShowForDockItem(Window owner, DockItem item, Point screenPoint)
    {
        if (item.Kind == DockItemKind.WindowsButton)
        {
            WindowsButtonService.OpenPowerUserMenu();
            return true;
        }

        var parsingName = GetShellParsingName(item);
        if (string.IsNullOrWhiteSpace(parsingName))
        {
            return false;
        }

        ShowForParsingName(owner, parsingName, screenPoint);
        return true;
    }

    public static void ShowForParsingName(Window owner, string parsingName, Point screenPoint)
    {
        var hwnd = new WindowInteropHelper(owner).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var pidl = IntPtr.Zero;
        var contextMenuPointer = IntPtr.Zero;
        var menu = IntPtr.Zero;
        NativeShellInterop.IShellFolder? parentFolder = null;

        try
        {
            var attributes = 0u;
            ThrowIfFailed(NativeShellInterop.SHParseDisplayName(parsingName, IntPtr.Zero, out pidl, 0, out attributes));

            var shellFolderId = typeof(NativeShellInterop.IShellFolder).GUID;
            ThrowIfFailed(NativeShellInterop.SHBindToParent(pidl, ref shellFolderId, out parentFolder, out var childPidl));

            var contextMenuId = typeof(NativeShellInterop.IContextMenu).GUID;
            var children = new[] { childPidl };
            ThrowIfFailed(parentFolder.GetUIObjectOf(hwnd, 1, children, ref contextMenuId, IntPtr.Zero, out contextMenuPointer));

            var contextMenu = (NativeShellInterop.IContextMenu)Marshal.GetObjectForIUnknown(contextMenuPointer);
            menu = NativeShellInterop.CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                return;
            }

            const uint firstCommand = 1;
            ThrowIfFailed(contextMenu.QueryContextMenu(menu, 0, firstCommand, 0x7FFF, CmfNormal));
            var command = NativeShellInterop.TrackPopupMenuEx(
                menu,
                TpmRightButton | TpmReturnCommand | TpmNonotify,
                (int)Math.Round(screenPoint.X),
                (int)Math.Round(screenPoint.Y),
                hwnd,
                IntPtr.Zero);

            if (command > 0)
            {
                var commandOffset = command - (int)firstCommand;
                var invoke = new NativeShellInterop.CmInvokeCommandInfoEx
                {
                    cbSize = Marshal.SizeOf<NativeShellInterop.CmInvokeCommandInfoEx>(),
                    fMask = CmicMaskUnicode | CmicMaskPtInvoke,
                    hwnd = hwnd,
                    lpVerb = (IntPtr)commandOffset,
                    lpVerbW = (IntPtr)commandOffset,
                    nShow = 1,
                    ptInvoke = new NativeShellInterop.ShellPoint
                    {
                        X = (int)Math.Round(screenPoint.X),
                        Y = (int)Math.Round(screenPoint.Y)
                    }
                };

                ThrowIfFailed(contextMenu.InvokeCommand(ref invoke));
            }
        }
        finally
        {
            if (menu != IntPtr.Zero)
            {
                NativeShellInterop.DestroyMenu(menu);
            }

            if (contextMenuPointer != IntPtr.Zero)
            {
                Marshal.Release(contextMenuPointer);
            }

            if (parentFolder is not null)
            {
                Marshal.ReleaseComObject(parentFolder);
            }

            if (pidl != IntPtr.Zero)
            {
                NativeShellInterop.CoTaskMemFree(pidl);
            }
        }
    }

    private static string? GetShellParsingName(DockItem item)
    {
        if (item.Kind == DockItemKind.RecycleBin)
        {
            return RecycleBinParsingName;
        }

        if (item.Kind is DockItemKind.DropPlaceholder or DockItemKind.Separator ||
            string.IsNullOrWhiteSpace(item.TargetPath))
        {
            return null;
        }

        var targetPath = item.TargetPath.Trim();
        if (targetPath.StartsWith("shell:::", StringComparison.OrdinalIgnoreCase))
        {
            return targetPath;
        }

        if (targetPath.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
        {
            var explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            return File.Exists(explorerPath) ? explorerPath : null;
        }

        if (Uri.TryCreate(targetPath, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return null;
        }

        try
        {
            if (File.Exists(targetPath) || Directory.Exists(targetPath))
            {
                return Path.GetFullPath(targetPath);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static void ThrowIfFailed(int hresult)
    {
        if (hresult < 0)
        {
            Marshal.ThrowExceptionForHR(hresult);
        }
    }
}
