using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Dock.App.Models;
using Dock.App.Services;

namespace Dock.App.ViewModels;

public sealed class DockBarViewModel : INotifyPropertyChanged
{
    public const string DropPlaceholderId = "__rock-et-dock-drop-placeholder";
    public static readonly string[] ThemeNames =
    [
        "Rock ET Glass",
        "Aero Milk",
        "AstroGlass",
        "AstroGrey",
        "AstroIron",
        "AstroLife",
        "AstroOrange",
        "AstroSteel",
        "Blank",
        "Brushed",
        "CrystalXP.net",
        "Dark Matter",
        "Inspirat",
        "Luminous",
        "Milk1",
        "Milk2",
        "Minired",
        "Painting",
        "ProtoClay",
        "ProtoGlass",
        "ProtoIron",
        "ProtoSea",
        "ProtoSky",
        "ProtoSteel",
        "ProtoTree",
        "Simply",
        "Special-RD",
        "ToonBLue",
        "Vista",
        "VistaBlack",
        "WhiteCristal",
        "ZaKtoon",
        "Alien Milk"
    ];

    private LocalizedText _text;

    public DockBarViewModel(DockBarSettings bar, string? language = null)
    {
        Bar = bar;
        _text = TextCatalog.Get(language);
        Items = new ObservableCollection<DockItemViewModel>(bar.Items.Select(item => new DockItemViewModel(item, _text)));
    }

    public DockBarSettings Bar { get; }

    public ObservableCollection<DockItemViewModel> Items { get; }

    public System.Windows.Controls.Orientation Orientation => Bar.Edge is DockEdge.Left or DockEdge.Right
        ? System.Windows.Controls.Orientation.Vertical
        : System.Windows.Controls.Orientation.Horizontal;

    public double ItemButtonSize => Bar.HideLabels
        ? Clamp(Bar.IconSize + 16, 28, 136)
        : Clamp(Bar.IconSize + 24, 40, 136);

    public double IconTileSize => Bar.HideLabels
        ? Clamp(Bar.IconSize + 10, 22, 128)
        : Clamp(Bar.IconSize + 18, 30, 128);

    public double IconImageSize => Clamp(Bar.IconSize, 12, 96);

    public Thickness ItemMargin => new(Clamp(Bar.IconSpacing, 0, 80) / 2.0);

    public Thickness ItemContentMargin => new(0, 0, 0, Clamp(Bar.IconBottomMargin, 0, 120));

    public CornerRadius ShellCornerRadius => new(Clamp(
        Bar.ShellCornerRadius >= 0 ? Bar.ShellCornerRadius : GetThemePalette(Bar.Theme).ShellCornerRadius,
        0,
        36));

    public CornerRadius TileCornerRadius => new(Clamp(
        Bar.TileCornerRadius >= 0 ? Bar.TileCornerRadius : GetThemePalette(Bar.Theme).TileCornerRadius,
        0,
        24));

    public double LabelFontSize => Bar.FontSize;

    public double LabelMaxWidth => Math.Max(24, ItemButtonSize - 8);

    public double ItemOpacity => Clamp(Bar.IconOpacity, 15, 100) / 100.0;

    public double CrossAxisZoomOverhang => Bar.ZoomEnabled
        ? Clamp((ItemButtonSize * (FocusedZoomScale - 1.0) / 2.0) + 12, 12, 96)
        : 12;

    public double PrimaryAxisZoomOverhang => Bar.ZoomEnabled
        ? Clamp(CalculateRequiredHoverAxisExtent() + 12, CrossAxisZoomOverhang, 220)
        : 12;

    public double HorizontalZoomOverhang => Orientation == System.Windows.Controls.Orientation.Vertical
        ? CrossAxisZoomOverhang
        : PrimaryAxisZoomOverhang;

    public double VerticalZoomOverhang => Orientation == System.Windows.Controls.Orientation.Vertical
        ? PrimaryAxisZoomOverhang
        : CrossAxisZoomOverhang;

    public double ZoomOverhang => Math.Max(HorizontalZoomOverhang, VerticalZoomOverhang);

