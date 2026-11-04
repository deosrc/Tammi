using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Windows.PlatformServices;

/// <summary>
/// App folders under <c>%LOCALAPPDATA%\Tammi</c>.
/// </summary>
/// <remarks>
/// Settings and data hold paths on this machine, so they live in the local, not the roaming, app data folder.
/// </remarks>
internal sealed class WindowsAppPaths : IAppPaths
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tammi");

    public string ConfigDirectory { get; } = Path.Combine(Root, "Config");

    public string DataDirectory { get; } = Path.Combine(Root, "Data");

    public string CacheDirectory { get; } = Path.Combine(Root, "Cache");
}
