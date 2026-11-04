using Microsoft.Extensions.DependencyInjection;
using TAMMi.Core.Agents;
using TAMMi.Core.Catalogue;
using TAMMi.Core.Credentials;
using TAMMi.Core.Git;
using TAMMi.Core.Integrations;
using TAMMi.Core.Platform;
using TAMMi.Core.Settings;
using TAMMi.Core.Terminals;
using TAMMi.Core.Windowing;
using TAMMi.Core.Workspaces;
using TAMMi.UI.Workspaces;

namespace TAMMi.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// How long an integration waits for its service to respond, e.g. while Settings checks a token.
    /// </summary>
    private static readonly TimeSpan IntegrationRequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Registers the services used by the shared UI. Each host calls this when it starts.
    /// </summary>
    public static IServiceCollection AddTammiUI(this IServiceCollection services)
    {
        // One window per app, so the open tabs are shared app-wide.
        services.AddSingleton<WorkspaceTabs>();
        services.AddSingleton<IGitCli>(_ => new GitCli());
        services.AddSingleton<IGitRepositoryReader, GitRepositoryReader>();
        services.AddSingleton<IGitWorktreeService, GitWorktreeService>();
        services.AddSingleton(services => new IntegrationManager(
            services.GetRequiredService<ISettingsStore>(),
            services.GetRequiredService<ICredentialStore>(),
            new HttpClient { Timeout = IntegrationRequestTimeout },
            services.GetRequiredService<TimeProvider>()));
        services.AddSingleton(services => services.GetRequiredService<IntegrationManager>().Registry);
        services.AddSingleton<TaskFinder>();
        services.AddSingleton<AgentHookServer>();
        services.AddSingleton(services => new AgentLauncher(
            services.GetRequiredService<IPtyService>(),
            services.GetRequiredService<AgentHookServer>()));
        services.AddSingleton<SupportedAgentChecker>();
        services.AddSingleton<TerminalLauncher>();
        services.AddSingleton<WorkspaceTerminals>();
        services.AddSingleton<WorkspaceViewState>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<WindowStateStore>();
        services.AddSingleton<IWorkspaceStore, WorkspaceStore>();
        services.AddSingleton<WorkspaceGitStatusMonitors>();
        services.AddSingleton<WorkspaceTaskLists>();
        services.AddSingleton<WorkspaceDeleter>();
        services.AddSingleton<WorkspaceCreator>();
        services.AddSingleton<IRepositoryCatalogue, RepositoryCatalogue>();
        return services;
    }
}
