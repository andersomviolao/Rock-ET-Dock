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

        if (Current.Bars.Count == 0)
        {
            Current.Bars.Add(CreateDefaultBar("Dock"));
            Save();
        }

        foreach (var bar in Current.Bars)
        {
            UserPaths.EnsureBarFolder(bar.Name);
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
                CreateDefaultBar("Principal")
            ]
        };
    }

    private static DockBarSettings CreateDefaultBar(string name)
    {
        var bar = DockBarSettings.Create(name, DockEdge.Bottom);
        bar.Items.Add(DockItem.CreateWindowsButton());
        bar.Items.Add(DockItem.CreateRecycleBin());
        bar.Items.Add(DockItem.CreateDockSettings());
        bar.Items.Add(DockItem.CreateQuit());
        return bar;
    }
}
