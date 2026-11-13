namespace Deosrc.Tammi.Installation;

/// <summary>
/// Deletes old installed versions.
/// </summary>
public static class VersionCleaner
{
    /// <summary>
    /// How many versions older than the running one are kept, so there's something to go back to.
    /// </summary>
    public const int DefaultPreviousVersionsToKeep = 2;

    /// <summary>
    /// Deletes installed versions, keeping the running version, any newer ones (installed by an update, not yet
    /// restarted into) and the newest few older ones. Folders that aren't versions, and installs that aren't
    /// complete, are left alone.
    /// </summary>
    /// <param name="location">Where versions are installed.</param>
    /// <param name="running">The version that's running. Nothing is deleted without it, so a development build never cleans up.</param>
    /// <param name="previousToKeep">How many versions older than <paramref name="running"/> to keep.</param>
    /// <returns>The versions deleted. A version that couldn't be deleted, e.g. because a file's in use, isn't included.</returns>
    public static IReadOnlyList<PackageVersion> Clean(
        InstallLocation location,
        PackageVersion? running,
        int previousToKeep = DefaultPreviousVersionsToKeep)
    {
        if (running is null)
        {
            return [];
        }

        var older = location.InstalledVersions().Where(version => version < running).ToList();
        var deleted = new List<PackageVersion>();
        // InstalledVersions is newest first.
        foreach (var version in older.Skip(Math.Max(previousToKeep, 0)))
        {
            try
            {
                Directory.Delete(location.DirectoryFor(version), recursive: true);
                deleted.Add(version);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // It's tried again next time Tammi starts.
            }
        }

        return deleted;
    }
}
