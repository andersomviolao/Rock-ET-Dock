using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace Dock.App.Services;

public static class RecycleBinService
{
    private const string RecycleBinParsingName = "shell:::{645FF040-5081-101B-9F08-00AA002F954E}";
    private const uint CmfNormal = 0x00000000;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCommand = 0x0100;
    private const uint TpmNonotify = 0x0080;
    private const uint CmicMaskUnicode = 0x00004000;
    private const uint FoDelete = 0x0003;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoConfirmation = 0x0010;

    public static void Open()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shell:RecycleBinFolder",
            UseShellExecute = true
        });
    }

    public static void ShowContextMenu(Window owner, Point screenPoint)
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
            ThrowIfFailed(SHParseDisplayName(RecycleBinParsingName, IntPtr.Zero, out pidl, 0, out attributes));

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

            ThrowIfFailed(contextMenu.QueryContextMenu(menu, 0, 1, 0x7FFF, CmfNormal));
            var command = TrackPopupMenuEx(
                menu,
                TpmRightButton | TpmReturnCommand | TpmNonotify,
                (int)Math.Round(screenPoint.X),
                (int)Math.Round(screenPoint.Y),
                hwnd,
                IntPtr.Zero);

            if (command > 0)
            {
                var invoke = new CmInvokeCommandInfoEx
                {
                    cbSize = Marshal.SizeOf<CmInvokeCommandInfoEx>(),
                    fMask = CmicMaskUnicode,
                    hwnd = hwnd,
                    lpVerb = (IntPtr)(command - 1),
                    lpVerbW = (IntPtr)(command - 1),
                    nShow = 1
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

    public static void MovePathsToRecycleBin(string[] paths)
    {
        var existingPaths = paths
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Select(Path.GetFullPath)
            .ToArray();

        if (existingPaths.Length == 0)
        {
            return;
        }

        var operation = new ShFileOpStruct
        {
            wFunc = FoDelete,
            pFrom = string.Join('\0', existingPaths) + "\0\0",
            fFlags = FofAllowUndo | FofNoConfirmation
        };

        var result = SHFileOperation(ref operation);
        if (result != 0)
        {
            throw new IOException($"Falha ao mover para a lixeira. Codigo: {result}.");
        }
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOpStruct lpFileOp);

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
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
