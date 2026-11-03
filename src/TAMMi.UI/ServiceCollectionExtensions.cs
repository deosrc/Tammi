using Microsoft.Extensions.DependencyInjection;
using TAMMi.Core.Git;
using TAMMi.Core.Workspaces;

namespace TAMMi.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services used by the shared UI. Each host calls this when it starts.
    /// </summary>
    public static IServiceCollection AddTammiUI(this IServiceCollection services)
    {
        // One window per app, so the open tabs are shared app-wide.
        services.AddSingleton<WorkspaceTabs>();
        services.AddSingleton<IGitCli>(_ => new GitCli());
        return services;
    }
}
