namespace GriniClean.Core.Models;

public sealed record CacheTarget(
    string DisplayName,
    string Path,
    long? SizeBytes,
    CacheTargetKind Kind,
    bool IsAdvanced
);

public enum CacheTargetKind
{
    UserCachesRootChild,
    ContainerCaches
}
