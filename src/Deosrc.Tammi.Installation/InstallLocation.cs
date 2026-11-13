namespace Deosrc.Tammi.Installation;

/// <summary>
/// Where installed versions of Tammi live: a folder for each version, inside the user's own app data folder so that
/// no admin rights are needed.
/// </summary>
public sealed class InstallLocation
{
    /// <summary>
    /// The name of the file in each version's folder that records how it was installed.
    /// </summary>
    public const string MetadataFileName = "install.json";

    /// <param name="versionsDirectory">The folder that holds a folder for each version.</param>
    public InstallLocation(string versionsDirectory) => VersionsDirectory = Path.GetFullPath(versionsDirectory);

    /// <summary>
    /// The folder that holds a folder for each version.
    /// </summary>
    public string VersionsDirectory { get; }

    /// <summary>
    /// The location Tammi is installed to by default: <c>%LOCALAPPDATA%\Tammi\Versions</c> on Windows and
    /// <c>$XDG_DATA_HOME/tammi/versions</c> (<c>~/.local/share/tammi/versions</c>) elsewhere.
    /// </summary>
    public static InstallLocation Default() => new(DefaultVersionsDirectory());

    private static string DefaultVersionsDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tammi", "Versions");
        }

        return Path.Combine(XdgDataHome(), "tammi", "versions");
    }

    /// <summary>
    /// <c>$XDG_DATA_HOME</c>, or <c>~/.local/share</c> if it isn't set to an absolute path.
    /// </summary>
    internal static string XdgDataHome()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return !string.IsNullOrEmpty(xdg) && Path.IsPathRooted(xdg)
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
    }

    /// <summary>
    /// The folder a version is installed in.
    /// </summary>
    public string DirectoryFor(PackageVersion version) => Path.Combine(VersionsDirectory, version.ToString());

    /// <summary>
    /// Whether a version is installed completely.
    /// </summary>
    public bool IsInstalled(PackageVersion version) => File.Exists(Path.Combine(DirectoryFor(version), MetadataFileName));

    /// <summary>
    /// The installed versions, newest first. A version that's still being installed isn't included.
    /// </summary>
    public IReadOnlyList<PackageVersion> InstalledVersions()
    {
        if (!Directory.Exists(VersionsDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(VersionsDirectory)
            .Select(directory => Path.GetFileName(directory))
            .Select(name => PackageVersion.TryParse(name, out var version) ? version : null)
            .OfType<PackageVersion>()
            .Where(IsInstalled)
            .OrderByDescending(version => version)
            .ToList();
    }

    /// <summary>
    /// Finds which installed version a folder belongs to, e.g. the folder the running app is in.
    /// </summary>
    /// <returns>The version, or <see langword="null"/> if the folder isn't in a version's folder (e.g. a development build).</returns>
    public PackageVersion? VersionRunningFrom(string directory)
    {
        var relative = Path.GetRelativePath(VersionsDirectory, Path.GetFullPath(directory));
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return null;
        }

        var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return PackageVersion.TryParse(first, out var version) ? version : null;
    }
}
