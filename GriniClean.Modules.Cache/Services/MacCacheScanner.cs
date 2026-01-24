using GriniClean.Core.Models;
using GriniClean.Infrastructure.FileSystem;
using GriniClean.Infrastructure.OS;

namespace GriniClean.Modules.Cache.Services;

public sealed class MacCacheScanner(IFileSystem fs, IUserPaths userPaths) : ICacheScanner
{
    public IReadOnlyList<CacheTarget> Scan(CacheScanOptions options, CancellationToken cancellationToken)
    {
        var results = new List<CacheTarget>();
        var home = userPaths.HomeDirectory;
        if (string.IsNullOrWhiteSpace(home) || home == "/") return results;

        var userCachesRoot = Path.Combine(home, "Library", "Caches");
        results.AddRange(ScanImmediateChildren(
            rootPath: userCachesRoot,
            kind: CacheTargetKind.UserCachesRootChild,
            isAdvanced: false,
            options,
            cancellationToken
        ));

        // If no child directories exist, still offer the root itself
        if (results.All(r => r.Kind != CacheTargetKind.UserCachesRootChild) && fs.DirectoryExists(userCachesRoot))
        {
            long? size = options.Fast ? null : SafeSize(fs, userCachesRoot, cancellationToken);

            results.Add(new CacheTarget(
                DisplayName: "User Caches (root)",
                Path: userCachesRoot,
                SizeBytes: size,
                Kind: CacheTargetKind.UserCachesRootChild,
                IsAdvanced: false
            ));
        }

        // ~/Library/Containers/*/Data/Library/Caches
        if (options.IncludeContainers)
        {
            var containersRoot = Path.Combine(home, "Library", "Containers");
            if (fs.DirectoryExists(containersRoot))
            {
                foreach (var containerDir in fs.EnumerateDirectories(containersRoot))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var cachesPath = Path.Combine(containerDir, "Data", "Library", "Caches");
                    if (!fs.DirectoryExists(cachesPath))
                        continue;

                    var display = Path.GetFileName(containerDir); // bundle id-like
                    long? size = options.Fast ? null : SafeSize(fs, cachesPath, cancellationToken);

                    results.Add(new CacheTarget(
                        DisplayName: display,
                        Path: cachesPath,
                        SizeBytes: size,
                        Kind: CacheTargetKind.ContainerCaches,
                        IsAdvanced: true
                    ));
                }
            }
        }

        // Sort: non-advanced first, then biggest first where sizes available
        return results
            .OrderBy(r => r.IsAdvanced)
            .ThenByDescending(r => r.SizeBytes ?? -1)
            .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<CacheTarget> ScanImmediateChildren(
        string rootPath,
        CacheTargetKind kind,
        bool isAdvanced,
        CacheScanOptions options,
        CancellationToken cancellationToken)
    {
        if (!fs.DirectoryExists(rootPath)) yield break;

        foreach (var dir in fs.EnumerateDirectories(rootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var display = Path.GetFileName(dir);
            long? size = options.Fast ? null : SafeSize(fs, dir, cancellationToken);

            yield return new CacheTarget(
                DisplayName: display,
                Path: dir,
                SizeBytes: size,
                Kind: kind,
                IsAdvanced: isAdvanced
            );
        }
    }

    private static long? SafeSize(IFileSystem fs, string path, CancellationToken ct)
    {
        try
        {
            return fs.GetDirectorySizeBytes(path, ct);
        }
        catch
        {
            return null;
        }
    }
}
