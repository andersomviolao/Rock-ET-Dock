using RockETDock.App.Models;
using RockETDock.App.Services;
using RockETDock.App.ViewModels;

var areaLeft = 0d;
var areaTop = 0d;
var areaWidth = 1920d;
var areaHeight = 1040d;
var itemButtonSize = 64d;
var overhang = 26d;
var edgeDistance = 35d;
var centerOffset = 120d;

foreach (var edge in Enum.GetValues<DockEdge>())
{
    var placement = DockGeometry.Calculate(new DockGeometryInput(
        edge,
        areaLeft,
        areaTop,
        areaWidth,
        areaHeight,
        ItemCount: 5,
        itemButtonSize,
        IconSpacing: 8,
        overhang,
        overhang,
        edgeDistance,
        centerOffset,
        BarWidth: 0,
        BarHeight: 0));

    Validate(edge, placement);
}

ValidateReorder();
ValidateRemoveItem();
ValidateSpecialItems();
ValidateSpecialCommandItems();
ValidateDefaultDockItems();
ValidateLegacySpecialItemsAreRemovedOnLoad();
ValidateRunningApplicationMapping();
ValidateWindowsShellCommandsBypassRunningActivation();
ValidateDropPlaceholder();
ValidateImportModes();
ValidateAnimatedGifImport();
ValidateExportToDesktop();
ValidateCompactIconSizing();
ValidateNeighborZoomScale();
ValidateHoverZoomOffsets();
ValidateHoverOverhangPreventsEdgeClipping();
ValidateCustomBarGeometry();
ValidateSliderGeometryExtremes();
ValidatePlaceholderIsGapOnly();
ValidateLocalization();
ValidateDockLimit();
ValidateVerticalDockGeometryAndOrientation();
ValidateTransparentBackgroundOpacity();

Console.WriteLine("Dock geometry checks passed for Top, Bottom, Left and Right.");
Console.WriteLine("Dock reorder checks passed.");
Console.WriteLine("Dock desktop export checks passed.");
Console.WriteLine("Dock import, placeholder and GIF checks passed.");
Console.WriteLine("Dock special command and running-app checks passed.");
Console.WriteLine("Dock default item checks passed.");
Console.WriteLine("Dock legacy special item migration checks passed.");
Console.WriteLine("Dock Windows shell launch checks passed.");
Console.WriteLine("Dock compact icon sizing checks passed.");
Console.WriteLine("Dock neighbor zoom checks passed.");
Console.WriteLine("Dock hover zoom offset checks passed.");
Console.WriteLine("Dock hover overhang checks passed.");
Console.WriteLine("Dock custom bar sizing checks passed.");
Console.WriteLine("Dock slider geometry checks passed.");
Console.WriteLine("Dock placeholder gap checks passed.");
Console.WriteLine("Dock localization checks passed.");
Console.WriteLine("Dock limit checks passed.");
Console.WriteLine("Dock vertical edge checks passed.");
Console.WriteLine("Dock transparent background checks passed.");

void Validate(DockEdge edge, DockPlacement placement)
{
    var shellLeft = placement.WindowLeft + overhang;
    var shellTop = placement.WindowTop + overhang;

    switch (edge)
    {
        case DockEdge.Bottom:
            AssertNear(areaTop + areaHeight - placement.ShellHeight - (8 + edgeDistance), shellTop, "bottom edge distance");
            AssertNear(areaLeft + (areaWidth - placement.ShellWidth) / 2 + centerOffset, shellLeft, "bottom center offset");
            break;
        case DockEdge.Top:
            AssertNear(areaTop + 8 + edgeDistance, shellTop, "top edge distance");
            AssertNear(areaLeft + (areaWidth - placement.ShellWidth) / 2 + centerOffset, shellLeft, "top center offset");
            break;
        case DockEdge.Left:
            AssertNear(areaLeft + 8 + edgeDistance, shellLeft, "left edge distance");
            AssertNear(areaTop + (areaHeight - placement.ShellHeight) / 2 + centerOffset, shellTop, "left center offset");
            break;
        case DockEdge.Right:
            AssertNear(areaLeft + areaWidth - placement.ShellWidth - (8 + edgeDistance), shellLeft, "right edge distance");
            AssertNear(areaTop + (areaHeight - placement.ShellHeight) / 2 + centerOffset, shellTop, "right center offset");
            break;
    }

    if (placement.WindowWidth <= 0 || placement.WindowHeight <= 0)
    {
        throw new InvalidOperationException($"{edge}: invalid window size.");
    }
}

