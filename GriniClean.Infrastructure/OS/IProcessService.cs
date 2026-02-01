namespace GriniClean.Infrastructure.OS;

public interface IProcessService
{
    bool IsProcessRunning(string processName);
    bool IsAppRunningByBundleId(string bundleId);
}
