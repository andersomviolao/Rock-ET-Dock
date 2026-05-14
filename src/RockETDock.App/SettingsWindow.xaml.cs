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
    private readonly DockBarSettings _bar;
    private readonly DockItemImporter _importer = new();
    private bool _isLoadingValues;
    private bool _isSavingValues;
    private bool _isApplyingSearchFilter;

    public event EventHandler? SettingsApplied;
    public event EventHandler<DockEdge>? CreateBarRequested;

    public SettingsWindow(DockConfigurationStore store, DockBarSettings bar)
    {
        _store = store;
        _bar = bar;

        DataContext = new SettingsWindowText(_store.Current.App, bar, CurrentText);
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

        AppDisplayNameBox.Text = GetAppDisplayName(app);
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

    private void CreateLeftDock_Click(object sender, RoutedEventArgs e)
    {
        CreateBarRequested?.Invoke(this, DockEdge.Left);
    }

    private void CreateRightDock_Click(object sender, RoutedEventArgs e)
    {
        CreateBarRequested?.Invoke(this, DockEdge.Right);
    }

    private void CreateTopDock_Click(object sender, RoutedEventArgs e)
    {
        CreateBarRequested?.Invoke(this, DockEdge.Top);
    }

    private void CreateBottomDock_Click(object sender, RoutedEventArgs e)
    {
        CreateBarRequested?.Invoke(this, DockEdge.Bottom);
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
            app.DisplayName = string.IsNullOrWhiteSpace(AppDisplayNameBox.Text) ? UserPaths.AppName : AppDisplayNameBox.Text.Trim();
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
                DataContext = new SettingsWindowText(app, _bar, CurrentText);
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
            DataContext = new SettingsWindowText(_store.Current.App, _bar, text);
            LanguageBox.ItemsSource = TextCatalog.LanguageOptions;
            LanguageBox.SelectedValue = text.LanguageCode;
            SetEnumItems(MoveModifierBox, EnumItems<DockMoveModifierKey>(text), _bar.MoveModifierKey);
            SetEnumItems(GifModifierBox, EnumItems<DockMoveModifierKey>(text), _bar.GifModifierKey);
            SetEnumItems(IconQualityBox, EnumItems<IconQuality>(text), _bar.IconQuality);
            SetEnumItems(HoverEffectBox, EnumItems<HoverEffect>(text), _bar.HoverEffect);
            SetEnumItems(EdgeBox, EnumItems<DockEdge>(text), _bar.Edge);
            SetEnumItems(LayeringBox, EnumItems<DockLayering>(text), _bar.Layering);
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
