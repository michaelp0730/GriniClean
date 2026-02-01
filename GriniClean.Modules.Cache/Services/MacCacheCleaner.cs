using GriniClean.Core.Models;
using GriniClean.Infrastructure.FileSystem;

namespace GriniClean.Modules.Cache.Services;

public sealed class MacCacheCleaner(ITrashService trash) : ICacheCleaner
{
    public CacheCleanResult MoveToTrash(IReadOnlyList<CacheTarget> targets, bool dryRun, CancellationToken ct)
    {
        var failed = new List<string>();
        var trashed = 0;

        foreach (var t in targets)
        {
            ct.ThrowIfCancellationRequested();

            if (dryRun) continue;

            var trashedPath = trash.TryMoveToTrash(t.Path);
            if (trashedPath is not null)
            {
                trashed++;
            }
            else
            {
                // If it's already gone, consider it effectively cleaned
                if (!Directory.Exists(t.Path) && !File.Exists(t.Path))
                {
                    trashed++;
                }
                else
                {
                    failed.Add(t.Path);
                }
            }
        }

        return new CacheCleanResult(
            Requested: targets.Count,
            Trashed: trashed,
            Failed: failed.Count,
            FailedPaths: failed
        );
    }
}
