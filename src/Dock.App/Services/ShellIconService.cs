using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dock.App.Services;

public static class ShellIconService
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiSysIconIndex = 0x000004000;
    private const uint ShgsiIcon = 0x000000100;
    private const uint ShgsiLargeIcon = 0x000000000;
    private const uint ShgsiSysIconIndex = 0x000004000;
    private const int ShilExtraLarge = 2;
    private const int ShilJumbo = 4;
    private const int IldTransparent = 0x00000001;
    private const int SiidRecycler = 31;

    public static ImageSource GetIcon(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return CreateFallbackIcon();
        }

        return GetClassicShellIcon(path);
    }

    public static ImageSource GetRecycleBinIcon()
    {
        return GetClassicStockIcon(SiidRecycler);
    }

    private static ImageSource GetClassicShellIcon(string path)
    {
        var info = new ShFileInfo();
        var result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiLargeIcon);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return CreateFallbackIcon();
        }

        try
        {
            return CreateImageSourceFromIcon(info.hIcon);
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static ImageSource GetClassicStockIcon(int stockIconId)
    {
        var info = new ShStockIconInfo
        {
            cbSize = (uint)Marshal.SizeOf<ShStockIconInfo>()
        };

        if (SHGetStockIconInfo(stockIconId, ShgsiIcon | ShgsiLargeIcon, ref info) != 0 ||
            info.hIcon == IntPtr.Zero)
        {
            return CreateFallbackIcon();
        }

        try
        {
            return CreateImageSourceFromIcon(info.hIcon);
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static bool TryGetShellImageListIcon(string path, out ImageSource source)
    {
        source = null!;
        var info = new ShFileInfo();
        var result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiSysIconIndex | ShgfiLargeIcon);
        return result != IntPtr.Zero &&
               info.iIcon >= 0 &&
               TryCreateIconFromImageList(info.iIcon, out source);
    }

    private static bool TryGetStockImageListIcon(int stockIconId, out ImageSource source)
    {
        source = null!;
        var info = new ShStockIconInfo
        {
            cbSize = (uint)Marshal.SizeOf<ShStockIconInfo>()
        };

        return SHGetStockIconInfo(stockIconId, ShgsiSysIconIndex | ShgsiLargeIcon, ref info) == 0 &&
               info.iSysImageIndex >= 0 &&
               TryCreateIconFromImageList(info.iSysImageIndex, out source);
    }

    private static bool TryCreateIconFromImageList(int imageIndex, out ImageSource source)
    {
        source = null!;

        foreach (var imageListSize in new[] { ShilJumbo, ShilExtraLarge })
        {
            var imageListId = ImageListId;
            if (SHGetImageList(imageListSize, ref imageListId, out var imageList) != 0 || imageList is null)
            {
                continue;
            }

            var iconHandle = IntPtr.Zero;
            try
            {
                if (imageList.GetIcon(imageIndex, IldTransparent, ref iconHandle) == 0 &&
                    iconHandle != IntPtr.Zero)
                {
                    source = CreateImageSourceFromIcon(iconHandle);
                    return true;
                }
            }
            finally
            {
                if (iconHandle != IntPtr.Zero)
                {
                    DestroyIcon(iconHandle);
                }

                Marshal.FinalReleaseComObject(imageList);
            }
        }

        return false;
    }

    private static ImageSource CreateImageSourceFromIcon(IntPtr iconHandle)
    {
        var source = Imaging.CreateBitmapSourceFromHIcon(
            iconHandle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(96, 96));
        source.Freeze();
        return source;
    }

    private static ImageSource CreateFallbackIcon()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(92, 138, 255)),
            null,
            new RectangleGeometry(new Rect(10, 8, 28, 32), 4, 4)));
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
            null,
            new RectangleGeometry(new Rect(15, 15, 18, 3), 1, 1)));
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            null,
            new RectangleGeometry(new Rect(15, 22, 18, 3), 1, 1)));

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("Shell32.dll", SetLastError = false)]
    private static extern int SHGetStockIconInfo(
        int siid,
        uint uFlags,
        ref ShStockIconInfo psii);

    [DllImport("Shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(
        int iImageList,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IImageList ppv);

    [DllImport("User32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static Guid ImageListId => new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig]
        int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);

        [PreserveSig]
        int ReplaceIcon(int i, IntPtr hicon, ref int pi);

        [PreserveSig]
        int SetOverlayImage(int iImage, int iOverlay);

        [PreserveSig]
        int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);

        [PreserveSig]
        int AddMasked(IntPtr hbmImage, int crMask, ref int pi);

        [PreserveSig]
        int Draw(IntPtr pimldp);

        [PreserveSig]
        int Remove(int i);

        [PreserveSig]
        int GetIcon(int i, int flags, ref IntPtr picon);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShStockIconInfo
    {
        public uint cbSize;
        public IntPtr hIcon;
        public int iSysImageIndex;
        public int iIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPath;
    }
}
