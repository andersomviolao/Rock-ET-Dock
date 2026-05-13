using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dock.App.Models;
using Dock.App.Services;
using Dock.App.ViewModels;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Dock.App;

public partial class SettingsWindow : Window
{
    private readonly DockConfigurationStore _store;
    private readonly DockBarSettings _bar;
    private readonly DockItemImporter _importer = new();
    private readonly DispatcherTimer _applyTimer = new();
    private bool _isLoadingValues;
    private bool _isSavingValues;

    public event EventHandler? SettingsApplied;

    public SettingsWindow(DockConfigurationStore store, DockBarSettings bar)
    {
        _store = store;
        _bar = bar;
        _applyTimer.Interval = TimeSpan.FromMilliseconds(250);
        _applyTimer.Tick += (_, _) =>
        {
            _applyTimer.Stop();
            ApplyValuesImmediately();
        };

        DataContext = new SettingsWindowText(bar);
        InitializeComponent();
        _isLoadingValues = true;
        LoadValues();
        _isLoadingValues = false;
        AttachImmediateApplyHandlers();
    }

    private void LoadValues()
    {
        var app = _store.Current.App;

        BarNameBox.Text = _bar.Name;
        RunAtStartupBox.IsChecked = app.RunAtStartup;
        HideLabelsBox.IsChecked = _bar.HideLabels;
        LockItemsBox.IsChecked = _bar.LockItems;
        AutoHideBox.IsChecked = _bar.AutoHide;
        WindowsButtonBox.IsChecked = _bar.Items.Any(static item => item.Kind == DockItemKind.WindowsButton);
        RecycleBinBox.IsChecked = _bar.Items.Any(static item => item.Kind == DockItemKind.RecycleBin);
        HideNativeTaskbarBox.IsChecked = app.HideNativeTaskbar;
        ImportModeBox.ItemsSource = EnumItems<DockImportMode>();
        ImportModeBox.SelectedValue = _bar.ImportMode;
        AutoHideDelaySlider.Value = _bar.AutoHideDelayMs;
        AutoHideDurationSlider.Value = _bar.AutoHideDurationMs;
        BarFolderBox.Text = UserPaths.EnsureBarFolder(_bar.Name);

        IconSizeSlider.Value = _bar.IconSize;
        IconOpacitySlider.Value = _bar.IconOpacity;
        IconSpacingSlider.Value = _bar.IconSpacing;
        IconBottomMarginSlider.Value = _bar.IconBottomMargin;
        IconQualityBox.ItemsSource = EnumItems<IconQuality>();
        IconQualityBox.SelectedValue = _bar.IconQuality;
        ZoomEnabledBox.IsChecked = _bar.ZoomEnabled;
        ZoomOpaqueBox.IsChecked = _bar.ZoomOpaque;
        ZoomSizeSlider.Value = _bar.ZoomSize;
        ZoomRangeSlider.Value = _bar.ZoomRange;
        ZoomDurationSlider.Value = _bar.ZoomDurationMs;
        HoverEffectBox.ItemsSource = EnumItems<HoverEffect>();
        HoverEffectBox.SelectedValue = _bar.HoverEffect;

        MonitorBox.ItemsSource = GetMonitorItems();
        MonitorBox.SelectedValue = _bar.MonitorIndex;
        if (MonitorBox.SelectedValue is null)
        {
            MonitorBox.SelectedIndex = 0;
        }
        EdgeBox.ItemsSource = EnumItems<DockEdge>();
        EdgeBox.SelectedValue = _bar.Edge;
        LayeringBox.ItemsSource = EnumItems<DockLayering>();
        LayeringBox.SelectedValue = _bar.Layering;
        BarWidthSlider.Value = _bar.BarWidth;
        BarHeightSlider.Value = _bar.BarHeight;
        OffsetSlider.Value = _bar.Offset;
        CenterOffsetSlider.Value = _bar.CenterOffset;

        ThemeBox.ItemsSource = DockBarViewModel.ThemeNames;
        ThemeBox.SelectedValue = _bar.Theme;
        if (ThemeBox.SelectedValue is null)
        {
            ThemeBox.SelectedIndex = 0;
        }
        var selectedTheme = ThemeBox.SelectedValue as string ?? _bar.Theme;
        var themeShape = DockBarViewModel.GetThemeShape(selectedTheme);
        BackgroundOpacitySlider.Value = _bar.BackgroundOpacity;
        ShellCornerRadiusSlider.Value = _bar.ShellCornerRadius >= 0 ? _bar.ShellCornerRadius : themeShape.ShellCornerRadius;
        TileCornerRadiusSlider.Value = _bar.TileCornerRadius >= 0 ? _bar.TileCornerRadius : themeShape.TileCornerRadius;
        FontFamilyBox.Text = _bar.FontFamily;
        FontSizeSlider.Value = _bar.FontSize;
        LabelColorBox.Text = _bar.LabelColor;

        MinimizeWindowsBox.IsChecked = app.MinimizeWindowsToDock;
        DisableMinimizeAnimationsBox.IsChecked = app.DisableMinimizeAnimations;
        ShowRunningIndicatorsBox.IsChecked = app.ShowRunningIndicators;
        OpenRunningInstancesBox.IsChecked = app.OpenRunningInstances;
        PopupOnMouseoverBox.IsChecked = app.PopupOnMouseover;
        PopupDelaySlider.Value = app.PopupDelayMs;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _applyTimer.Stop();
        if (SaveValues())
        {
            SettingsApplied?.Invoke(this, EventArgs.Empty);
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        _applyTimer.Stop();
        if (SaveValues())
        {
            SettingsApplied?.Invoke(this, EventArgs.Empty);
        }
    }

    private void AddGif_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Adicionar GIF animado",
            Filter = "GIF animado (*.gif)|*.gif",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            foreach (var item in _importer.ImportAnimatedGifs(_bar, dialog.FileNames))
            {
                _bar.Items.Add(item);
            }

            _store.Save();
            SettingsApplied?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, UserPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AttachImmediateApplyHandlers()
    {
        foreach (var checkBox in new[]
                 {
                     RunAtStartupBox, HideLabelsBox, LockItemsBox, AutoHideBox, WindowsButtonBox,
                     RecycleBinBox, HideNativeTaskbarBox, ZoomEnabledBox, ZoomOpaqueBox,
                     MinimizeWindowsBox, DisableMinimizeAnimationsBox, ShowRunningIndicatorsBox,
                     OpenRunningInstancesBox, PopupOnMouseoverBox
                 })
        {
            checkBox.Checked += (_, _) => QueueImmediateApply();
            checkBox.Unchecked += (_, _) => QueueImmediateApply();
        }

        foreach (var slider in new[]
                 {
                     AutoHideDelaySlider, AutoHideDurationSlider, IconSizeSlider, IconOpacitySlider,
                     IconSpacingSlider, IconBottomMarginSlider, ZoomSizeSlider, ZoomRangeSlider,
                     ZoomDurationSlider, BarWidthSlider, BarHeightSlider, OffsetSlider, CenterOffsetSlider,
                     BackgroundOpacitySlider, ShellCornerRadiusSlider, TileCornerRadiusSlider,
                     FontSizeSlider, PopupDelaySlider
                 })
        {
            slider.ValueChanged += (_, _) => QueueImmediateApply();
        }

        foreach (var comboBox in new[]
                 {
                     ImportModeBox, IconQualityBox, HoverEffectBox, MonitorBox,
                     EdgeBox, LayeringBox
                 })
        {
            comboBox.SelectionChanged += (_, _) => QueueImmediateApply();
        }

        ThemeBox.SelectionChanged += (_, _) =>
        {
            if (_isLoadingValues || _isSavingValues || ThemeBox.SelectedValue is not string theme)
            {
                return;
            }

            var shape = DockBarViewModel.GetThemeShape(theme);
            ShellCornerRadiusSlider.Value = shape.ShellCornerRadius;
            TileCornerRadiusSlider.Value = shape.TileCornerRadius;
            QueueImmediateApply();
        };

        foreach (var textBox in new[] { BarNameBox, FontFamilyBox, LabelColorBox })
        {
            textBox.TextChanged += (_, _) => QueueImmediateApply();
        }
    }

    private void QueueImmediateApply()
    {
        if (_isLoadingValues || _isSavingValues)
        {
            return;
        }

        _applyTimer.Stop();
        _applyTimer.Start();
    }

    private void ApplyValuesImmediately()
    {
        if (SaveValues(showErrors: false))
        {
            SettingsApplied?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool SaveValues(bool showErrors = true)
    {
        var app = _store.Current.App;

        if (_isSavingValues)
        {
            return true;
        }

        try
        {
            _isSavingValues = true;
            var oldName = _bar.Name;
            var oldFolder = UserPaths.GetBarFolder(oldName);
            _bar.Name = string.IsNullOrWhiteSpace(BarNameBox.Text) ? "Principal" : BarNameBox.Text.Trim();
            app.RunAtStartup = RunAtStartupBox.IsChecked == true;
            StartupRegistration.SetEnabled(app.RunAtStartup);
            _bar.HideLabels = HideLabelsBox.IsChecked == true;
            _bar.LockItems = LockItemsBox.IsChecked == true;
            _bar.AutoHide = AutoHideBox.IsChecked == true;
            SetWindowsButtonEnabled(WindowsButtonBox.IsChecked == true);
            SetRecycleBinEnabled(RecycleBinBox.IsChecked == true);
            app.HideNativeTaskbar = HideNativeTaskbarBox.IsChecked == true;
            _bar.ImportMode = (DockImportMode)ImportModeBox.SelectedValue;
            _bar.AutoHideDelayMs = (int)AutoHideDelaySlider.Value;
            _bar.AutoHideDurationMs = (int)AutoHideDurationSlider.Value;

            _bar.IconSize = (int)IconSizeSlider.Value;
            _bar.IconOpacity = (int)IconOpacitySlider.Value;
            _bar.IconSpacing = (int)IconSpacingSlider.Value;
            _bar.IconBottomMargin = (int)IconBottomMarginSlider.Value;
            _bar.IconQuality = (IconQuality)IconQualityBox.SelectedValue;
            _bar.ZoomEnabled = ZoomEnabledBox.IsChecked == true;
            _bar.ZoomOpaque = ZoomOpaqueBox.IsChecked == true;
            _bar.ZoomSize = (int)ZoomSizeSlider.Value;
            _bar.ZoomRange = (int)ZoomRangeSlider.Value;
            _bar.ZoomDurationMs = (int)ZoomDurationSlider.Value;
            _bar.HoverEffect = (HoverEffect)HoverEffectBox.SelectedValue;

            _bar.MonitorIndex = (int)MonitorBox.SelectedValue;
            _bar.Edge = (DockEdge)EdgeBox.SelectedValue;
            _bar.Layering = (DockLayering)LayeringBox.SelectedValue;
            _bar.BarWidth = (int)BarWidthSlider.Value;
            _bar.BarHeight = (int)BarHeightSlider.Value;
            _bar.Offset = OffsetSlider.Value;
            _bar.CenterOffset = CenterOffsetSlider.Value;

            _bar.Theme = (string)ThemeBox.SelectedValue;
            _bar.BackgroundOpacity = (int)BackgroundOpacitySlider.Value;
            _bar.ShellCornerRadius = (int)ShellCornerRadiusSlider.Value;
            _bar.TileCornerRadius = (int)TileCornerRadiusSlider.Value;
            _bar.FontFamily = string.IsNullOrWhiteSpace(FontFamilyBox.Text) ? "Segoe UI" : FontFamilyBox.Text.Trim();
            _bar.FontSize = FontSizeSlider.Value;
            _bar.LabelColor = string.IsNullOrWhiteSpace(LabelColorBox.Text) ? "#E8FFFFFF" : LabelColorBox.Text.Trim();

            app.MinimizeWindowsToDock = MinimizeWindowsBox.IsChecked == true;
            app.DisableMinimizeAnimations = DisableMinimizeAnimationsBox.IsChecked == true;
            app.ShowRunningIndicators = ShowRunningIndicatorsBox.IsChecked == true;
            app.OpenRunningInstances = OpenRunningInstancesBox.IsChecked == true;
            app.PopupOnMouseover = PopupOnMouseoverBox.IsChecked == true;
            app.PopupDelayMs = (int)PopupDelaySlider.Value;

            var newFolder = EnsureRenamedBarFolder(oldFolder, _bar.Name);
            if (!string.Equals(oldName, _bar.Name, StringComparison.OrdinalIgnoreCase))
            {
                UpdateItemPaths(oldFolder, newFolder);
                BarFolderBox.Text = newFolder;
            }

            _store.Save();
            return true;
        }
        catch (Exception ex)
        {
            if (showErrors)
            {
                System.Windows.MessageBox.Show(this, ex.Message, UserPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }
        finally
        {
            _isSavingValues = false;
        }
    }

    private static string EnsureRenamedBarFolder(string oldFolder, string newName)
    {
        var newFolder = UserPaths.GetBarFolder(newName);
        if (string.Equals(oldFolder, newFolder, StringComparison.OrdinalIgnoreCase))
        {
            return UserPaths.EnsureBarFolder(newName);
        }

        if (System.IO.Directory.Exists(oldFolder) &&
            !System.IO.File.Exists(newFolder) &&
            !System.IO.Directory.Exists(newFolder))
        {
            System.IO.Directory.Move(oldFolder, newFolder);
            return newFolder;
        }

        return UserPaths.EnsureBarFolder(newName);
    }

    private void UpdateItemPaths(string oldFolder, string newFolder)
    {
        foreach (var item in _bar.Items)
        {
            if (!item.TargetPath.StartsWith(oldFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relative = System.IO.Path.GetRelativePath(oldFolder, item.TargetPath);
            item.TargetPath = System.IO.Path.Combine(newFolder, relative);
        }
    }

    private void SetWindowsButtonEnabled(bool enabled)
    {
        var existing = _bar.Items.FirstOrDefault(static item => item.Kind == DockItemKind.WindowsButton);
        if (enabled && existing is null)
        {
            _bar.Items.Insert(0, DockItem.CreateWindowsButton());
        }
        else if (!enabled && existing is not null)
        {
            _bar.Items.Remove(existing);
        }
    }

    private void SetRecycleBinEnabled(bool enabled)
    {
        var existing = _bar.Items.FirstOrDefault(static item => item.Kind == DockItemKind.RecycleBin);
        if (enabled && existing is null)
        {
            var insertIndex = _bar.Items.FindIndex(static item => item.Kind != DockItemKind.WindowsButton);
            if (insertIndex < 0)
            {
                insertIndex = _bar.Items.Count;
            }

            _bar.Items.Insert(insertIndex, DockItem.CreateRecycleBin());
        }
        else if (!enabled && existing is not null)
        {
            _bar.Items.Remove(existing);
        }
    }

    private static IReadOnlyList<EnumItem<T>> EnumItems<T>() where T : struct, Enum
    {
        var values = (T[])Enum.GetValues(typeof(T));
        var items = new List<EnumItem<T>>();
        foreach (var value in values)
        {
            items.Add(new EnumItem<T>(value, LabelFor(value)));
        }

        return items;
    }

    private static IReadOnlyList<EnumItem<int>> GetMonitorItems()
    {
        var items = new List<EnumItem<int>>();
        var screens = Forms.Screen.AllScreens;
        for (var index = 0; index < screens.Length; index++)
        {
            var screen = screens[index];
            items.Add(new EnumItem<int>(index, $"{index + 1} - {screen.DeviceName}"));
        }

        return items;
    }

    private static string LabelFor<T>(T value)
    {
        return value switch
        {
            DockEdge.Bottom => "Rodape",
            DockEdge.Top => "Topo",
            DockEdge.Left => "Esquerda",
            DockEdge.Right => "Direita",
            DockLayering.TopMost => "Sempre acima",
            DockLayering.Normal => "Normal",
            DockLayering.Bottom => "Sempre abaixo",
            IconQuality.Low => "Baixa",
            IconQuality.Medium => "Media",
            IconQuality.High => "Alta",
            HoverEffect.None => "Nenhum",
            HoverEffect.Bubble => "Bolha",
            HoverEffect.Plateau => "Plato",
            DockImportMode.MoveToBarFolder => "Mover para a pasta da barra",
            DockImportMode.CreateShortcutInBarFolder => "Criar atalho na pasta da barra",
            _ => value?.ToString() ?? ""
        };
    }

    public sealed record EnumItem<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    public sealed class SettingsWindowText
    {
        public SettingsWindowText(DockBarSettings bar)
        {
            BarNameText = $"Barra: {bar.Name}";
            ConfigPathText = UserPaths.ConfigFile;
        }

        public string BarNameText { get; }

        public string ConfigPathText { get; }
    }
}
