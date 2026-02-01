using System.Diagnostics;

namespace GriniClean.Infrastructure.FileSystem;

public sealed class MacTrashService : ITrashService
{
    public string? TryMoveToTrash(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (!Directory.Exists(path) && !File.Exists(path)) return null;

        // Strategy 1: Homebrew `trash` command (most reliable for CLI)
        // `trash` comes from: brew install trash
        if (TryRun("/usr/local/bin/trash", new[] { path }) ||
            TryRun("/opt/homebrew/bin/trash", new[] { path }) ||
            TryRun("trash", new[] { path })) // if on PATH
        {
            return path;
        }

        // Strategy 2: Finder via AppleScript (may require Automation permission)
        if (TryFinderDelete(path))
            return path;

        return null;
    }

    private static bool TryFinderDelete(string path)
    {
        var escaped = path.Replace("\"", "\\\"");
        var script = $"tell application \"Finder\" to delete POSIX file \"{escaped}\"";

        return TryRun("/usr/bin/osascript", new[] { "-e", script });
    }

    private static bool TryRun(string fileName, IEnumerable<string> args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null) return false;

            p.WaitForExit(15_000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
