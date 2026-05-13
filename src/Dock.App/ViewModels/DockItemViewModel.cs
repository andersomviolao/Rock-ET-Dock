using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Dock.App.Models;
using Dock.App.Services;

namespace Dock.App.ViewModels;

public sealed class DockItemViewModel : INotifyPropertyChanged
{
    private bool _isBeingDragged;

    public DockItemViewModel(DockItem item)
    {
        Item = item;
        Icon = item.Kind switch
        {
            DockItemKind.WindowsButton => SpecialIconService.GetWindowsLogo(),
            DockItemKind.RecycleBin => ShellIconService.GetRecycleBinIcon(),
            DockItemKind.DropPlaceholder => SpecialIconService.GetDropPlaceholderIcon(),
            DockItemKind.Window when string.IsNullOrWhiteSpace(item.TargetPath) => SpecialIconService.GetWindowIcon(),
            _ => ShellIconService.GetIcon(item.TargetPath)
        };
    }

    public DockItem Item { get; }

    public string DisplayName => Item.DisplayName;

    public string ShortLabel => Item.DisplayName.Length <= 12
        ? Item.DisplayName
        : Item.DisplayName[..12];

    public ImageSource Icon { get; }

    public bool IsWindowsButton => Item.Kind == DockItemKind.WindowsButton;

    public bool IsRecycleBin => Item.Kind == DockItemKind.RecycleBin;

    public bool IsAnimatedGif => Item.Kind == DockItemKind.AnimatedGif;

    public bool IsDropPlaceholder => Item.Kind == DockItemKind.DropPlaceholder;

    public Visibility StaticIconVisibility => IsAnimatedGif ? Visibility.Collapsed : Visibility.Visible;

    public Visibility AnimatedGifVisibility => IsAnimatedGif ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ContentVisibility => IsDropPlaceholder ? Visibility.Collapsed : Visibility.Visible;

    public bool IsBeingDragged
    {
        get => _isBeingDragged;
        set
        {
            if (_isBeingDragged == value)
            {
                return;
            }

            _isBeingDragged = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DragOpacity));
        }
    }

    public double DragOpacity => IsBeingDragged ? 0.0 : 1.0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
