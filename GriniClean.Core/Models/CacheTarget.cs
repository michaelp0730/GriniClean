namespace GriniClean.Core.Models;

public sealed record CacheTarget(
    string DisplayName,
    string Path,
    long? SizeBytes,
    CacheTargetKind Kind,
    bool IsAdvanced,
    bool IsApple
);

public enum CacheTargetKind
{
    UserCachesRootChild,
    ContainerCaches
}
