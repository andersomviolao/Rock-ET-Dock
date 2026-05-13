using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        var targetPath = GetAvailablePath(barFolder, $"{UserPaths.ToSafeFileName(baseName)}.url");
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
        var targetPath = GetAvailablePath(gifFolder, Path.GetFileName(sourceFullPath));
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

        var fileName = Path.GetFileName(sourceFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetPath = GetAvailablePath(barFolder, fileName);

        if (File.Exists(sourceFullPath))
        {
            TryMoveFile(sourceFullPath, targetPath);
            return targetPath;
        }

        if (Directory.Exists(sourceFullPath))
        {
            TryMoveDirectory(sourceFullPath, targetPath);
            return targetPath;
        }

        throw new FileNotFoundException("Dropped path no longer exists.", sourceFullPath);
    }

    private static string CreateShortcutInBarFolder(string sourcePath, string barFolder)
    {
        var sourceFullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourceFullPath) && !Directory.Exists(sourceFullPath))
        {
            throw new FileNotFoundException("Dropped path no longer exists.", sourceFullPath);
        }

        var shortcutPath = GetAvailablePath(barFolder, $"{GetDisplayName(sourceFullPath)}.lnk");
        CreateShellShortcut(shortcutPath, sourceFullPath);
        return shortcutPath;
    }

    private static void CreateShellShortcut(string shortcutPath, string targetPath)
    {
        Type? shellType = null;
        object? shell = null;
        object? shortcut = null;

        try
        {
            shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true);
            shell = Activator.CreateInstance(shellType!);
            shortcut = shellType!.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcutPath]);

            var shortcutType = shortcut!.GetType();
            shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, [targetPath]);
            shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, [GetWorkingDirectory(targetPath)]);
            shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    private static string GetWorkingDirectory(string targetPath)
    {
        if (Directory.Exists(targetPath))
        {
            return targetPath;
        }

        return Path.GetDirectoryName(targetPath) ?? UserPaths.UserProfile;
    }

    private static void TryMoveFile(string sourcePath, string targetPath)
    {
        try
        {
            File.Move(sourcePath, targetPath);
        }
        catch (IOException)
        {
            File.Copy(sourcePath, targetPath);
            File.Delete(sourcePath);
        }
    }

    private static void TryMoveDirectory(string sourcePath, string targetPath)
    {
        try
        {
            Directory.Move(sourcePath, targetPath);
        }
        catch (IOException)
        {
            CopyDirectory(sourcePath, targetPath);
            Directory.Delete(sourcePath, recursive: true);
        }
    }

    private static void CopyDirectory(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(targetPath);

        foreach (var directory in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, directory);
            Directory.CreateDirectory(Path.Combine(targetPath, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, file);
            var targetFile = Path.Combine(targetPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile);
        }
    }

    private static string GetAvailablePath(string directory, string fileName)
    {
        var safeFileName = UserPaths.ToSafeFileName(Path.GetFileNameWithoutExtension(fileName));
        var extension = Path.GetExtension(fileName);
        var candidate = Path.Combine(directory, safeFileName + extension);

        for (var index = 2; File.Exists(candidate) || Directory.Exists(candidate); index++)
        {
            candidate = Path.Combine(directory, $"{safeFileName} ({index}){extension}");
        }

        return candidate;
    }

    private static string GetDisplayName(string path)
    {
        var trimmed = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Directory.Exists(trimmed)
            ? Path.GetFileName(trimmed)
            : Path.GetFileNameWithoutExtension(trimmed);
    }
}