void AssertNear(double expected, double actual, string name)
{
    if (Math.Abs(expected - actual) > 0.001)
    {
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}.");
    }
}

void ValidateReorder()
{
    var bar = new DockBarSettings
    {
        Items =
        [
            CreateItem("a"),
            CreateItem("b"),
            CreateItem("c"),
            CreateItem("d")
        ]
    };
    var viewModel = new DockBarViewModel(bar);

    AssertTrue(viewModel.MoveItemToIndex("a", 3), "move a before d");
    AssertOrder(viewModel, "b", "c", "a", "d");
    AssertPersistedOrder(bar, "b", "c", "a", "d");

    AssertTrue(viewModel.MoveItemToIndex("d", 0), "move d to start");
    AssertOrder(viewModel, "d", "b", "c", "a");
    AssertPersistedOrder(bar, "d", "b", "c", "a");

    AssertTrue(viewModel.MoveItemToEnd("b"), "move b to end");
    AssertOrder(viewModel, "d", "c", "a", "b");
    AssertPersistedOrder(bar, "d", "c", "a", "b");

    AssertTrue(!viewModel.MoveItemToIndex("missing", 1), "missing item should not move");
}

void ValidateRemoveItem()
{
    var bar = new DockBarSettings
    {
        Items =
        [
            CreateItem("a"),
            CreateItem("b"),
            CreateItem("c")
        ]
    };
    var viewModel = new DockBarViewModel(bar);

    AssertTrue(viewModel.RemoveItem("b"), "remove b");
    AssertOrder(viewModel, "a", "c");
    AssertPersistedOrder(bar, "a", "c");
    AssertTrue(!viewModel.RemoveItem("missing"), "missing item should not remove");
}

void ValidateSpecialItems()
{
    var recycleBin = DockItem.CreateRecycleBin();
    AssertTrue(recycleBin.Kind == DockItemKind.RecycleBin, "recycle bin kind");
    AssertTrue(recycleBin.TargetPath == "shell:RecycleBinFolder", "recycle bin shell target");

    var bar = new DockBarSettings
    {
        Items =
        [
            DockItem.CreateWindowsButton(),
            recycleBin,
            CreateItem("a")
        ]
    };
    var viewModel = new DockBarViewModel(bar);

    AssertTrue(viewModel.RemoveItem(recycleBin.Id), "remove recycle bin");
    AssertOrder(viewModel, "windows-button", "a");
    AssertPersistedOrder(bar, "windows-button", "a");
}

void ValidateSpecialCommandItems()
{
    var separator = DockItem.CreateSeparator();

    AssertTrue(separator.Kind == DockItemKind.Separator, "separator kind");

    var separatorViewModel = new DockItemViewModel(separator);
    AssertTrue(separatorViewModel.ContentVisibility == System.Windows.Visibility.Collapsed, "separator hides icon content");
    AssertTrue(separatorViewModel.SeparatorVisibility == System.Windows.Visibility.Visible, "separator shows separator content");
}

void ValidateDefaultDockItems()
{
    var items = DefaultDockItemFactory.CreateInitialItems(TextCatalog.Get(TextCatalog.English)).ToArray();

    AssertTrue(items.Length == 5, "default dock should start with exactly five items");
    AssertTrue(items[0].Kind == DockItemKind.WindowsButton, "default first item should be Windows button");
    AssertTrue(items[1].DisplayName == "Windows Settings", "default second item should be Windows Settings");
    AssertTrue(items[2].DisplayName == "File Explorer", "default third item should be File Explorer");
    AssertTrue(items[3].DisplayName == "Microsoft Edge", "default fourth item should be Microsoft Edge");
    AssertTrue(items[4].Kind == DockItemKind.RecycleBin, "default fifth item should be Recycle Bin");
    AssertTrue(!items.Any(static item => item.Kind is DockItemKind.DockSettings or DockItemKind.Quit), "default dock should not include legacy Settings or Exit items");

    var bar = new DockBarSettings();
    AssertTrue(bar.ImportMode == DockImportMode.CreateShortcutInBarFolder, "shortcut creation should be the default import behavior");
    AssertTrue(bar.MoveModifierKey == DockMoveModifierKey.Shift, "Shift should be the default move modifier");
    AssertTrue(bar.GifModifierKey == DockMoveModifierKey.Alt, "Alt should be the default looping GIF modifier");
}

