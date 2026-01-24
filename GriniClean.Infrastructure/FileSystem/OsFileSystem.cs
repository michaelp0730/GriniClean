namespace GriniClean.Infrastructure.FileSystem;

public sealed class OsFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateDirectories(string path)
    {
        try
        {
            // Materialize inside try/catch so exceptions during enumeration are handled.
            return Directory.EnumerateDirectories(path).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public long GetDirectorySizeBytes(string path, CancellationToken cancellationToken)
    {
        // NOTE: we avoid following symlinks by skipping reparse points / links.
        // On macOS, FileAttributes.ReparsePoint isn't always reliable for symlinks,
        // so we use LinkTarget when available.
        long total = 0;

        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = stack.Pop();

            // Skip symlinked directories (avoid escaping the tree)
            try
            {
                var dirInfo = new DirectoryInfo(current);
#if NET8_0_OR_GREATER
                if (dirInfo.LinkTarget is not null) continue;
#endif
            }
            catch
            {
                continue;
            }

            // Files
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fileInfo = new FileInfo(file);
#if NET8_0_OR_GREATER
                        if (fileInfo.LinkTarget is not null) continue;
#endif
                        total += fileInfo.Length;
                    }
                    catch
                    {
                        // ignore unreadable files
                    }
                }
            }
            catch
            {
                // ignore unreadable directories
            }

            // Subdirectories
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stack.Push(dir);
                }
            }
            catch
            {
                // ignore
            }
        }

        return total;
    }
}
