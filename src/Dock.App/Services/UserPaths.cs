using System;
using System.IO;
using System.Linq;

namespace Dock.App.Services;

public static class UserPaths
{
    public const string AppName = "Rock ET Dock";

    public static string UserProfile =>
        GetPathOverride("ROCK_ET_DOCK_USERPROFILE") ??
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string UserDockRoot =>
        Path.Combine(UserProfile, AppName);

    public static string ConfigRoot =>
        Path.Combine(
            GetPathOverride("ROCK_ET_DOCK_LOCALAPPDATA") ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName);

    public static string DesktopDirectory =>
        GetPathOverride("ROCK_ET_DOCK_DESKTOP") ??
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string ConfigFile =>
        Path.Combine(ConfigRoot, "dock.config.json");

    public static string LogRoot =>
        Path.Combine(ConfigRoot, "logs");

    public static string EnsureBarFolder(string barName)
    {
        Directory.CreateDirectory(UserDockRoot);

        var path = GetBarFolder(barName);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetBarFolder(string barName)
    {
        return Path.Combine(UserDockRoot, ToSafeFileName(barName));
    }

    public static string ToSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "Barra" : safe;
    }

    private static string? GetPathOverride(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