    public Thickness DockShellMargin => new(HorizontalZoomOverhang, VerticalZoomOverhang, HorizontalZoomOverhang, VerticalZoomOverhang);

    public Visibility LabelVisibility => Bar.HideLabels ? Visibility.Collapsed : Visibility.Visible;

    public Brush ShellBackground
    {
        get
        {
            var alpha = (byte)(Clamp(Bar.BackgroundOpacity, 15, 100) * 255 / 100);
            var color = GetThemePalette(Bar.Theme).ShellColor;

            return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        }
    }

    public Brush ShellBorderBrush => CreateBrush(GetThemePalette(Bar.Theme).ShellBorder);

    public Brush TileBackground => CreateBrush(GetThemePalette(Bar.Theme).TileBackground);

    public Brush TileBorderBrush => CreateBrush(GetThemePalette(Bar.Theme).TileBorder);

    public Brush SeparatorBrush => CreateBrush(GetThemePalette(Bar.Theme).ShellBorder);

    public double SeparatorWidth => Orientation == System.Windows.Controls.Orientation.Vertical
        ? Math.Max(16, IconTileSize * 0.72)
        : 3;

    public double SeparatorHeight => Orientation == System.Windows.Controls.Orientation.Vertical
        ? 3
        : Math.Max(16, IconTileSize * 0.72);

    public double ShadowOpacity => GetThemePalette(Bar.Theme).ShadowOpacity;

    public Brush LabelBrush
    {
        get
        {
            try
            {
                return (Brush)new BrushConverter().ConvertFromString(Bar.LabelColor)!;
            }
            catch
            {
                return new SolidColorBrush(Color.FromArgb(232, 255, 255, 255));
            }
        }
    }

    public double GetZoomScaleForDistance(double distance)
    {
        if (!Bar.ZoomEnabled || distance < 0)
        {
            return 1.0;
        }

        var focusedScale = FocusedZoomScale;
        if (distance <= 0)
        {
            return focusedScale;
        }

        var range = Clamp(Bar.ZoomRange, 0, 20);
        var radius = range + 0.65;
        if (range <= 0 || distance > radius)
        {
            return 1.0;
        }

        var t = Clamp(1.0 - (distance / radius), 0, 1);
        var influence = t * t * (3 - (2 * t));
        return 1.0 + ((focusedScale - 1.0) * influence);
    }

    public void AddItem(DockItem item)
    {
        Bar.Items.Add(item);
        Items.Add(new DockItemViewModel(item, _text));
    }

    public void InsertItem(int index, DockItem item)
    {
        var insertIndex = Clamp(index, 0, Items.Count);
        Items.Insert(insertIndex, new DockItemViewModel(item, _text));
        PersistVisualOrder();
    }

    public void AddRuntimeItem(DockItem item)
    {
        var existing = Items.FirstOrDefault(viewModel => viewModel.Item.NativeWindowHandle == item.NativeWindowHandle);
        if (existing is not null)
        {
            Items.Remove(existing);
        }

        Items.Add(new DockItemViewModel(item, _text));
    }

    public bool RemoveRuntimeWindow(long nativeWindowHandle)
    {
        var item = Items.FirstOrDefault(viewModel =>
            viewModel.Item.IsRuntime &&
            viewModel.Item.NativeWindowHandle == nativeWindowHandle);

        if (item is null)
        {
            return false;
        }

        Items.Remove(item);
        return true;
    }

    public void SyncPersistentItemsFromSettings()
    {
        var runtimeItems = Items
            .Where(static viewModel => viewModel.Item.IsRuntime)
            .Select(static viewModel => viewModel.Item)
            .ToList();

        Items.Clear();
        foreach (var item in Bar.Items)
        {
            Items.Add(new DockItemViewModel(item, _text));
        }

        foreach (var item in runtimeItems)
        {
            Items.Add(new DockItemViewModel(item, _text));
        }
    }

