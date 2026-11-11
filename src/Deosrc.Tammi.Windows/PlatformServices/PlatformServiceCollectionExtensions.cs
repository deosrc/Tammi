using Microsoft.Extensions.DependencyInjection;
using Deosrc.Tammi.Core.Credentials;
using Deosrc.Tammi.Core.Platform;
using IFilePicker = Deosrc.Tammi.Core.Platform.IFilePicker;
using Deosrc.Tammi.Core.Platform.Pty;
using Deosrc.Tammi.UI.Visualiser;

namespace Deosrc.Tammi.Windows.PlatformServices;

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
        services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
        services.AddSingleton<IFolderPicker, WindowsFolderPicker>();
        services.AddSingleton<IFilePicker, WindowsFilePicker>();
        services.AddSingleton<IPtyService, WindowsPtyService>();
        services.AddSingleton<IApplicationIconProvider, WindowsApplicationIconProvider>();
        services.AddSingleton<IVisualiserWindow, MauiVisualiserWindow>();
        return services;
    }
}
