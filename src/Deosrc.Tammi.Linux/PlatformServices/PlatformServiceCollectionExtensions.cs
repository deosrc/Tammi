using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Deosrc.Tammi.Core.Credentials;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Platform.Pty;

namespace Deosrc.Tammi.Linux.PlatformServices;

internal static class PlatformServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Linux implementations of the platform services.
    /// </summary>
    public static IServiceCollection AddLinuxPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAppPaths, XdgAppPaths>();
        services.AddSingleton<IProcessLauncher, LinuxProcessLauncher>();
        services.AddSingleton<IFileSystemWatchService, FileSystemWatchService>();
        services.AddSingleton<ICredentialStore, LibSecretCredentialStore>();
        services.AddSingleton<IPtyService, LinuxPtyService>();
        services.AddSingleton<IFolderPicker>(provider =>
            new PhotinoFolderPicker(provider.GetRequiredService<PhotinoBlazorApp>().MainWindow));
        return services;
    }
}