    public bool MoveItemToIndex(string draggedItemId, int insertionIndex, bool persist = true)
    {
        var source = Items.FirstOrDefault(item => item.Item.Id == draggedItemId);
        if (source is null)
        {
            return false;
        }

        var oldIndex = Items.IndexOf(source);
        var newIndex = Clamp(insertionIndex, 0, Items.Count);
        if (newIndex > oldIndex)
        {
            newIndex--;
        }

        if (newIndex < 0 || oldIndex == newIndex)
        {
            return false;
        }

        Items.Move(oldIndex, newIndex);
        if (persist)
        {
            PersistVisualOrder();
        }

        return true;
    }

    public bool MoveItemToEnd(string draggedItemId)
    {
        return MoveItemToIndex(draggedItemId, Items.Count);
    }

    public bool RemoveItem(string itemId)
    {
        var item = Items.FirstOrDefault(viewModel => viewModel.Item.Id == itemId);
        if (item is null)
        {
            return false;
        }

        Items.Remove(item);
        PersistVisualOrder();
        return true;
    }

    public bool SetDropPlaceholderVisualIndex(int visualInsertionIndex)
    {
        var existing = Items.FirstOrDefault(static viewModel => viewModel.IsDropPlaceholder);
        var fullIndex = GetFullIndexForVisualInsertionIndex(visualInsertionIndex);

        if (existing is null)
        {
            Items.Insert(Clamp(fullIndex, 0, Items.Count), new DockItemViewModel(new DockItem
            {
                Id = DropPlaceholderId,
                Kind = DockItemKind.DropPlaceholder,
                DisplayName = "",
                IsRuntime = true
            }, _text));
            return true;
        }

        var oldIndex = Items.IndexOf(existing);
        var newIndex = fullIndex;
        if (newIndex > oldIndex)
        {
            newIndex--;
        }

        newIndex = Clamp(newIndex, 0, Items.Count - 1);
        if (oldIndex == newIndex)
        {
            return false;
        }

        Items.Move(oldIndex, newIndex);
        return true;
    }

    public int RemoveDropPlaceholder()
    {
        var existing = Items.FirstOrDefault(static viewModel => viewModel.IsDropPlaceholder);
        if (existing is null)
        {
            return -1;
        }

        var index = Items.IndexOf(existing);
        Items.Remove(existing);
        return index;
    }

    public int DropPlaceholderIndex
    {
        get
        {
            var existing = Items.FirstOrDefault(static viewModel => viewModel.IsDropPlaceholder);
            return existing is null ? -1 : Items.IndexOf(existing);
        }
    }

    public bool MoveItemToAbsoluteIndex(string draggedItemId, int targetIndex)
    {
        var source = Items.FirstOrDefault(item => item.Item.Id == draggedItemId);
        if (source is null)
        {
            return false;
        }

        var oldIndex = Items.IndexOf(source);
        var newIndex = Clamp(targetIndex, 0, Items.Count - 1);
        if (oldIndex == newIndex)
        {
            return false;
        }

        Items.Move(oldIndex, newIndex);
        PersistVisualOrder();
        return true;
    }

    public void PersistVisualOrder()
    {
        Bar.Items = Items
            .Where(static viewModel => !viewModel.Item.IsRuntime && !viewModel.IsDropPlaceholder)
            .Select(static viewModel => viewModel.Item)
            .ToList();
    }

