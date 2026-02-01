using GriniClean.Core.Models;

namespace GriniClean.Modules.Cache.Services;

public interface ICacheCleaner
{
    CacheCleanResult MoveToTrash(IReadOnlyList<CacheTarget> targets, bool dryRun, CancellationToken ct);
}

public sealed record CacheCleanResult(
    int Requested,
    int Trashed,
    int Failed,
    IReadOnlyList<string> FailedPaths
);
