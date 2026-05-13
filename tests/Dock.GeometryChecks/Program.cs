using Dock.App.Models;
using Dock.App.Services;
using Dock.App.ViewModels;

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
        edgeDistance,
        centerOffset,
        BarWidth: 0,
        BarHeight: 0));

    Validate(edge, placement);
}

ValidateReorder();
ValidateRemoveItem();
ValidateSpecialItems();
ValidateDropPlaceholder();
ValidateImportModes();
ValidateAnimatedGifImport();
ValidateExportToDesktop();
ValidateCompactIconSizing();
ValidateNeighborZoomScale();
ValidateCustomBarGeometry();
ValidatePlaceholderIsGapOnly();

Console.WriteLine("Dock geometry checks passed for Top, Bottom, Left and Right.");
Console.WriteLine("Dock reorder checks passed.");
Console.WriteLine("Dock desktop export checks passed.");
Console.WriteLine("Dock import, placeholder and GIF checks passed.");
Console.WriteLine("Dock compact icon sizing checks passed.");
Console.WriteLine("Dock neighbor zoom checks passed.");
Console.WriteLine("Dock custom bar sizing checks passed.");
Console.WriteLine("Dock placeholder gap checks passed.");

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
        compactViewModel.ZoomOverhang,
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
        Overhang: 26,
        EdgeDistance: 0,
        CenterOffset: 0,
        BarWidth: 720,
        BarHeight: 120));

    AssertNear(720, placement.ShellWidth, "custom horizontal bar width");
    AssertNear(120, placement.ShellHeight, "custom horizontal bar height");
}

void ValidatePlaceholderIsGapOnly()
{
    var placeholder = new DockItemViewModel(new DockItem
    {
        Kind = DockItemKind.DropPlaceholder
    });

    AssertTrue(placeholder.ContentVisibility == System.Windows.Visibility.Collapsed, "drop placeholder should render as empty gap");
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
