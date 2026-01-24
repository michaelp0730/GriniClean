namespace GriniClean.Infrastructure.FileSystem;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateDirectories(string path);
    long GetDirectorySizeBytes(string path, CancellationToken cancellationToken);
}
