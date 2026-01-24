namespace GriniClean.Infrastructure.OS;

public sealed class MacUserPaths : IUserPaths
{
    public string HomeDirectory
    {
        get
        {
            // Preferred: UserProfile
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
                return userProfile;

            // Fallback: HOME env var
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrWhiteSpace(home))
                return home;

            // Last resort (should never happen)
            return "/";
        }
    }
}
