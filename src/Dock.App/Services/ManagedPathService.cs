using System;
using System.IO;

namespace Dock.App.Services;

public static class ManagedPathService
{
    public static string MoveFileSystemEntry(string sourcePath, string targetDirectory)
    {
        var sourceFullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourceFullPath) && !Directory.Exists(sourceFullPath))
        {
            throw new FileNotFoundException("Dropped path no longer exists.", sourceFullPath);
        }

        var targetFullDirectory = Path.GetFullPath(targetDirectory);
        if (Directory.Exists(sourceFullPath) &&
            IsPathInsideDirectory(targetFullDirectory, sourceFullPath))
        {
            throw new InvalidOperationException("Nao e possivel mover uma pasta para dentro dela mesma.");
        }

        Directory.CreateDirectory(targetDirectory);
        var targetPath = GetAvailablePath(
            targetDirectory,
            Path.GetFileName(sourceFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

        if (File.Exists(sourceFullPath))
        {
            MoveFile(sourceFullPath, targetPath);
            return targetPath;
        }

        MoveDirectory(sourceFullPath, targetPath);
        return targetPath;
    }

    public static string GetAvailablePath(string directory, string fileName)
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

    public static bool IsDirectChildOfDirectory(string path, string directory)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(
            parent?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void MoveFile(string sourcePath, string targetPath)
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

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static void MoveDirectory(string sourcePath, string targetPath)
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
}
