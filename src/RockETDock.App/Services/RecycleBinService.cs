using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace RockETDock.App.Services;

public static class RecycleBinService
{
    private const string RecycleBinParsingName = "shell:::{645FF040-5081-101B-9F08-00AA002F954E}";
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

    // Delegates to ShellContextMenuService so COM interfaces are defined in one place (NativeShellInterop).
    public static void ShowContextMenu(Window owner, Point screenPoint)
    {
        ShellContextMenuService.ShowForParsingName(owner, RecycleBinParsingName, screenPoint);
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
            throw new IOException($"Failed to move to Recycle Bin. Code: {result}.");
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOpStruct lpFileOp);

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
}
