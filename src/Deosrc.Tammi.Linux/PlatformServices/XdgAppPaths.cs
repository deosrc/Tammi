using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Linux.PlatformServices;

/// <summary>
/// App folders under the XDG base directories, e.g. <c>~/.config/tammi</c>.
/// </summary>
/// <remarks>
/// See the <see href="https://specifications.freedesktop.org/basedir-spec/latest/">XDG Base Directory
/// Specification</see>.
/// </remarks>
internal sealed class XdgAppPaths : IAppPaths
{
    private const string AppFolderName = "tammi";

    public string ConfigDirectory { get; } = Resolve("XDG_CONFIG_HOME", ".config");

    public string DataDirectory { get; } = Resolve("XDG_DATA_HOME", Path.Combine(".local", "share"));

    public string CacheDirectory { get; } = Resolve("XDG_CACHE_HOME", ".cache");

    // The specification puts logs in the state directory.
    public string LogDirectory { get; } = Path.Combine(
        Resolve("XDG_STATE_HOME", Path.Combine(".local", "state")),
        "logs");

    private static string Resolve(string variable, string defaultUnderHome)
    {
        // The specification says to ignore relative paths.
        var baseDirectory = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(baseDirectory) || !Path.IsPathRooted(baseDirectory))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            baseDirectory = Path.Combine(home, defaultUnderHome);
        }

        return Path.Combine(baseDirectory, AppFolderName);
    }
}
