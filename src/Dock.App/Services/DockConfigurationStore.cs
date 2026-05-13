using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dock.App.Models;

namespace Dock.App.Services;

public sealed class DockConfigurationStore
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public DockConfiguration Current { get; private set; } = new();

    public DockConfiguration Load()
    {
        Directory.CreateDirectory(UserPaths.ConfigRoot);
        Directory.CreateDirectory(UserPaths.UserDockRoot);

        if (File.Exists(UserPaths.ConfigFile))
        {
            var json = File.ReadAllText(UserPaths.ConfigFile);
            Current = JsonSerializer.Deserialize<DockConfiguration>(json, _jsonOptions) ?? CreateDefault();
        }
        else
        {
            Current = CreateDefault();
            Save();
        }

        var changed = false;

        if (Current.Bars.Count == 0)
        {
            Current.Bars.Add(CreateDefaultBar("Dock"));
            changed = true;
        }

        Current.App.Language = TextCatalog.NormalizeLanguage(Current.App.Language);

        foreach (var bar in Current.Bars)
        {
            UserPaths.EnsureBarFolder(bar.Name);
            changed |= NormalizeBar(bar);
        }

        if (changed)
        {
            Save();
        }

        return Current;
    }

    public void Save()
    {
        Directory.CreateDirectory(UserPaths.ConfigRoot);
        var json = JsonSerializer.Serialize(Current, _jsonOptions);
        File.WriteAllText(UserPaths.ConfigFile, json);
    }

    private static DockConfiguration CreateDefault()
    {
        return new DockConfiguration
        {
            Bars =
            [
                CreateDefaultBar("Main")
            ]
        };
    }

    private static DockBarSettings CreateDefaultBar(string name)
    {
        var text = TextCatalog.Get(TextCatalog.English);
        var bar = DockBarSettings.Create(name, DockEdge.Bottom);
        bar.ImportMode = DockImportMode.CreateShortcutInBarFolder;
        bar.MoveModifierKey = DockMoveModifierKey.Shift;
        bar.GifModifierKey = DockMoveModifierKey.Alt;

        foreach (var item in DefaultDockItemFactory.CreateInitialItems(text))
        {
            bar.Items.Add(item);
        }

        return bar;
    }

    private static bool NormalizeBar(DockBarSettings bar)
    {
        var changed = false;

        if (bar.ImportMode != DockImportMode.CreateShortcutInBarFolder)
        {
            bar.ImportMode = DockImportMode.CreateShortcutInBarFolder;
            changed = true;
        }

        var removedLegacyItems = bar.Items.RemoveAll(static item =>
            item.Kind is DockItemKind.DockSettings or DockItemKind.Quit);
        return changed || removedLegacyItems > 0;
    }
}
