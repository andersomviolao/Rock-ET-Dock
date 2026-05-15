using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RockETDock.App.Models;
using RockETDock.App.Services;
using RockETDock.App.ViewModels;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace RockETDock.App;

public partial class SettingsWindow : Window
{
    private readonly DockConfigurationStore _store;
    private readonly DockItemImporter _importer = new();
    private DockBarSettings _selectedBar;
    private bool _isLoadingValues;
    private bool _isSavingValues;
    private bool _isApplyingSearchFilter;

    public event EventHandler<SettingsAppliedEventArgs>? SettingsApplied;
    public event EventHandler<DockBarCreationEventArgs>? CreateBarRequested;

    public SettingsWindow(DockConfigurationStore store, DockBarSettings bar)
    {
        _store = store;
        _selectedBar = ResolveBar(bar);

        DataContext = new SettingsWindowText(_store.Current.App, _selectedBar, CurrentText);
        InitializeComponent();
        ApplySystemTheme();
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        _isLoadingValues = true;
        LoadValues();
        _isLoadingValues = false;
        AttachImmediateSaveHandlers();
    }

    private void LoadValues()
    {
        var app = _store.Current.App;

        RefreshBarSelector();
        AppDisplayNameBox.Text = GetAppDisplayName(app);
        BarNameBox.Text = _selectedBar.Name;
        RunAtStartupBox.IsChecked = app.RunAtStartup;
        HideLabelsBox.IsChecked = _selectedBar.HideLabels;
        LockItemsBox.IsChecked = _selectedBar.LockItems;
        AutoHideBox.IsChecked = _selectedBar.AutoHide;
        WindowsButtonBox.IsChecked = _selectedBar.Items.Any(static item => item.Kind == DockItemKind.WindowsButton);
        RecycleBinBox.IsChecked = _selectedBar.Items.Any(static item => item.Kind == DockItemKind.RecycleBin);
        HideNativeTaskbarBox.IsChecked = app.HideNativeTaskbar;
        AutoHideDelaySlider.Value = _selectedBar.AutoHideDelayMs;
        AutoHideDurationSlider.Value = _selectedBar.AutoHideDurationMs;
        BarFolderBox.Text = UserPaths.EnsureBarFolder(_selectedBar.Name);

        IconSizeSlider.Value = _selectedBar.IconSize;
        IconOpacitySlider.Value = _selectedBar.IconOpacity;
        IconSpacingSlider.Value = _selectedBar.IconSpacing;
        IconBottomMarginSlider.Value = _selectedBar.IconBottomMargin;
        ZoomEnabledBox.IsChecked = _selectedBar.ZoomEnabled;
        ZoomOpaqueBox.IsChecked = _selectedBar.ZoomOpaque;
        ZoomSizeSlider.Value = _selectedBar.ZoomSize;
        ZoomRangeSlider.Value = _selectedBar.ZoomRange;
        ZoomDurationSlider.Value = _selectedBar.ZoomDurationMs;

        MonitorBox.ItemsSource = GetMonitorItems();
        MonitorBox.SelectedValue = _selectedBar.MonitorIndex;
        if (MonitorBox.SelectedValue is null)
        {
            MonitorBox.SelectedIndex = 0;
        }
        BarWidthSlider.Value = _selectedBar.BarWidth;
        BarHeightSlider.Value = _selectedBar.BarHeight;
        OffsetSlider.Value = _selectedBar.Offset;
        CenterOffsetSlider.Value = _selectedBar.CenterOffset;

        ThemeBox.ItemsSource = DockBarViewModel.ThemeNames;
        ThemeBox.SelectedValue = _selectedBar.Theme;
        if (ThemeBox.SelectedValue is null)
        {
            ThemeBox.SelectedIndex = 0;
        }
        var selectedTheme = ThemeBox.SelectedValue as string ?? _selectedBar.Theme;
        var themeShape = DockBarViewModel.GetThemeShape(selectedTheme);
        BackgroundOpacitySlider.Value = _selectedBar.BackgroundOpacity;
        ShellCornerRadiusSlider.Value = _selectedBar.ShellCornerRadius >= 0 ? _selectedBar.ShellCornerRadius : themeShape.ShellCornerRadius;
        TileCornerRadiusSlider.Value = _selectedBar.TileCornerRadius >= 0 ? _selectedBar.TileCornerRadius : themeShape.TileCornerRadius;
        FontFamilyBox.Text = _selectedBar.FontFamily;
        FontSizeSlider.Value = _selectedBar.FontSize;
        LabelColorBox.Text = _selectedBar.LabelColor;

        MinimizeWindowsBox.IsChecked = app.MinimizeWindowsToDock;
        DisableMinimizeAnimationsBox.IsChecked = app.DisableMinimizeAnimations;
        ShowRunningIndicatorsBox.IsChecked = app.ShowRunningIndicators;
        OpenRunningInstancesBox.IsChecked = app.OpenRunningInstances;
        PopupOnMouseoverBox.IsChecked = app.PopupOnMouseover;
        PopupDelaySlider.Value = app.PopupDelayMs;

        ApplyLocalizedValues();
    }

