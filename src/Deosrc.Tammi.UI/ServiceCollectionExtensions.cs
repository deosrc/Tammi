using Microsoft.Extensions.DependencyInjection;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Catalogue;
using Deosrc.Tammi.Core.Credentials;
using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Ssh;
using Deosrc.Tammi.Core.Terminals;
using Deosrc.Tammi.Core.Tools;
using Deosrc.Tammi.Core.Windowing;
using Deosrc.Tammi.Core.Workspaces;
using Deosrc.Tammi.UI.Components;
using Deosrc.Tammi.UI.Logging;
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
        services.AddTammiLogging();

        // One window per app, so the open tabs are shared app-wide.
        services.AddSingleton<WorkspaceTabs>();
        services.AddSingleton<OpenWorkspaceTabsStore>();
        services.AddSingleton<WorkspaceTabsKeeper>();
        services.AddSingleton<IGitCli>(services => new GitCli(logger: services.GetRequiredService<ILogger<GitCli>>()));
        services.AddSingleton<IGitUserConfig>(services => new GitUserConfig(services.GetRequiredService<IGitCli>()));
        services.AddSingleton<ISshKeyStore>(services => new SshKeyStore(
            services.GetRequiredService<IProcessLauncher>(),
            logger: services.GetRequiredService<ILogger<SshKeyStore>>()));
        services.AddScoped<Clipboard>();
        services.AddSingleton<IGitRepositoryReader, GitRepositoryReader>();
        services.AddSingleton<IGitSync>(services => new GitSync(services.GetRequiredService<IGitCli>()));
        services.AddSingleton<IGitWorkingTree>(services => new GitWorkingTree(services.GetRequiredService<IGitCli>()));
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
        services.AddSingleton<ToolImages>();
        services.AddSingleton<WorkspaceTools>();
        services.AddSingleton<TerminalLauncher>();
        services.AddSingleton<WorkspaceTerminals>();
        services.AddSingleton<WorkspaceAgents>();
        services.AddSingleton<WorkspaceGitGraphs>();
        services.AddSingleton<WorkspaceTabRestorer>();
        services.AddSingleton<WorkspaceViewState>();
        services.AddSingleton<GitGraphViewOptions>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<WindowStateStore>();
        services.AddSingleton<LayoutStateStore>();
        services.AddSingleton<IWorkspaceStore, WorkspaceStore>();
        services.AddSingleton<WorkspaceGitStatusMonitors>();
        services.AddSingleton<WorkspaceTaskLists>();
        services.AddSingleton(services => new WorkspacePullRequestLists(
            services.GetRequiredService<WorkspaceTabs>(),
            services.GetRequiredService<IntegrationRegistry>(),
            services.GetRequiredService<IGitRepositoryReader>(),
            services.GetRequiredService<TimeProvider>()));
        services.AddSingleton<WorkspaceDeleter>();
        services.AddSingleton<WorkspaceCreator>();
        services.AddSingleton<IRepositoryCatalogue, RepositoryCatalogue>();
        return services;
    }
}
