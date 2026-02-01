using System.Diagnostics;

namespace GriniClean.Infrastructure.OS;

public sealed class MacProcessService : IProcessService
{
    public bool IsProcessRunning(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        // pgrep -ix matches exact name case-insensitively; exit code 0 if found
        return TryRun("/usr/bin/pgrep", new[] { "-ix", processName });
    }

    public bool IsAppRunningByBundleId(string bundleId)
    {
        if (string.IsNullOrWhiteSpace(bundleId)) return false;

        // AppleScript: application id "com.operasoftware.Opera" is running
        var script = $"tell application \"System Events\" to (exists (processes whose bundle identifier is \"{bundleId}\"))";

        var output = TryRunCapture("/usr/bin/osascript", new[] { "-e", script });
        return output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
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
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null) return false;

            p.WaitForExit(3_000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string TryRunCapture(string fileName, IEnumerable<string> args)
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
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null) return string.Empty;

            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3_000);
            return stdout ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