    public void RefreshSettings()
    {
        OnPropertyChanged(nameof(Orientation));
        OnPropertyChanged(nameof(ItemButtonSize));
        OnPropertyChanged(nameof(IconTileSize));
        OnPropertyChanged(nameof(IconImageSize));
        OnPropertyChanged(nameof(ItemMargin));
        OnPropertyChanged(nameof(ItemContentMargin));
        OnPropertyChanged(nameof(ShellCornerRadius));
        OnPropertyChanged(nameof(TileCornerRadius));
        OnPropertyChanged(nameof(LabelFontSize));
        OnPropertyChanged(nameof(LabelMaxWidth));
        OnPropertyChanged(nameof(ItemOpacity));
        OnPropertyChanged(nameof(CrossAxisZoomOverhang));
        OnPropertyChanged(nameof(PrimaryAxisZoomOverhang));
        OnPropertyChanged(nameof(HorizontalZoomOverhang));
        OnPropertyChanged(nameof(VerticalZoomOverhang));
        OnPropertyChanged(nameof(ZoomOverhang));
        OnPropertyChanged(nameof(DockShellMargin));
        OnPropertyChanged(nameof(LabelVisibility));
        OnPropertyChanged(nameof(ShellBackground));
        OnPropertyChanged(nameof(ShellBorderBrush));
        OnPropertyChanged(nameof(TileBackground));
        OnPropertyChanged(nameof(TileBorderBrush));
        OnPropertyChanged(nameof(SeparatorBrush));
        OnPropertyChanged(nameof(SeparatorWidth));
        OnPropertyChanged(nameof(SeparatorHeight));
        OnPropertyChanged(nameof(ShadowOpacity));
        OnPropertyChanged(nameof(LabelBrush));
    }

    public void SetLanguage(string? language)
    {
        _text = TextCatalog.Get(language);
        foreach (var item in Items)
        {
            item.SetText(_text);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private int GetFullIndexForVisualInsertionIndex(int visualInsertionIndex)
    {
        var target = Clamp(visualInsertionIndex, 0, Items.Count);
        var seen = 0;
        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index].IsDropPlaceholder)
            {
                continue;
            }

            if (seen == target)
            {
                return index;
            }

            seen++;
        }

