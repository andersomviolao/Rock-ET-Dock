using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Dock.App.Models;

namespace Dock.App.Services;

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
        IShellFolder? parentFolder = null;

        try
        {
            var attributes = 0u;
            ThrowIfFailed(SHParseDisplayName(parsingName, IntPtr.Zero, out pidl, 0, out attributes));

            var shellFolderId = typeof(IShellFolder).GUID;
            ThrowIfFailed(SHBindToParent(pidl, ref shellFolderId, out parentFolder, out var childPidl));

            var contextMenuId = typeof(IContextMenu).GUID;
            var children = new[] { childPidl };
            ThrowIfFailed(parentFolder.GetUIObjectOf(hwnd, 1, children, ref contextMenuId, IntPtr.Zero, out contextMenuPointer));

            var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(contextMenuPointer);
            menu = CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                return;
            }

            const uint firstCommand = 1;
            ThrowIfFailed(contextMenu.QueryContextMenu(menu, 0, firstCommand, 0x7FFF, CmfNormal));
            var command = TrackPopupMenuEx(
                menu,
                TpmRightButton | TpmReturnCommand | TpmNonotify,
                (int)Math.Round(screenPoint.X),
                (int)Math.Round(screenPoint.Y),
                hwnd,
                IntPtr.Zero);

            if (command > 0)
            {
                var commandOffset = command - (int)firstCommand;
                var invoke = new CmInvokeCommandInfoEx
                {
                    cbSize = Marshal.SizeOf<CmInvokeCommandInfoEx>(),
                    fMask = CmicMaskUnicode | CmicMaskPtInvoke,
                    hwnd = hwnd,
                    lpVerb = (IntPtr)commandOffset,
                    lpVerbW = (IntPtr)commandOffset,
                    nShow = 1,
                    ptInvoke = new PointInvoke
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
                DestroyMenu(menu);
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
                CoTaskMemFree(pidl);
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        string pszName,
        IntPtr pbc,
        out IntPtr ppidl,
        uint sfgaoIn,
        out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(
        IntPtr pidl,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellFolder ppv,
        out IntPtr ppidlLast);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(
        IntPtr hMenu,
        uint uFlags,
        int x,
        int y,
        IntPtr hwnd,
        IntPtr lptpm);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    private interface IShellFolder
    {
        [PreserveSig]
        int ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);

        [PreserveSig]
        int EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);

        [PreserveSig]
        int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

        [PreserveSig]
        int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int GetAttributesOf(uint cidl, IntPtr apidl, ref uint rgfInOut);

        [PreserveSig]
        int GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] apidl, ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);

        [PreserveSig]
        int GetDisplayNameOf(IntPtr pidl, uint uFlags, out StrRet pName);

        [PreserveSig]
        int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(ref CmInvokeCommandInfoEx pici);

        [PreserveSig]
        int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CmInvokeCommandInfoEx
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr lpTitle;
        public IntPtr lpVerbW;
        public IntPtr lpParametersW;
        public IntPtr lpDirectoryW;
        public IntPtr lpTitleW;
        public PointInvoke ptInvoke;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointInvoke
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StrRet
    {
        [FieldOffset(0)]
        public uint uType;

        [FieldOffset(4)]
        public IntPtr pOleStr;
    }
}
