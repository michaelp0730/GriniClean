using GriniClean.Core.Models;

namespace GriniClean.Modules.Cache.Services;

public interface ICacheScanner
{
    IReadOnlyList<CacheTarget> Scan(CacheScanOptions options, CancellationToken cancellationToken);
}

public sealed record CacheScanOptions(
    bool Fast,
    bool IncludeContainers
);
