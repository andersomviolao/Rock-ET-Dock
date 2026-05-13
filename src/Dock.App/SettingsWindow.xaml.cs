using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
    private bool _isLoadingValues;
    private bool _isSavingValues;

    public event EventHandler? SettingsApplied;

    public SettingsWindow(DockConfigurationStore store, DockBarSettings bar)
    {
        _store = store;
        _bar = bar;

        DataContext = new SettingsWindowText(bar, CurrentText);
        InitializeComponent();
        _isLoadingValues = true;
        LoadValues();
        _isLoadingValues = false;
        AttachImmediateSaveHandlers();
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
        AutoHideDelaySlider.Value = _bar.AutoHideDelayMs;
        AutoHideDurationSlider.Value = _bar.AutoHideDurationMs;
        BarFolderBox.Text = UserPaths.EnsureBarFolder(_bar.Name);

        IconSizeSlider.Value = _bar.IconSize;
        IconOpacitySlider.Value = _bar.IconOpacity;
        IconSpacingSlider.Value = _bar.IconSpacing;
        IconBottomMarginSlider.Value = _bar.IconBottomMargin;
        ZoomEnabledBox.IsChecked = _bar.ZoomEnabled;
        ZoomOpaqueBox.IsChecked = _bar.ZoomOpaque;
        ZoomSizeSlider.Value = _bar.ZoomSize;
        ZoomRangeSlider.Value = _bar.ZoomRange;
        ZoomDurationSlider.Value = _bar.ZoomDurationMs;

        MonitorBox.ItemsSource = GetMonitorItems();
        MonitorBox.SelectedValue = _bar.MonitorIndex;
        if (MonitorBox.SelectedValue is null)
        {
            MonitorBox.SelectedIndex = 0;
        }
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

        ApplyLocalizedValues();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AddGif_Click(object sender, RoutedEventArgs e)
    {
        var text = CurrentText;
        var dialog = new OpenFileDialog
        {
            Title = text["DialogAddGifTitle"],
            Filter = text["DialogAddGifFilter"],
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

    private void AttachImmediateSaveHandlers()
    {
        foreach (var checkBox in new[]
                 {
                     RunAtStartupBox, HideLabelsBox, LockItemsBox, AutoHideBox, WindowsButtonBox,
                     RecycleBinBox, HideNativeTaskbarBox, ZoomEnabledBox, ZoomOpaqueBox,
                     MinimizeWindowsBox, DisableMinimizeAnimationsBox, ShowRunningIndicatorsBox,
                     OpenRunningInstancesBox, PopupOnMouseoverBox
                 })
        {
            checkBox.Checked += (_, _) => SaveImmediately();
            checkBox.Unchecked += (_, _) => SaveImmediately();
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
            slider.ValueChanged += (_, _) => SaveImmediately();
        }

        foreach (var comboBox in new[]
                 {
                     LanguageBox, MoveModifierBox, GifModifierBox, IconQualityBox, HoverEffectBox, MonitorBox,
                     EdgeBox, LayeringBox
                 })
        {
            comboBox.SelectionChanged += (_, _) => SaveImmediately();
        }

        ThemeBox.SelectionChanged += (_, _) =>
        {
            if (_isLoadingValues || _isSavingValues || ThemeBox.SelectedValue is not string theme)
            {
                return;
            }

            var shape = DockBarViewModel.GetThemeShape(theme);
            _isLoadingValues = true;
            try
            {
                ShellCornerRadiusSlider.Value = shape.ShellCornerRadius;
                TileCornerRadiusSlider.Value = shape.TileCornerRadius;
            }
            finally
            {
                _isLoadingValues = false;
            }

            SaveImmediately();
        };

        foreach (var textBox in new[] { BarNameBox, FontFamilyBox, LabelColorBox })
        {
            textBox.TextChanged += (_, _) => SaveImmediately();
        }
    }

    private void SaveImmediately()
    {
        if (_isLoadingValues || _isSavingValues)
        {
            return;
        }

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
            var selectedLanguage = TextCatalog.NormalizeLanguage(LanguageBox.SelectedValue as string ?? app.Language);
            var languageChanged = !string.Equals(app.Language, selectedLanguage, StringComparison.OrdinalIgnoreCase);
            app.Language = selectedLanguage;
            _bar.Name = string.IsNullOrWhiteSpace(BarNameBox.Text) ? CurrentText["SettingsBarPrefix"] : BarNameBox.Text.Trim();
            app.RunAtStartup = RunAtStartupBox.IsChecked == true;
            StartupRegistration.SetEnabled(app.RunAtStartup);
            _bar.HideLabels = HideLabelsBox.IsChecked == true;
            _bar.LockItems = LockItemsBox.IsChecked == true;
            _bar.AutoHide = AutoHideBox.IsChecked == true;
            SetWindowsButtonEnabled(WindowsButtonBox.IsChecked == true);
            SetRecycleBinEnabled(RecycleBinBox.IsChecked == true);
            app.HideNativeTaskbar = HideNativeTaskbarBox.IsChecked == true;
            _bar.ImportMode = DockImportMode.CreateShortcutInBarFolder;
            _bar.MoveModifierKey = SelectedEnum(MoveModifierBox, _bar.MoveModifierKey);
            _bar.GifModifierKey = SelectedEnum(GifModifierBox, _bar.GifModifierKey);
            _bar.AutoHideDelayMs = (int)AutoHideDelaySlider.Value;
            _bar.AutoHideDurationMs = (int)AutoHideDurationSlider.Value;

            _bar.IconSize = (int)IconSizeSlider.Value;
            _bar.IconOpacity = (int)IconOpacitySlider.Value;
            _bar.IconSpacing = (int)IconSpacingSlider.Value;
            _bar.IconBottomMargin = (int)IconBottomMarginSlider.Value;
            _bar.IconQuality = SelectedEnum(IconQualityBox, _bar.IconQuality);
            _bar.ZoomEnabled = ZoomEnabledBox.IsChecked == true;
            _bar.ZoomOpaque = ZoomOpaqueBox.IsChecked == true;
            _bar.ZoomSize = (int)ZoomSizeSlider.Value;
            _bar.ZoomRange = (int)ZoomRangeSlider.Value;
            _bar.ZoomDurationMs = (int)ZoomDurationSlider.Value;
            _bar.HoverEffect = SelectedEnum(HoverEffectBox, _bar.HoverEffect);

            _bar.MonitorIndex = (int)MonitorBox.SelectedValue;
            _bar.Edge = SelectedEnum(EdgeBox, _bar.Edge);
            _bar.Layering = SelectedEnum(LayeringBox, _bar.Layering);
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
            if (languageChanged)
            {
                ApplyLocalizedValues();
            }
            else
            {
                DataContext = new SettingsWindowText(_bar, CurrentText);
            }

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

            _bar.Items.Insert(insertIndex, DockItem.CreateRecycleBin(CurrentText["ItemRecycleBin"]));
        }
        else if (!enabled && existing is not null)
        {
            _bar.Items.Remove(existing);
        }
    }

    private void ApplyLocalizedValues()
    {
        var wasLoadingValues = _isLoadingValues;
        _isLoadingValues = true;

        try
        {
            var text = CurrentText;
            DataContext = new SettingsWindowText(_bar, text);
            LanguageBox.ItemsSource = TextCatalog.LanguageOptions;
            LanguageBox.SelectedValue = text.LanguageCode;
            SetEnumItems(MoveModifierBox, EnumItems<DockMoveModifierKey>(text), _bar.MoveModifierKey);
            SetEnumItems(GifModifierBox, EnumItems<DockMoveModifierKey>(text), _bar.GifModifierKey);
            SetEnumItems(IconQualityBox, EnumItems<IconQuality>(text), _bar.IconQuality);
            SetEnumItems(HoverEffectBox, EnumItems<HoverEffect>(text), _bar.HoverEffect);
            SetEnumItems(EdgeBox, EnumItems<DockEdge>(text), _bar.Edge);
            SetEnumItems(LayeringBox, EnumItems<DockLayering>(text), _bar.Layering);
        }
        finally
        {
            _isLoadingValues = wasLoadingValues;
        }
    }

    private static void SetEnumItems<T>(ComboBox comboBox, IReadOnlyList<EnumItem<T>> items, T selectedValue)
        where T : struct, Enum
    {
        comboBox.ItemsSource = items;
        comboBox.SelectedValue = selectedValue;
    }

    private static T SelectedEnum<T>(ComboBox comboBox, T fallback)
        where T : struct, Enum
    {
        return comboBox.SelectedValue is T value ? value : fallback;
    }

    private LocalizedText CurrentText => TextCatalog.Get(_store.Current.App.Language);

    private static IReadOnlyList<EnumItem<T>> EnumItems<T>(LocalizedText text) where T : struct, Enum
    {
        var values = (T[])Enum.GetValues(typeof(T));
        var items = new List<EnumItem<T>>();
        foreach (var value in values)
        {
            items.Add(new EnumItem<T>(value, text.LabelFor(value)));
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

    public sealed record EnumItem<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    public sealed class SettingsWindowText
    {
        public SettingsWindowText(DockBarSettings bar, LocalizedText text)
        {
            Text = text;
            BarNameText = $"{text["SettingsBarPrefix"]}: {bar.Name}";
            ConfigPathText = UserPaths.ConfigFile;
        }

        public LocalizedText Text { get; }

        public string BarNameText { get; }

        public string ConfigPathText { get; }
    }
}