        return Items.Count;
    }

    private static int Clamp(int value, int minimum, int maximum)
    {
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    private double FocusedZoomScale => Clamp((double)Bar.ZoomSize / Math.Max(1, Bar.IconSize), 1.0, 1.8);

    private double CalculateRequiredHoverAxisExtent()
    {
        var slotStep = Math.Max(1, ItemButtonSize + Math.Max(0, Bar.IconSpacing));
        var radius = Clamp(Bar.ZoomRange, 0, 20) + 0.65;
        var itemCount = Math.Max(3, ((int)Math.Ceiling(radius) * 2) + 3);
        var centers = Enumerable.Range(0, itemCount)
            .Select(index => index * slotStep)
            .ToArray();
        var maxExtent = 0.0;

        for (var pointerStep = 0; pointerStep <= (itemCount - 1) * 2; pointerStep++)
        {
            var pointerAxis = pointerStep * slotStep / 2.0;
            var scales = centers
                .Select(center => GetZoomScaleForDistance(Math.Abs(pointerAxis - center) / slotStep))
                .ToArray();
            var offsets = DockZoomLayout.CalculateOffsets(centers, scales, ItemButtonSize);

            for (var index = 0; index < centers.Length; index++)
            {
                var ownExpansion = ItemButtonSize * (scales[index] - 1.0) / 2.0;
                maxExtent = Math.Max(maxExtent, Math.Abs(offsets[index]) + ownExpansion);
            }
        }

        return maxExtent;
    }

    private static Brush CreateBrush(Color color)
    {
        return color.A == 0 ? Brushes.Transparent : new SolidColorBrush(color);
    }

    private static DockThemePalette GetThemePalette(string theme)
    {
        return theme switch
        {
            "Aero Milk" => new DockThemePalette(Color.FromRgb(232, 242, 248), Color.FromArgb(150, 122, 157, 176), Color.FromArgb(82, 255, 255, 255), Color.FromArgb(120, 120, 160, 184), 0.22, 2, 0),
            "AstroGlass" => new DockThemePalette(Color.FromRgb(28, 38, 56), Color.FromArgb(150, 145, 188, 230), Color.FromArgb(64, 125, 170, 220), Color.FromArgb(110, 180, 220, 255), 0.34, 24, 16),
            "AstroGrey" => new DockThemePalette(Color.FromRgb(48, 52, 58), Color.FromArgb(150, 180, 184, 190), Color.FromArgb(58, 255, 255, 255), Color.FromArgb(95, 218, 222, 228), 0.3, 4, 4),
            "AstroIron" => new DockThemePalette(Color.FromRgb(34, 37, 41), Color.FromArgb(150, 145, 150, 156), Color.FromArgb(55, 210, 220, 235), Color.FromArgb(95, 150, 158, 168), 0.32, 0, 0),
            "AstroLife" => new DockThemePalette(Color.FromRgb(40, 63, 46), Color.FromArgb(150, 124, 184, 132), Color.FromArgb(62, 128, 214, 144), Color.FromArgb(100, 171, 235, 178), 0.3, 18, 12),
            "AstroOrange" => new DockThemePalette(Color.FromRgb(74, 46, 25), Color.FromArgb(150, 236, 143, 69), Color.FromArgb(68, 255, 166, 91), Color.FromArgb(110, 255, 188, 120), 0.32, 8, 0),
            "AstroSteel" => new DockThemePalette(Color.FromRgb(39, 52, 62), Color.FromArgb(150, 138, 171, 190), Colors.Transparent, Colors.Transparent, 0.3, 12, 0),
            "Blank" => new DockThemePalette(Color.FromRgb(18, 20, 24), Colors.Transparent, Colors.Transparent, Colors.Transparent, 0, 0, 0),
            "Brushed" => new DockThemePalette(Color.FromRgb(94, 96, 95), Color.FromArgb(155, 210, 210, 205), Color.FromArgb(48, 255, 255, 248), Color.FromArgb(92, 230, 230, 224), 0.25, 0, 0),
            "CrystalXP.net" => new DockThemePalette(Color.FromRgb(215, 232, 246), Color.FromArgb(155, 84, 138, 201), Color.FromArgb(72, 93, 161, 235), Color.FromArgb(110, 52, 105, 190), 0.25, 20, 18),
            "Dark Matter" => new DockThemePalette(Color.FromRgb(14, 17, 24), Color.FromArgb(120, 139, 98, 255), Color.FromArgb(70, 139, 98, 255), Color.FromArgb(110, 139, 98, 255), 0.3, 10, 4),
            "Inspirat" => new DockThemePalette(Color.FromRgb(37, 54, 71), Color.FromArgb(150, 108, 161, 202), Colors.Transparent, Colors.Transparent, 0.3, 16, 0),
            "Luminous" => new DockThemePalette(Color.FromRgb(245, 247, 232), Color.FromArgb(150, 190, 198, 120), Color.FromArgb(75, 255, 255, 190), Color.FromArgb(110, 220, 226, 154), 0.22, 30, 20),
            "Milk1" => new DockThemePalette(Color.FromRgb(238, 241, 236), Color.FromArgb(145, 155, 168, 150), Colors.Transparent, Colors.Transparent, 0.2, 18, 0),
            "Milk2" => new DockThemePalette(Color.FromRgb(226, 232, 224), Color.FromArgb(145, 136, 156, 132), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(92, 164, 178, 158), 0.22, 3, 3),
            "Minired" => new DockThemePalette(Color.FromRgb(72, 24, 30), Color.FromArgb(150, 226, 82, 92), Color.FromArgb(70, 255, 84, 96), Color.FromArgb(110, 255, 130, 140), 0.32, 0, 0),
            "Painting" => new DockThemePalette(Color.FromRgb(83, 70, 55), Color.FromArgb(150, 211, 183, 138), Color.FromArgb(65, 245, 212, 156), Color.FromArgb(105, 235, 196, 130), 0.26, 22, 6),
            "ProtoClay" => new DockThemePalette(Color.FromRgb(91, 78, 64), Color.FromArgb(150, 190, 162, 126), Colors.Transparent, Colors.Transparent, 0.28, 6, 0),
            "ProtoGlass" => new DockThemePalette(Color.FromRgb(42, 55, 70), Color.FromArgb(150, 134, 190, 226), Color.FromArgb(62, 136, 198, 238), Color.FromArgb(105, 190, 230, 255), 0.32, 28, 18),
            "ProtoIron" => new DockThemePalette(Color.FromRgb(35, 36, 38), Color.FromArgb(150, 128, 132, 138), Color.FromArgb(54, 200, 205, 212), Color.FromArgb(96, 150, 154, 162), 0.32, 0, 0),
            "ProtoSea" => new DockThemePalette(Color.FromRgb(24, 66, 76), Color.FromArgb(150, 78, 178, 198), Color.FromArgb(62, 76, 206, 228), Color.FromArgb(105, 140, 230, 244), 0.3, 14, 14),
            "ProtoSky" => new DockThemePalette(Color.FromRgb(44, 77, 108), Color.FromArgb(150, 112, 182, 232), Colors.Transparent, Colors.Transparent, 0.3, 22, 0),
            "ProtoSteel" => new DockThemePalette(Color.FromRgb(54, 63, 70), Color.FromArgb(150, 150, 170, 184), Color.FromArgb(56, 190, 205, 218), Color.FromArgb(96, 170, 190, 205), 0.28, 7, 2),
            "ProtoTree" => new DockThemePalette(Color.FromRgb(39, 72, 44), Color.FromArgb(150, 92, 168, 102), Colors.Transparent, Colors.Transparent, 0.28, 14, 0),
            "Simply" => new DockThemePalette(Color.FromRgb(240, 240, 240), Color.FromArgb(145, 165, 165, 165), Colors.Transparent, Colors.Transparent, 0.18, 0, 0),
            "Special-RD" => new DockThemePalette(Color.FromRgb(41, 37, 54), Color.FromArgb(150, 174, 144, 224), Color.FromArgb(62, 182, 150, 238), Color.FromArgb(105, 220, 198, 255), 0.32, 12, 0),
            "ToonBLue" => new DockThemePalette(Color.FromRgb(54, 106, 162), Color.FromArgb(170, 40, 78, 130), Color.FromArgb(80, 92, 178, 245), Color.FromArgb(130, 22, 70, 130), 0.25, 0, 0),
            "Vista" => new DockThemePalette(Color.FromRgb(30, 52, 82), Color.FromArgb(150, 110, 170, 230), Color.FromArgb(70, 90, 170, 250), Color.FromArgb(110, 170, 220, 255), 0.34, 24, 16),
            "VistaBlack" => new DockThemePalette(Color.FromRgb(16, 18, 22), Color.FromArgb(150, 90, 96, 108), Color.FromArgb(64, 255, 255, 255), Color.FromArgb(95, 145, 152, 166), 0.34, 4, 0),
            "WhiteCristal" => new DockThemePalette(Color.FromRgb(248, 250, 252), Color.FromArgb(145, 165, 188, 214), Colors.Transparent, Colors.Transparent, 0.2, 28, 0),
            "ZaKtoon" => new DockThemePalette(Color.FromRgb(78, 98, 55), Color.FromArgb(165, 45, 78, 28), Color.FromArgb(74, 146, 190, 72), Color.FromArgb(125, 42, 82, 26), 0.25, 0, 0),
            "Alien Milk" => new DockThemePalette(Color.FromRgb(238, 255, 236), Color.FromArgb(140, 70, 130, 82), Color.FromArgb(100, 83, 176, 98), Color.FromArgb(90, 56, 122, 68), 0.24, 18, 10),
            _ => new DockThemePalette(Color.FromRgb(27, 31, 38), Color.FromArgb(85, 255, 255, 255), Color.FromArgb(31, 255, 255, 255), Color.FromArgb(34, 255, 255, 255), 0.28, 14, 10)
        };
    }

    public static DockThemeShape GetThemeShape(string theme)
    {
        var palette = GetThemePalette(theme);
        return new DockThemeShape(palette.ShellCornerRadius, palette.TileCornerRadius);
    }

    public readonly record struct DockThemeShape(int ShellCornerRadius, int TileCornerRadius);

    private readonly record struct DockThemePalette(
        Color ShellColor,
        Color ShellBorder,
        Color TileBackground,
        Color TileBorder,
        double ShadowOpacity,
        int ShellCornerRadius,
        int TileCornerRadius);
}
