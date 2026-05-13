using System;
using System.Collections.Generic;

namespace Dock.App.Models;

public sealed class DockConfiguration
{
    public int Version { get; set; } = 1;

    public ApplicationSettings App { get; set; } = new();

    public List<DockBarSettings> Bars { get; set; } = [];
}

public sealed class ApplicationSettings
{
    public string DisplayName { get; set; } = "Rock ET Dock";

    public string Language { get; set; } = "pt-BR";

    public bool RunAtStartup { get; set; }

    public bool MinimizeWindowsToDock { get; set; }

    public bool DisableMinimizeAnimations { get; set; }

    public bool ShowRunningIndicators { get; set; } = true;

    public bool OpenRunningInstances { get; set; } = true;

    public bool PopupOnMouseover { get; set; }

    public bool HideNativeTaskbar { get; set; }

    public int PopupDelayMs { get; set; } = 250;
}

public sealed class DockBarSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Principal";

    public DockEdge Edge { get; set; } = DockEdge.Bottom;

    public int MonitorIndex { get; set; }

    public double Offset { get; set; }

    public double CenterOffset { get; set; }

    public DockLayering Layering { get; set; } = DockLayering.TopMost;

    public bool HideLabels { get; set; }

    public bool LockItems { get; set; }

    public bool AutoHide { get; set; }

    public DockImportMode ImportMode { get; set; } = DockImportMode.MoveToBarFolder;

    public int AutoHideDelayMs { get; set; } = 500;

    public int AutoHideDurationMs { get; set; } = 180;

    public int IconSize { get; set; } = 40;

    public int BarWidth { get; set; }

    public int BarHeight { get; set; }

    public int IconSpacing { get; set; } = 8;

    public int IconBottomMargin { get; set; }

    public int ShellCornerRadius { get; set; } = -1;

    public int TileCornerRadius { get; set; } = -1;

    public IconQuality IconQuality { get; set; } = IconQuality.High;

    public int IconOpacity { get; set; } = 100;

    public bool ZoomEnabled { get; set; } = true;

    public int ZoomSize { get; set; } = 64;

    public int ZoomRange { get; set; } = 4;

    public int ZoomDurationMs { get; set; } = 180;

    public bool ZoomOpaque { get; set; } = true;

    public HoverEffect HoverEffect { get; set; } = HoverEffect.Bubble;

    public string Theme { get; set; } = "Rock ET Glass";

    public int BackgroundOpacity { get; set; } = 85;

    public string FontFamily { get; set; } = "Segoe UI";

    public double FontSize { get; set; } = 9;

    public string LabelColor { get; set; } = "#E8FFFFFF";

    public List<DockItem> Items { get; set; } = [];

    public static DockBarSettings Create(string name, DockEdge edge)
    {
        return new DockBarSettings
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Edge = edge
        };
    }
}

public sealed class DockItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DockItemKind Kind { get; set; } = DockItemKind.File;

    public string DisplayName { get; set; } = "";

    public string TargetPath { get; set; } = "";

    public string? OriginalSourcePath { get; set; }

    public long NativeWindowHandle { get; set; }

    public int ProcessId { get; set; }

    public bool IsRuntime { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public static DockItem CreateWindowsButton()
    {
        return new DockItem
        {
            Id = "windows-button",
            Kind = DockItemKind.WindowsButton,
            DisplayName = "Windows"
        };
    }

    public static DockItem CreateRecycleBin()
    {
        return new DockItem
        {
            Id = "recycle-bin",
            Kind = DockItemKind.RecycleBin,
            DisplayName = "Lixeira",
            TargetPath = "shell:RecycleBinFolder"
        };
    }

    public static DockItem CreateAnimatedGif(string targetPath, string displayName)
    {
        return new DockItem
        {
            Kind = DockItemKind.AnimatedGif,
            DisplayName = displayName,
            TargetPath = targetPath,
            OriginalSourcePath = targetPath
        };
    }

    public static DockItem CreateSeparator()
    {
        return new DockItem
        {
            Kind = DockItemKind.Separator,
            DisplayName = "Separador"
        };
    }

    public static DockItem CreateDockSettings()
    {
        return new DockItem
        {
            Kind = DockItemKind.DockSettings,
            DisplayName = "Configuracoes"
        };
    }

    public static DockItem CreateQuit()
    {
        return new DockItem
        {
            Kind = DockItemKind.Quit,
            DisplayName = "Sair"
        };
    }
}

public enum DockEdge
{
    Bottom,
    Top,
    Left,
    Right
}

public enum DockItemKind
{
    File,
    Folder,
    Link,
    Window,
    WindowsButton,
    RecycleBin,
    AnimatedGif,
    DropPlaceholder,
    Separator,
    DockSettings,
    Quit,
    Special
}

public enum DockLayering
{
    TopMost,
    Normal,
    Bottom
}

public enum IconQuality
{
    Low,
    Medium,
    High
}

public enum HoverEffect
{
    None,
    Bubble,
    Plateau
}

public enum DockImportMode
{
    MoveToBarFolder,
    CreateShortcutInBarFolder
}
