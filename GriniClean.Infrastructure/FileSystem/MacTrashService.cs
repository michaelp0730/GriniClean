using System.Diagnostics;

namespace GriniClean.Infrastructure.FileSystem;

public sealed class MacTrashService : ITrashService
{
    public string? TryMoveToTrash(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (!Directory.Exists(path) && !File.Exists(path)) return null;

        // Escape quotes for AppleScript string literal
        var escaped = path.Replace("\"", "\\\"");

        // This moves the item to Trash.
        // Note: AppleScript doesn't provide the final trashed path easily; return original path on success.
        var script = $"tell application \"Finder\" to delete POSIX file \"{escaped}\"";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                ArgumentList = { "-e", script },
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var p = Process.Start(psi);
            if (p is null) return null;

            p.WaitForExit(15_000);
            if (p.ExitCode == 0) return path;

            return null;
        }
        catch
        {
            return null;
        }
    }
}
