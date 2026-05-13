using System;
using System.IO;
using Dock.App.Models;

namespace Dock.App.Services;

public sealed class DockItemExporter
{
    public string MoveToDesktop(DockItem item)
    {
        if (item.Kind is DockItemKind.WindowsButton or DockItemKind.RecycleBin ||
            item.IsRuntime ||
            string.IsNullOrWhiteSpace(item.TargetPath))
        {
            throw new InvalidOperationException("Este item nao pode ser movido para a area de trabalho.");
        }

        var sourcePath = Path.GetFullPath(item.TargetPath);
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException("O item real nao existe mais.", sourcePath);
        }

        var desktop = UserPaths.DesktopDirectory;
        Directory.CreateDirectory(desktop);

        if (IsDirectChildOfDirectory(sourcePath, desktop))
        {
            return sourcePath;
        }

        var fileName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetPath = GetAvailablePath(desktop, fileName);

        if (File.Exists(sourcePath))
        {
            TryMoveFile(sourcePath, targetPath);
            return targetPath;
        }

        TryMoveDirectory(sourcePath, targetPath);
        return targetPath;
    }

    private static bool IsDirectChildOfDirectory(string path, string directory)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(
            parent?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
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
}