void ValidateLegacySpecialItemsAreRemovedOnLoad()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "rock-et-dock-legacy-config-" + Guid.NewGuid().ToString("N"));
    var localAppData = Path.Combine(tempRoot, "localappdata");
    var userProfile = Path.Combine(tempRoot, "profile");
    var previousLocalAppData = Environment.GetEnvironmentVariable("ROCK_ET_DOCK_LOCALAPPDATA");
    var previousUserProfile = Environment.GetEnvironmentVariable("ROCK_ET_DOCK_USERPROFILE");

    try
    {
        Environment.SetEnvironmentVariable("ROCK_ET_DOCK_LOCALAPPDATA", localAppData);
        Environment.SetEnvironmentVariable("ROCK_ET_DOCK_USERPROFILE", userProfile);
        Directory.CreateDirectory(UserPaths.ConfigRoot);
        File.WriteAllText(UserPaths.ConfigFile, """
        {
          "Version": 1,
          "App": {
            "Language": "en-US"
          },
          "Bars": [
            {
              "Id": "legacy",
              "Name": "Legacy",
              "ImportMode": "MoveToBarFolder",
              "Items": [
                {
                  "Id": "windows-button",
                  "Kind": "WindowsButton",
                  "DisplayName": "Windows"
                },
                {
                  "Id": "legacy-settings",
                  "Kind": "DockSettings",
                  "DisplayName": "Settings"
                },
                {
                  "Id": "legacy-quit",
                  "Kind": "Quit",
                  "DisplayName": "Exit"
                }
              ]
            }
          ]
        }
        """);

        var config = new DockConfigurationStore().Load();
        var bar = config.Bars.Single();
        AssertTrue(bar.Items.Count == 1, "legacy Settings and Exit items should be removed on load");
        AssertTrue(bar.Items[0].Kind == DockItemKind.WindowsButton, "legacy migration should preserve non-legacy items");
        AssertTrue(bar.ImportMode == DockImportMode.CreateShortcutInBarFolder, "legacy import mode should normalize to shortcut default");
        AssertTrue(!File.ReadAllText(UserPaths.ConfigFile).Contains("DockSettings", StringComparison.Ordinal), "saved config should not keep DockSettings");
        AssertTrue(!File.ReadAllText(UserPaths.ConfigFile).Contains("Quit", StringComparison.Ordinal), "saved config should not keep Quit");
    }
    finally
    {
        Environment.SetEnvironmentVariable("ROCK_ET_DOCK_LOCALAPPDATA", previousLocalAppData);
        Environment.SetEnvironmentVariable("ROCK_ET_DOCK_USERPROFILE", previousUserProfile);
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

void ValidateRunningApplicationMapping()
{
    var processPath = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
    {
        return;
    }

    var item = new DockItem
    {
        Kind = DockItemKind.File,
        DisplayName = "Current",
        TargetPath = processPath
    };

    AssertTrue(RunningApplicationService.TryGetExecutablePath(item, out var executablePath), "current process path should be executable");
    AssertTrue(string.Equals(Path.GetFullPath(processPath), executablePath, StringComparison.OrdinalIgnoreCase), "executable path should normalize");
}

void ValidateWindowsShellCommandsBypassRunningActivation()
{
    var windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    var settingsItem = new DockItem
    {
        Kind = DockItemKind.Link,
        DisplayName = "Windows Settings",
        TargetPath = Path.Combine(windowsPath, "ImmersiveControlPanel", "SystemSettings.exe")
    };
    var settingsUriItem = new DockItem
    {
        Kind = DockItemKind.Link,
        DisplayName = "Windows Settings",
        TargetPath = "ms-settings:"
    };
    var explorerItem = new DockItem
    {
        Kind = DockItemKind.Link,
        DisplayName = "File Explorer",
        TargetPath = Path.Combine(windowsPath, "explorer.exe")
    };

    AssertTrue(DockLauncher.IsWindowsShellCommand(settingsItem), "SystemSettings.exe should be a Windows shell command");
    AssertTrue(DockLauncher.IsWindowsShellCommand(settingsUriItem), "ms-settings URI should be a Windows shell command");
    AssertTrue(DockLauncher.IsWindowsShellCommand(explorerItem), "explorer.exe should be a Windows shell command");
    AssertTrue(!RunningApplicationService.TryGetExecutablePath(settingsItem, out _), "Settings should not be treated as a running app target");
    AssertTrue(!RunningApplicationService.TryGetExecutablePath(explorerItem, out _), "File Explorer should not be treated as a running app target");
}

void ValidateDropPlaceholder()
{
    var bar = new DockBarSettings
    {
        Items =
        [
            CreateItem("a"),
            CreateItem("b"),
            CreateItem("c")
        ]
    };
    var viewModel = new DockBarViewModel(bar);

    AssertTrue(viewModel.SetDropPlaceholderVisualIndex(1), "insert drop placeholder");
    AssertOrder(viewModel, "a", DockBarViewModel.DropPlaceholderId, "b", "c");
    AssertPersistedOrder(bar, "a", "b", "c");

    AssertTrue(viewModel.SetDropPlaceholderVisualIndex(3), "move drop placeholder");
    AssertOrder(viewModel, "a", "b", "c", DockBarViewModel.DropPlaceholderId);

    var placeholderIndex = viewModel.RemoveDropPlaceholder();
    AssertTrue(placeholderIndex == 3, "remove drop placeholder index");
    AssertOrder(viewModel, "a", "b", "c");
}

void ValidateImportModes()
{
    var tempProfile = Path.Combine(Path.GetTempPath(), "rock-et-dock-import-profile-" + Guid.NewGuid().ToString("N"));
    var sourceRoot = Path.Combine(tempProfile, "source");
    var previousProfile = Environment.GetEnvironmentVariable("ROCK_ET_DOCK_USERPROFILE");

    try
    {
        Directory.CreateDirectory(sourceRoot);
        Environment.SetEnvironmentVariable("ROCK_ET_DOCK_USERPROFILE", tempProfile);

        var shortcutSource = Path.Combine(sourceRoot, "shortcut-source.txt");
        File.WriteAllText(shortcutSource, "shortcut");
        var shortcutBar = new DockBarSettings
        {
            Name = "ShortcutBar",
            ImportMode = DockImportMode.CreateShortcutInBarFolder
        };
        var shortcutItem = new DockItemImporter().ImportFileSystemPath(shortcutBar, shortcutSource);
        AssertTrue(File.Exists(shortcutSource), "shortcut mode keeps source");
        AssertTrue(File.Exists(shortcutItem.TargetPath), "shortcut mode creates link");
        AssertTrue(Path.GetExtension(shortcutItem.TargetPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase), "shortcut mode extension");
        AssertTrue(shortcutItem.Kind == DockItemKind.Link, "shortcut mode item kind");

        var moveSource = Path.Combine(sourceRoot, "move-source.txt");
        File.WriteAllText(moveSource, "move");
        var moveBar = new DockBarSettings
        {
            Name = "MoveBar",
            ImportMode = DockImportMode.MoveToBarFolder
        };
        var moveItem = new DockItemImporter().ImportFileSystemPath(moveBar, moveSource);
        AssertTrue(!File.Exists(moveSource), "move mode removes source");
        AssertTrue(File.Exists(moveItem.TargetPath), "move mode creates target");
        AssertTrue(moveItem.Kind == DockItemKind.File, "move mode item kind");
    }
    finally
    {
        Environment.SetEnvironmentVariable("ROCK_ET_DOCK_USERPROFILE", previousProfile);
        if (Directory.Exists(tempProfile))
        {
            Directory.Delete(tempProfile, recursive: true);
        }
    }
}

void ValidateAnimatedGifImport()
{
    var tempProfile = Path.Combine(Path.GetTempPath(), "rock-et-dock-gif-profile-" + Guid.NewGuid().ToString("N"));
    var sourceRoot = Path.Combine(tempProfile, "source");
    var previousProfile = Environment.GetEnvironmentVariable("ROCK_ET_DOCK_USERPROFILE");

    try
    {
        Directory.CreateDirectory(sourceRoot);
        Environment.SetEnvironmentVariable("ROCK_ET_DOCK_USERPROFILE", tempProfile);

        var sourcePath = Path.Combine(sourceRoot, "sample.gif");
        File.WriteAllBytes(sourcePath, Convert.FromBase64String("R0lGODlhAQABAPAAAP///wAAACH5BAAAAAAALAAAAAABAAEAAAICRAEAOw=="));

        var bar = new DockBarSettings { Name = "GifBar" };
        var item = new DockItemImporter().ImportAnimatedGif(bar, sourcePath);
        AssertTrue(item.Kind == DockItemKind.AnimatedGif, "gif import item kind");
        AssertTrue(File.Exists(item.TargetPath), "gif import target exists");
        AssertTrue(File.Exists(sourcePath), "gif import keeps source");
        AssertTrue(item.TargetPath.Contains($"{Path.DirectorySeparatorChar}gifs{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase), "gif import folder");
    }
    finally
    {
        Environment.SetEnvironmentVariable("ROCK_ET_DOCK_USERPROFILE", previousProfile);
        if (Directory.Exists(tempProfile))
        {
            Directory.Delete(tempProfile, recursive: true);
        }
    }
}

void ValidateExportToDesktop()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "rock-et-dock-export-" + Guid.NewGuid().ToString("N"));
    var sourceRoot = Path.Combine(tempRoot, "source");
    var desktop = Path.Combine(tempRoot, "desktop");
    var previousDesktop = Environment.GetEnvironmentVariable("ROCK_ET_DOCK_DESKTOP");

    try
    {
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(desktop);
        var sourcePath = Path.Combine(sourceRoot, "sample.txt");
        File.WriteAllText(sourcePath, "sample");
        Environment.SetEnvironmentVariable("ROCK_ET_DOCK_DESKTOP", desktop);

        var item = new DockItem
        {
            Id = "export-file",
            Kind = DockItemKind.File,
            DisplayName = "Sample",
            TargetPath = sourcePath
        };

        var targetPath = new DockItemExporter().MoveToDesktop(item);
        AssertTrue(File.Exists(targetPath), "export target should exist");
        AssertTrue(!File.Exists(sourcePath), "export source should move away");
        AssertTrue(string.Equals(Path.GetDirectoryName(targetPath), desktop, StringComparison.OrdinalIgnoreCase), "export target should be on desktop");
    }
    finally
    {
        Environment.SetEnvironmentVariable("ROCK_ET_DOCK_DESKTOP", previousDesktop);
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

void ValidateCompactIconSizing()
{
    var compactBar = new DockBarSettings
    {
        IconSize = 12,
        HideLabels = true
    };
    var compactViewModel = new DockBarViewModel(compactBar);

    AssertTrue(compactViewModel.IconImageSize == 12, "compact icon image size should honor 12px minimum");
    AssertTrue(compactViewModel.ItemButtonSize < 48, "compact hidden-label item should be smaller than old minimum");
    AssertTrue(compactViewModel.IconTileSize < compactViewModel.ItemButtonSize, "compact tile should fit inside item");

    var placement = DockGeometry.Calculate(new DockGeometryInput(
        DockEdge.Bottom,
        0,
        0,
        1920,
        1040,
        ItemCount: 3,
        compactViewModel.ItemButtonSize,
        compactBar.IconSpacing,
        compactViewModel.HorizontalZoomOverhang,
        compactViewModel.VerticalZoomOverhang,
        EdgeDistance: 0,
        CenterOffset: 0,
        BarWidth: compactBar.BarWidth,
        BarHeight: compactBar.BarHeight));

    AssertTrue(placement.ShellHeight >= compactViewModel.ItemButtonSize + 28, "compact shell should include item margins and padding");
}

void ValidateNeighborZoomScale()
{
    var bar = new DockBarSettings
    {
        IconSize = 40,
        ZoomSize = 64,
        ZoomEnabled = true,
        ZoomRange = 3
    };
    var viewModel = new DockBarViewModel(bar);

    var focused = viewModel.GetZoomScaleForDistance(0);
    var midPoint = viewModel.GetZoomScaleForDistance(0.5);
    var near = viewModel.GetZoomScaleForDistance(1);
    var between = viewModel.GetZoomScaleForDistance(1.5);
    var far = viewModel.GetZoomScaleForDistance(3);
    var outside = viewModel.GetZoomScaleForDistance(4);

    AssertTrue(focused > midPoint, "continuous zoom should reduce before neighbor index");
    AssertTrue(midPoint > near, "continuous zoom midpoint should be smoother than full neighbor step");
    AssertTrue(focused > near, "focused icon should scale more than neighbor");
    AssertTrue(near > between, "continuous zoom should decrease between icon slots");
    AssertTrue(near > far, "near neighbor should scale more than far neighbor");
    AssertTrue(far > 1.0, "last icon inside zoom range should still scale");
    AssertTrue(viewModel.GetZoomScaleForDistance(5) == 1.0, "icon outside zoom range should stay at normal scale");
}

void ValidateHoverZoomOffsets()
{
    var offsets = DockZoomLayout.CalculateOffsets(
        [0, 50, 100],
        [1.0, 1.6, 1.0],
        itemSize: 50);

    AssertNear(-15, offsets[0], "left icon should move left for focused middle icon");
    AssertNear(0, offsets[1], "focused icon should stay centered");
    AssertNear(15, offsets[2], "right icon should move right for focused middle icon");

    var combinedOffsets = DockZoomLayout.CalculateOffsets(
        [0, 50, 100],
        [1.3, 1.6, 1.3],
        itemSize: 50);

    AssertTrue(combinedOffsets[0] < 0, "left influenced icon should still move outward");
    AssertTrue(combinedOffsets[2] > 0, "right influenced icon should still move outward");
}

void ValidateHoverOverhangPreventsEdgeClipping()
{
    var bar = new DockBarSettings
    {
        IconSize = 40,
        ZoomSize = 64,
        ZoomEnabled = true,
        ZoomRange = 4,
        IconSpacing = 8
    };
    var viewModel = new DockBarViewModel(bar);
    var slotStep = viewModel.ItemButtonSize + bar.IconSpacing;
    var centers = Enumerable.Range(0, 9)
        .Select(index => index * slotStep)
        .ToArray();
    var pointerAxis = centers[4];
    var scales = centers
        .Select(center => viewModel.GetZoomScaleForDistance(Math.Abs(pointerAxis - center) / slotStep))
        .ToArray();
    var offsets = DockZoomLayout.CalculateOffsets(centers, scales, viewModel.ItemButtonSize);
    var requiredExtent = offsets
        .Select((offset, index) => Math.Abs(offset) + (viewModel.ItemButtonSize * (scales[index] - 1.0) / 2.0))
        .Max();

    AssertTrue(viewModel.PrimaryAxisZoomOverhang > requiredExtent, "primary-axis overhang should cover outward hover offsets");
    AssertTrue(viewModel.PrimaryAxisZoomOverhang > viewModel.CrossAxisZoomOverhang, "primary-axis overhang should exceed cross-axis overhang when neighbors move outward");
}

void ValidateCustomBarGeometry()
{
    var placement = DockGeometry.Calculate(new DockGeometryInput(
        DockEdge.Bottom,
        0,
        0,
        1920,
        1040,
        ItemCount: 4,
        ItemButtonSize: 64,
        IconSpacing: 18,
        HorizontalOverhang: 26,
        VerticalOverhang: 26,
        EdgeDistance: 0,
        CenterOffset: 0,
        BarWidth: 720,
        BarHeight: 120));

    AssertNear(720, placement.ShellWidth, "custom horizontal bar width");
    AssertNear(120, placement.ShellHeight, "custom horizontal bar height");
}

void ValidateSliderGeometryExtremes()
{
    foreach (var edge in Enum.GetValues<DockEdge>())
    {
        foreach (var iconSize in new[] { 12, 40, 96 })
        {
            foreach (var spacing in new[] { 0, 8, 40 })
            {
                var bar = new DockBarSettings
                {
                    Edge = edge,
                    IconSize = iconSize,
                    IconSpacing = spacing,
                    IconBottomMargin = 60,
                    ZoomEnabled = false,
                    BarWidth = 1,
                    BarHeight = 1
                };
                var viewModel = new DockBarViewModel(bar);

                ValidateSpacingMarginAxis(edge, spacing, viewModel.ItemMargin);
                AssertNear(-60, viewModel.ItemContentOffsetY, $"{edge} icon bottom offset");
                AssertTrue(viewModel.ItemContentMargin.Top == 0 && viewModel.ItemContentMargin.Bottom == 0, $"{edge} icon bottom margin should not squeeze item content");
                AssertNear(12, viewModel.HorizontalZoomOverhang, $"{edge} bottom offset should not inflate horizontal overhang");
                AssertNear(72, viewModel.VerticalZoomOverhang, $"{edge} bottom offset should reserve vertical overhang");

                foreach (var edgeDistance in new[] { -220, 0, 200 })
                {
                    foreach (var centerOffset in new[] { -800, 0, 800 })
                    {
                        var placement = DockGeometry.Calculate(new DockGeometryInput(
                            edge,
                            areaLeft,
                            areaTop,
                            areaWidth,
                            areaHeight,
                            ItemCount: 5,
                            viewModel.ItemButtonSize,
                            spacing,
                            viewModel.HorizontalZoomOverhang,
                            viewModel.VerticalZoomOverhang,
                            EdgeDistance: edgeDistance,
                            CenterOffset: centerOffset,
                            BarWidth: bar.BarWidth,
                            BarHeight: bar.BarHeight));

                        var requiredPrimarySize = 5 * (viewModel.ItemButtonSize + spacing) + 24;
                        var requiredCrossSize = viewModel.ItemButtonSize + 28;
                        if (edge is DockEdge.Left or DockEdge.Right)
                        {
                            AssertTrue(placement.ShellWidth >= requiredCrossSize, $"{edge} width slider should not clip item cross-axis");
                            AssertTrue(placement.ShellHeight >= requiredPrimarySize, $"{edge} height slider should not clip item stack");
                        }
                        else
                        {
                            AssertTrue(placement.ShellWidth >= requiredPrimarySize, $"{edge} width slider should not clip item row");
                            AssertTrue(placement.ShellHeight >= requiredCrossSize, $"{edge} height slider should not clip item cross-axis");
                        }

                        ValidateShellWithinWorkingArea($"{edge} edgeDistance={edgeDistance} centerOffset={centerOffset}", placement, viewModel);
                    }
                }
            }
        }
    }
}

void ValidateSpacingMarginAxis(DockEdge edge, int spacing, System.Windows.Thickness margin)
{
    var half = spacing / 2.0;
    if (edge is DockEdge.Left or DockEdge.Right)
    {
        AssertNear(0, margin.Left, $"{edge} spacing left margin");
        AssertNear(0, margin.Right, $"{edge} spacing right margin");
        AssertNear(half, margin.Top, $"{edge} spacing top margin");
        AssertNear(half, margin.Bottom, $"{edge} spacing bottom margin");
        return;
    }

    AssertNear(half, margin.Left, $"{edge} spacing left margin");
    AssertNear(half, margin.Right, $"{edge} spacing right margin");
    AssertNear(0, margin.Top, $"{edge} spacing top margin");
    AssertNear(0, margin.Bottom, $"{edge} spacing bottom margin");
}

void ValidateShellWithinWorkingArea(string scenario, DockPlacement placement, DockBarViewModel viewModel)
{
    var shellLeft = placement.WindowLeft + viewModel.HorizontalZoomOverhang;
    var shellTop = placement.WindowTop + viewModel.VerticalZoomOverhang;

    AssertTrue(shellLeft >= areaLeft - 0.001, $"{scenario} shell should not move left of working area");
    AssertTrue(shellTop >= areaTop - 0.001, $"{scenario} shell should not move above working area");
    AssertTrue(shellLeft + placement.ShellWidth <= areaLeft + areaWidth + 0.001, $"{scenario} shell should not move right of working area");
    AssertTrue(shellTop + placement.ShellHeight <= areaTop + areaHeight + 0.001, $"{scenario} shell should not move below working area");
}

void ValidatePlaceholderIsGapOnly()
{
    var placeholder = new DockItemViewModel(new DockItem
    {
        Kind = DockItemKind.DropPlaceholder
    });

    AssertTrue(placeholder.ContentVisibility == System.Windows.Visibility.Collapsed, "drop placeholder should render as empty gap");
    AssertTrue(placeholder.ButtonVisibility == System.Windows.Visibility.Collapsed, "drop placeholder should not consume a fixed layout slot");
}

void ValidateLocalization()
{
    var english = TextCatalog.Get("en-US");
    var portuguese = TextCatalog.Get("pt-BR");

    AssertTrue(english["SettingsClose"] == "Close", "English settings label");
    AssertTrue(portuguese["SettingsClose"] == "Fechar", "Brazilian Portuguese settings label");
    AssertTrue(TextCatalog.NormalizeLanguage("missing") == TextCatalog.English, "unknown languages should fall back to English");

    var bar = new DockBarSettings
    {
        Items =
        [
            DockItem.CreateRecycleBin()
        ]
    };

    var viewModel = new DockBarViewModel(bar, TextCatalog.English);
    AssertTrue(viewModel.Items[0].DisplayName == "Recycle Bin", "English recycle bin item label");

    viewModel.SetLanguage(TextCatalog.PortugueseBrazil);
    AssertTrue(viewModel.Items[0].DisplayName == "Lixeira", "Brazilian Portuguese recycle bin item label");
}

void ValidateDockLimit()
{
    var configuration = new DockConfiguration();
    for (var index = 0; index < DockConfiguration.MaxBars; index++)
    {
        configuration.Bars.Add(DockBarSettings.Create($"Bar {index + 1}", DockEdge.Bottom));
    }

    AssertTrue(DockConfiguration.MaxBars == 4, "dock limit should be four bars");
    AssertTrue(!configuration.CanCreateBar, "configuration should block creating a fifth bar");

    configuration.Bars.RemoveAt(configuration.Bars.Count - 1);
    AssertTrue(configuration.CanCreateBar, "configuration should allow creating a fourth bar");
}

void ValidateVerticalDockGeometryAndOrientation()
{
    var leftBar = new DockBarSettings { Edge = DockEdge.Left, IconSize = 42, IconSpacing = 9 };
    var rightBar = new DockBarSettings { Edge = DockEdge.Right, IconSize = 42, IconSpacing = 9 };
    var bottomBar = new DockBarSettings { Edge = DockEdge.Bottom, IconSize = 42, IconSpacing = 9 };

    AssertTrue(new DockBarViewModel(leftBar).Orientation == System.Windows.Controls.Orientation.Vertical, "left dock should use vertical item panel");
    AssertTrue(new DockBarViewModel(rightBar).Orientation == System.Windows.Controls.Orientation.Vertical, "right dock should use vertical item panel");
    AssertTrue(new DockBarViewModel(bottomBar).Orientation == System.Windows.Controls.Orientation.Horizontal, "bottom dock should use horizontal item panel");

    var verticalPlacement = DockGeometry.Calculate(new DockGeometryInput(
        DockEdge.Left,
        0,
        0,
        1920,
        1040,
        ItemCount: 10,
        new DockBarViewModel(leftBar).ItemButtonSize,
        leftBar.IconSpacing,
        HorizontalOverhang: 26,
        VerticalOverhang: 26,
        EdgeDistance: 10,
        CenterOffset: 0,
        BarWidth: 0,
        BarHeight: 0));

    AssertTrue(verticalPlacement.ShellHeight > verticalPlacement.ShellWidth, "left dock shell should be taller than wide");
    AssertTrue(verticalPlacement.WindowHeight > verticalPlacement.WindowWidth, "left dock window should be taller than wide");

    var horizontalPlacement = DockGeometry.Calculate(new DockGeometryInput(
        DockEdge.Bottom,
        0,
        0,
        1920,
        1040,
        ItemCount: 10,
        new DockBarViewModel(bottomBar).ItemButtonSize,
        bottomBar.IconSpacing,
        HorizontalOverhang: 26,
        VerticalOverhang: 26,
        EdgeDistance: 10,
        CenterOffset: 0,
        BarWidth: 0,
        BarHeight: 0));

    AssertTrue(horizontalPlacement.ShellWidth > horizontalPlacement.ShellHeight, "bottom dock shell should be wider than tall");
    AssertTrue(horizontalPlacement.WindowWidth > horizontalPlacement.WindowHeight, "bottom dock window should be wider than tall");
}

void ValidateTransparentBackgroundOpacity()
{
    var transparentBar = new DockBarSettings
    {
        BackgroundOpacity = 0,
        Theme = "Rock ET Glass"
    };
    var transparentViewModel = new DockBarViewModel(transparentBar);

    AssertTrue(GetBrushAlpha(transparentViewModel.ShellBackground) == 0, "minimum background opacity should make shell background transparent");
    AssertTrue(GetBrushAlpha(transparentViewModel.ShellBorderBrush) == 0, "minimum background opacity should hide shell border");
    AssertTrue(transparentViewModel.ShadowOpacity == 0, "minimum background opacity should hide shell shadow");

    var opaqueBar = new DockBarSettings
    {
        BackgroundOpacity = 100,
        Theme = "Rock ET Glass"
    };
    var opaqueViewModel = new DockBarViewModel(opaqueBar);

    AssertTrue(GetBrushAlpha(opaqueViewModel.ShellBackground) == 255, "maximum background opacity should keep shell background opaque");
    AssertTrue(opaqueViewModel.ShadowOpacity > 0, "non-transparent background opacity should keep shell shadow");
}

DockItem CreateItem(string id)
{
    return new DockItem
    {
        Id = id,
        Kind = DockItemKind.File,
        DisplayName = id.ToUpperInvariant()
    };
}

void AssertOrder(DockBarViewModel viewModel, params string[] expected)
{
    var actual = viewModel.Items.Select(static item => item.Item.Id).ToArray();
    if (!actual.SequenceEqual(expected))
    {
        throw new InvalidOperationException($"visual order: expected {string.Join(", ", expected)}, got {string.Join(", ", actual)}.");
    }
}

void AssertPersistedOrder(DockBarSettings bar, params string[] expected)
{
    var actual = bar.Items.Select(static item => item.Id).ToArray();
    if (!actual.SequenceEqual(expected))
    {
        throw new InvalidOperationException($"persisted order: expected {string.Join(", ", expected)}, got {string.Join(", ", actual)}.");
    }
}

void AssertTrue(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException(name);
    }
}

byte GetBrushAlpha(System.Windows.Media.Brush brush)
{
    if (brush is System.Windows.Media.SolidColorBrush solidColorBrush)
    {
        return solidColorBrush.Color.A;
    }

    return brush.Opacity <= 0 ? (byte)0 : (byte)255;
}