    private DockBarSettings ResolveBar(DockBarSettings requestedBar)
    {
        return _store.Current.Bars.FirstOrDefault(bar =>
                   string.Equals(bar.Id, requestedBar.Id, StringComparison.OrdinalIgnoreCase)) ??
               _store.Current.Bars.FirstOrDefault() ??
               requestedBar;
    }

    private void RefreshBarSelector()
    {
        if (!IsInitialized)
        {
            return;
        }

        var configuration = _store.Current;
        BarSelectorBox.ItemsSource = null;
        BarSelectorBox.ItemsSource = configuration.Bars;
        BarSelectorBox.SelectedItem = _selectedBar;

        var text = CurrentText;
        BarLimitText.Text = string.Format(CultureInfo.InvariantCulture, text["SettingsDockLimitStatus"], configuration.Bars.Count, DockConfiguration.MaxBars);
        var canCreateBar = configuration.CanCreateBar;
        foreach (var button in new[] { CreateLeftDockButton, CreateRightDockButton, CreateTopDockButton, CreateBottomDockButton })
        {
            button.IsEnabled = canCreateBar;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void SettingsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySearchFilter();
    }

    private void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == SettingsTabs)
        {
            ApplySearchFilter();
        }
    }

    private void BarSelectorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingValues || BarSelectorBox.SelectedItem is not DockBarSettings selectedBar ||
            string.Equals(selectedBar.Id, _selectedBar.Id, StringComparison.Ordinal))
        {
            return;
        }

        _selectedBar = selectedBar;
        _isLoadingValues = true;
        try
        {
            LoadValues();
        }
        finally
        {
            _isLoadingValues = false;
        }
    }

    private void CreateLeftDock_Click(object sender, RoutedEventArgs e)
    {
        CreateDock(DockEdge.Left);
    }

    private void CreateRightDock_Click(object sender, RoutedEventArgs e)
    {
        CreateDock(DockEdge.Right);
    }

    private void CreateTopDock_Click(object sender, RoutedEventArgs e)
    {
        CreateDock(DockEdge.Top);
    }

    private void CreateBottomDock_Click(object sender, RoutedEventArgs e)
    {
        CreateDock(DockEdge.Bottom);
    }

    private void CreateDock(DockEdge edge)
    {
        var text = CurrentText;
        if (!_store.Current.CanCreateBar)
        {
            System.Windows.MessageBox.Show(this, text["SettingsDockLimitReached"], UserPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshBarSelector();
            return;
        }

        var args = new DockBarCreationEventArgs(edge);
        CreateBarRequested?.Invoke(this, args);
        if (args.CreatedBar is null)
        {
            RefreshBarSelector();
            return;
        }

        _selectedBar = ResolveBar(args.CreatedBar);
        _isLoadingValues = true;
        try
        {
            LoadValues();
        }
        finally
        {
            _isLoadingValues = false;
        }

        SettingsApplied?.Invoke(this, new SettingsAppliedEventArgs(_selectedBar));
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
            foreach (var item in _importer.ImportAnimatedGifs(_selectedBar, dialog.FileNames))
            {
                _selectedBar.Items.Add(item);
            }

            _store.Save();
            SettingsApplied?.Invoke(this, new SettingsAppliedEventArgs(_selectedBar));
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

        foreach (var textBox in new[] { AppDisplayNameBox, BarNameBox, FontFamilyBox, LabelColorBox })
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
            SettingsApplied?.Invoke(this, new SettingsAppliedEventArgs(_selectedBar));
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
            var oldName = _selectedBar.Name;
            var oldFolder = UserPaths.GetBarFolder(oldName);
            var selectedLanguage = TextCatalog.NormalizeLanguage(LanguageBox.SelectedValue as string ?? app.Language);
            var languageChanged = !string.Equals(app.Language, selectedLanguage, StringComparison.OrdinalIgnoreCase);
            app.Language = selectedLanguage;
            app.DisplayName = string.IsNullOrWhiteSpace(AppDisplayNameBox.Text) ? UserPaths.AppName : AppDisplayNameBox.Text.Trim();
            _selectedBar.Name = string.IsNullOrWhiteSpace(BarNameBox.Text) ? CurrentText["SettingsBarPrefix"] : BarNameBox.Text.Trim();
            app.RunAtStartup = RunAtStartupBox.IsChecked == true;
            StartupRegistration.SetEnabled(app.RunAtStartup);
            _selectedBar.HideLabels = HideLabelsBox.IsChecked == true;
            _selectedBar.LockItems = LockItemsBox.IsChecked == true;
            _selectedBar.AutoHide = AutoHideBox.IsChecked == true;
            SetWindowsButtonEnabled(WindowsButtonBox.IsChecked == true);
            SetRecycleBinEnabled(RecycleBinBox.IsChecked == true);
            app.HideNativeTaskbar = HideNativeTaskbarBox.IsChecked == true;
            _selectedBar.ImportMode = DockImportMode.CreateShortcutInBarFolder;
            _selectedBar.MoveModifierKey = SelectedEnum(MoveModifierBox, _selectedBar.MoveModifierKey);
            _selectedBar.GifModifierKey = SelectedEnum(GifModifierBox, _selectedBar.GifModifierKey);
            _selectedBar.AutoHideDelayMs = (int)AutoHideDelaySlider.Value;
            _selectedBar.AutoHideDurationMs = (int)AutoHideDurationSlider.Value;

            _selectedBar.IconSize = (int)IconSizeSlider.Value;
            _selectedBar.IconOpacity = (int)IconOpacitySlider.Value;
            _selectedBar.IconSpacing = (int)IconSpacingSlider.Value;
            _selectedBar.IconBottomMargin = (int)IconBottomMarginSlider.Value;
            _selectedBar.IconQuality = SelectedEnum(IconQualityBox, _selectedBar.IconQuality);
            _selectedBar.ZoomEnabled = ZoomEnabledBox.IsChecked == true;
            _selectedBar.ZoomOpaque = ZoomOpaqueBox.IsChecked == true;
            _selectedBar.ZoomSize = (int)ZoomSizeSlider.Value;
            _selectedBar.ZoomRange = (int)ZoomRangeSlider.Value;
            _selectedBar.ZoomDurationMs = (int)ZoomDurationSlider.Value;
            _selectedBar.HoverEffect = SelectedEnum(HoverEffectBox, _selectedBar.HoverEffect);

            _selectedBar.MonitorIndex = (int)MonitorBox.SelectedValue;
            _selectedBar.Edge = SelectedEnum(EdgeBox, _selectedBar.Edge);
            _selectedBar.Layering = SelectedEnum(LayeringBox, _selectedBar.Layering);
            _selectedBar.BarWidth = (int)BarWidthSlider.Value;
            _selectedBar.BarHeight = (int)BarHeightSlider.Value;
            _selectedBar.Offset = OffsetSlider.Value;
            _selectedBar.CenterOffset = CenterOffsetSlider.Value;

            _selectedBar.Theme = (string)ThemeBox.SelectedValue;
            _selectedBar.BackgroundOpacity = (int)BackgroundOpacitySlider.Value;
            _selectedBar.ShellCornerRadius = (int)ShellCornerRadiusSlider.Value;
            _selectedBar.TileCornerRadius = (int)TileCornerRadiusSlider.Value;
            _selectedBar.FontFamily = string.IsNullOrWhiteSpace(FontFamilyBox.Text) ? "Segoe UI" : FontFamilyBox.Text.Trim();
            _selectedBar.FontSize = FontSizeSlider.Value;
            _selectedBar.LabelColor = string.IsNullOrWhiteSpace(LabelColorBox.Text) ? "#E8FFFFFF" : LabelColorBox.Text.Trim();

            app.MinimizeWindowsToDock = MinimizeWindowsBox.IsChecked == true;
            app.DisableMinimizeAnimations = DisableMinimizeAnimationsBox.IsChecked == true;
            app.ShowRunningIndicators = ShowRunningIndicatorsBox.IsChecked == true;
            app.OpenRunningInstances = OpenRunningInstancesBox.IsChecked == true;
            app.PopupOnMouseover = PopupOnMouseoverBox.IsChecked == true;
            app.PopupDelayMs = (int)PopupDelaySlider.Value;

            var newFolder = EnsureRenamedBarFolder(oldFolder, _selectedBar.Name);
            if (!string.Equals(oldName, _selectedBar.Name, StringComparison.OrdinalIgnoreCase))
            {
                UpdateItemPaths(oldFolder, newFolder);
                BarFolderBox.Text = newFolder;
                RefreshBarSelector();
            }

            _store.Save();
            if (languageChanged)
            {
                ApplyLocalizedValues();
            }
            else
            {
                DataContext = new SettingsWindowText(app, _selectedBar, CurrentText);
            }

            ApplySearchFilter();

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
        foreach (var item in _selectedBar.Items)
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
        var existing = _selectedBar.Items.FirstOrDefault(static item => item.Kind == DockItemKind.WindowsButton);
        if (enabled && existing is null)
        {
            _selectedBar.Items.Insert(0, DockItem.CreateWindowsButton());
        }
        else if (!enabled && existing is not null)
        {
            _selectedBar.Items.Remove(existing);
        }
    }

    private void SetRecycleBinEnabled(bool enabled)
    {
        var existing = _selectedBar.Items.FirstOrDefault(static item => item.Kind == DockItemKind.RecycleBin);
        if (enabled && existing is null)
        {
            var insertIndex = _selectedBar.Items.FindIndex(static item => item.Kind != DockItemKind.WindowsButton);
            if (insertIndex < 0)
            {
                insertIndex = _selectedBar.Items.Count;
            }

            _selectedBar.Items.Insert(insertIndex, DockItem.CreateRecycleBin(CurrentText["ItemRecycleBin"]));
        }
        else if (!enabled && existing is not null)
        {
            _selectedBar.Items.Remove(existing);
        }
    }

    private void ApplyLocalizedValues()
    {
        var wasLoadingValues = _isLoadingValues;
        _isLoadingValues = true;

        try
        {
            var text = CurrentText;
            DataContext = new SettingsWindowText(_store.Current.App, _selectedBar, text);
            LanguageBox.ItemsSource = TextCatalog.LanguageOptions;
            LanguageBox.SelectedValue = text.LanguageCode;
            SetEnumItems(MoveModifierBox, EnumItems<DockMoveModifierKey>(text), _selectedBar.MoveModifierKey);
            SetEnumItems(GifModifierBox, EnumItems<DockMoveModifierKey>(text), _selectedBar.GifModifierKey);
            SetEnumItems(IconQualityBox, EnumItems<IconQuality>(text), _selectedBar.IconQuality);
            SetEnumItems(HoverEffectBox, EnumItems<HoverEffect>(text), _selectedBar.HoverEffect);
            SetEnumItems(EdgeBox, EnumItems<DockEdge>(text), _selectedBar.Edge);
            SetEnumItems(LayeringBox, EnumItems<DockLayering>(text), _selectedBar.Layering);
            RefreshBarSelector();
            ApplySearchFilter();
        }
        finally
        {
            _isLoadingValues = wasLoadingValues;
        }
    }

    private void ApplySearchFilter()
    {
        if (_isApplyingSearchFilter || !IsInitialized)
        {
            return;
        }

        var query = NormalizeSearchText(SettingsSearchBox.Text);
        var firstMatchingTab = (TabItem?)null;
        var selectedTabHasMatches = true;

        foreach (var tab in SettingsTabs.Items.OfType<TabItem>())
        {
            var tabHasMatches = ApplySearchFilter(tab.Content as DependencyObject, query);
            if (query.Length > 0 && tabHasMatches && firstMatchingTab is null)
            {
                firstMatchingTab = tab;
            }

            if (ReferenceEquals(SettingsTabs.SelectedItem, tab))
            {
                selectedTabHasMatches = tabHasMatches;
            }
        }

        if (query.Length == 0 || selectedTabHasMatches || firstMatchingTab is null)
        {
            return;
        }

        try
        {
            _isApplyingSearchFilter = true;
            SettingsTabs.SelectedItem = firstMatchingTab;
        }
        finally
        {
            _isApplyingSearchFilter = false;
        }
    }

    private bool ApplySearchFilter(DependencyObject? root, string query)
    {
        if (root is null)
        {
            return query.Length == 0;
        }

        var hasQuery = query.Length > 0;
        var hasVisibleMatch = false;
        foreach (var card in LogicalDescendants<Border>(root).Where(IsSearchableSettingsCard))
        {
            var visible = !hasQuery || NormalizeSearchText(GetSearchableText(card)).Contains(query, StringComparison.Ordinal);
            card.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            hasVisibleMatch |= visible;
        }

        foreach (var title in LogicalDescendants<TextBlock>(root).Where(IsSectionTitle))
        {
            title.Visibility = hasQuery ? Visibility.Collapsed : Visibility.Visible;
        }

        return !hasQuery || hasVisibleMatch;
    }

    private bool IsSearchableSettingsCard(Border border)
    {
        return ReferenceEquals(border.Style, FindResource("SettingsRowCard")) ||
               ReferenceEquals(border.Style, FindResource("SettingsPanelCard"));
    }

    private bool IsSectionTitle(TextBlock textBlock)
    {
        return ReferenceEquals(textBlock.Style, FindResource("SectionTitleText"));
    }

    private static string GetSearchableText(DependencyObject root)
    {
        var builder = new StringBuilder();
        foreach (var textBlock in LogicalDescendants<TextBlock>(root))
        {
            builder.Append(' ');
            builder.Append(textBlock.Text);
        }

        foreach (var textBox in LogicalDescendants<TextBox>(root))
        {
            builder.Append(' ');
            builder.Append(textBox.Text);
        }

        foreach (var contentControl in LogicalDescendants<ContentControl>(root))
        {
            if (contentControl.Content is string text)
            {
                builder.Append(' ');
                builder.Append(text);
            }
        }

        return builder.ToString();
    }

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    private static IEnumerable<T> LogicalDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            if (child is DependencyObject dependencyObject)
            {
                foreach (var descendant in LogicalDescendants<T>(dependencyObject))
                {
                    yield return descendant;
                }
            }
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

    private static string GetAppDisplayName(ApplicationSettings app)
    {
        return string.IsNullOrWhiteSpace(app.DisplayName)
            ? UserPaths.AppName
            : app.DisplayName.Trim();
    }

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

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        base.OnClosed(e);
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Color or UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
        {
            Dispatcher.Invoke(ApplySystemTheme);
        }
    }

    private void ApplySystemTheme()
    {
        if (IsWindowsLightTheme())
        {
            SetThemeBrushes(
                background: Color.FromRgb(243, 243, 243),
                sidebar: Color.FromRgb(243, 243, 243),
                card: Color.FromRgb(255, 255, 255),
                cardHover: Color.FromRgb(249, 249, 249),
                text: Color.FromRgb(28, 28, 28),
                secondaryText: Color.FromRgb(91, 91, 91),
                border: Color.FromRgb(225, 225, 225),
                selectedNav: Color.FromRgb(232, 232, 232),
                input: Color.FromRgb(255, 255, 255),
                button: Color.FromRgb(251, 251, 251),
                accent: Color.FromRgb(0, 103, 192));
            return;
        }

        SetThemeBrushes(
            background: Color.FromRgb(32, 32, 32),
            sidebar: Color.FromRgb(32, 32, 32),
            card: Color.FromRgb(43, 43, 43),
            cardHover: Color.FromRgb(50, 50, 50),
            text: Color.FromRgb(255, 255, 255),
            secondaryText: Color.FromRgb(200, 200, 200),
            border: Color.FromRgb(58, 58, 58),
            selectedNav: Color.FromRgb(45, 45, 45),
            input: Color.FromRgb(43, 43, 43),
            button: Color.FromRgb(51, 51, 51),
            accent: Color.FromRgb(76, 194, 255));
    }

    private void SetThemeBrushes(
        Color background,
        Color sidebar,
        Color card,
        Color cardHover,
        Color text,
        Color secondaryText,
        Color border,
        Color selectedNav,
        Color input,
        Color button,
        Color accent)
    {
        Resources["SettingsBackgroundBrush"] = new SolidColorBrush(background);
        Resources["SettingsSidebarBrush"] = new SolidColorBrush(sidebar);
        Resources["SettingsCardBrush"] = new SolidColorBrush(card);
        Resources["SettingsCardHoverBrush"] = new SolidColorBrush(cardHover);
        Resources["SettingsTextBrush"] = new SolidColorBrush(text);
        Resources["SettingsSecondaryTextBrush"] = new SolidColorBrush(secondaryText);
        Resources["SettingsBorderBrush"] = new SolidColorBrush(border);
        Resources["SettingsSelectedNavBrush"] = new SolidColorBrush(selectedNav);
        Resources["SettingsInputBrush"] = new SolidColorBrush(input);
        Resources["SettingsButtonBrush"] = new SolidColorBrush(button);
        Resources["SettingsAccentBrush"] = new SolidColorBrush(accent);
    }

    private static bool IsWindowsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch
        {
            return true;
        }
    }

    public sealed record EnumItem<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    public sealed class SettingsAppliedEventArgs(DockBarSettings bar) : EventArgs
    {
        public DockBarSettings Bar { get; } = bar;
    }

    public sealed class DockBarCreationEventArgs(DockEdge edge) : EventArgs
    {
        public DockEdge Edge { get; } = edge;

        public DockBarSettings? CreatedBar { get; set; }
    }

    public sealed class SettingsWindowText
    {
        public SettingsWindowText(ApplicationSettings app, DockBarSettings bar, LocalizedText text)
        {
            Text = text;
            AppDisplayName = GetAppDisplayName(app);
            WindowTitleText = $"{AppDisplayName} - {text["SettingsTitle"]}";
            BarNameText = $"{text["SettingsBarPrefix"]}: {bar.Name}";
            ConfigPathText = UserPaths.ConfigFile;
        }

        public LocalizedText Text { get; }

        public string AppDisplayName { get; }

        public string WindowTitleText { get; }

        public string BarNameText { get; }

        public string ConfigPathText { get; }
    }
}
