using Microsoft.Extensions.DependencyInjection;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Catalogue;
using Deosrc.Tammi.Core.Credentials;
using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Terminals;
using Deosrc.Tammi.Core.Tools;
using Deosrc.Tammi.Core.Windowing;
using Deosrc.Tammi.Core.Workspaces;
using Deosrc.Tammi.UI.Workspaces;
using Microsoft.Extensions.Logging;

namespace Deosrc.Tammi.UI;

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
            services.GetRequiredService<ILoggerFactory>(),
            services.GetRequiredService<TimeProvider>()));
        services.AddSingleton(services => services.GetRequiredService<IntegrationManager>().Registry);
        services.AddSingleton<TaskFinder>();
        services.AddSingleton<AgentHookServer>();
        services.AddSingleton(services => new AgentLauncher(
            services.GetRequiredService<IPtyService>(),
            services.GetRequiredService<AgentHookServer>()));
        services.AddSingleton<SupportedAgentChecker>();
        services.AddSingleton<BuiltInToolFinder>();
        services.AddSingleton<WorkspaceTools>();
        services.AddSingleton<TerminalLauncher>();
        services.AddSingleton<WorkspaceTerminals>();
        services.AddSingleton<WorkspaceAgents>();
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
