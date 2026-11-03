using Microsoft.Extensions.DependencyInjection;
using TAMMi.Core.Platform;
using TAMMi.Core.Platform.Pty;

namespace TAMMi.Windows.PlatformServices;

internal static class PlatformServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Windows implementations of the platform services.
    /// </summary>
    public static IServiceCollection AddWindowsPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAppPaths, WindowsAppPaths>();
        services.AddSingleton<IProcessLauncher, WindowsProcessLauncher>();
        services.AddSingleton<IFileSystemWatchService, FileSystemWatchService>();
        services.AddSingleton<IFolderPicker, WindowsFolderPicker>();
        services.AddSingleton<IPtyService, WindowsPtyService>();
        return services;
    }
}
