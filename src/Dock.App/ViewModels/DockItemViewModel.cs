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
    private LocalizedText _text;
    private bool _isBeingDragged;
    private bool _isRunning;

    public DockItemViewModel(DockItem item, LocalizedText? text = null)
    {
        Item = item;
        _text = text ?? TextCatalog.Get(TextCatalog.English);
        Icon = item.Kind switch
        {
            DockItemKind.WindowsButton => SpecialIconService.GetWindowsLogo(),
            DockItemKind.RecycleBin => ShellIconService.GetRecycleBinIcon(),
            DockItemKind.DropPlaceholder => SpecialIconService.GetDropPlaceholderIcon(),
            DockItemKind.Separator => SpecialIconService.GetSeparatorIcon(),
            DockItemKind.Window when string.IsNullOrWhiteSpace(item.TargetPath) => SpecialIconService.GetWindowIcon(),
            _ => ShellIconService.GetIcon(item.TargetPath)
        };
    }

    public DockItem Item { get; }

    public string DisplayName => Item.Kind switch
    {
        DockItemKind.WindowsButton => _text["ItemWindows"],
        DockItemKind.RecycleBin => _text["ItemRecycleBin"],
        DockItemKind.Separator => _text["ItemSeparator"],
        _ => Item.DisplayName
    };

    public string ShortLabel => DisplayName.Length <= 12
        ? DisplayName
        : DisplayName[..12];

    public ImageSource Icon { get; }

    public bool IsWindowsButton => Item.Kind == DockItemKind.WindowsButton;

    public bool IsRecycleBin => Item.Kind == DockItemKind.RecycleBin;

    public bool IsAnimatedGif => Item.Kind == DockItemKind.AnimatedGif;

    public bool IsDropPlaceholder => Item.Kind == DockItemKind.DropPlaceholder;

    public bool IsSeparator => Item.Kind == DockItemKind.Separator;

    public Visibility StaticIconVisibility => IsAnimatedGif ? Visibility.Collapsed : Visibility.Visible;

    public Visibility AnimatedGifVisibility => IsAnimatedGif ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ButtonVisibility => IsDropPlaceholder ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ContentVisibility => IsDropPlaceholder || IsSeparator ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SeparatorVisibility => IsSeparator ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RunningIndicatorVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RunningIndicatorVisibility));
        }
    }

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

    public void SetText(LocalizedText text)
    {
        _text = text;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ShortLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
