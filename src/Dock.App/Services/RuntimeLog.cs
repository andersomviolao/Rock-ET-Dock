using System;
using System.IO;

namespace Dock.App.Services;

public static class RuntimeLog
{
    private static readonly object Sync = new();

    public static void WriteDiagnostic(string context, string message)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ROCK_ET_DOCK_TRACE_DRAG"), "1", StringComparison.Ordinal))
        {
            return;
        }

        WriteEntry($"[{DateTimeOffset.Now:O}] {context}: {message}{Environment.NewLine}");
    }

    public static void Write(Exception exception, string context)
    {
        WriteEntry($"[{DateTimeOffset.Now:O}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
    }

    private static void WriteEntry(string entry)
    {
        try
        {
            Directory.CreateDirectory(UserPaths.LogRoot);
            var path = Path.Combine(UserPaths.LogRoot, "runtime.log");

            lock (Sync)
            {
                File.AppendAllText(path, entry);
            }
        }
        catch
        {
            // Logging must never become another crash path.
        }
    }
}
