using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dock.App.Models;

namespace Dock.App.Services;

public sealed class DockItemImporter
{
    public DockItem ImportFileSystemPath(DockBarSettings bar, string sourcePath)
    {
        var barFolder = UserPaths.EnsureBarFolder(bar.Name);
        var displayName = GetDisplayName(sourcePath);
        var targetPath = bar.ImportMode == DockImportMode.CreateShortcutInBarFolder
            ? CreateShortcutInBarFolder(sourcePath, barFolder)
            : MoveIntoBarFolder(sourcePath, barFolder);

        return new DockItem
        {
            Kind = bar.ImportMode == DockImportMode.CreateShortcutInBarFolder
                ? DockItemKind.Link
                : Directory.Exists(targetPath) ? DockItemKind.Folder : DockItemKind.File,
            DisplayName = displayName,
            TargetPath = targetPath,
            OriginalSourcePath = sourcePath
        };
    }

    public DockItem ImportUrl(DockBarSettings bar, Uri uri)
    {
        var barFolder = UserPaths.EnsureBarFolder(bar.Name);
        var baseName = string.IsNullOrWhiteSpace(uri.Host) ? "Link" : uri.Host;
        var targetPath = ManagedPathService.GetAvailablePath(barFolder, $"{UserPaths.ToSafeFileName(baseName)}.url");
        var content = $"[InternetShortcut]{Environment.NewLine}URL={uri}{Environment.NewLine}";
        File.WriteAllText(targetPath, content);

        return new DockItem
        {
            Kind = DockItemKind.Link,
            DisplayName = baseName,
            TargetPath = targetPath,
            OriginalSourcePath = uri.ToString()
        };
    }

    public DockItem ImportAnimatedGif(DockBarSettings bar, string sourcePath)
    {
        var sourceFullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourceFullPath))
        {
            throw new FileNotFoundException("GIF no longer exists.", sourceFullPath);
        }

        if (!Path.GetExtension(sourceFullPath).Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Selecione um arquivo .gif.");
        }

        var gifFolder = Path.Combine(UserPaths.EnsureBarFolder(bar.Name), "gifs");
        Directory.CreateDirectory(gifFolder);
        var targetPath = ManagedPathService.GetAvailablePath(gifFolder, Path.GetFileName(sourceFullPath));
        File.Copy(sourceFullPath, targetPath);

        return DockItem.CreateAnimatedGif(targetPath, Path.GetFileNameWithoutExtension(sourceFullPath));
    }

    public IReadOnlyList<DockItem> ImportAnimatedGifs(DockBarSettings bar, IEnumerable<string> sourcePaths)
    {
        return sourcePaths.Select(path => ImportAnimatedGif(bar, path)).ToList();
    }

    private static string MoveIntoBarFolder(string sourcePath, string barFolder)
    {
        var sourceFullPath = Path.GetFullPath(sourcePath);
        var barFullPath = Path.GetFullPath(barFolder);

        if (sourceFullPath.StartsWith(barFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return sourceFullPath;
        }

        return ManagedPathService.MoveFileSystemEntry(sourceFullPath, barFolder);
    }

    private static string CreateShortcutInBarFolder(string sourcePath, string barFolder)
    {
        var sourceFullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourceFullPath) && !Directory.Exists(sourceFullPath))
        {
            throw new FileNotFoundException("Dropped path no longer exists.", sourceFullPath);
        }

        var shortcutPath = ManagedPathService.GetAvailablePath(barFolder, $"{GetDisplayName(sourceFullPath)}.lnk");
        ShellShortcutService.CreateShortcut(shortcutPath, sourceFullPath);
        return shortcutPath;
    }

    private static string GetDisplayName(string path)
    {
        var trimmed = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Directory.Exists(trimmed)
            ? Path.GetFileName(trimmed)
            : Path.GetFileNameWithoutExtension(trimmed);
    }
}
