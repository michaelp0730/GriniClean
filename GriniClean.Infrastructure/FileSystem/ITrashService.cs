namespace GriniClean.Infrastructure.FileSystem;

public interface ITrashService
{
    /// <summary>Moves a file or directory to the user's Trash. Returns the trashed path if successful.</summary>
    string? TryMoveToTrash(string path);
}
