using Microsoft.Extensions.DependencyInjection;
using Deosrc.Tammi.Core;
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
using Deosrc.Tammi.Core.Usage;
using Deosrc.Tammi.Core.Windowing;
using Deosrc.Tammi.Core.Workspaces;
using Deosrc.Tammi.UI.Components;
using Deosrc.Tammi.UI.Layout;
using Deosrc.Tammi.Installation;
using Deosrc.Tammi.UI.Logging;
using Deosrc.Tammi.Core.Updates;
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
    /// How long a request to check for or download an update waits. Downloads are large, so it's long.
    /// </summary>
    private static readonly TimeSpan UpdateRequestTimeout = TimeSpan.FromMinutes(10);

    /// <returns>The OS and architecture releases are downloaded for, or <see langword="null"/> if there are none.</returns>
    private static ReleaseTarget? CurrentReleaseTarget()
    {
        try
        {
            return ReleaseTarget.Current();
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

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
        services.AddSingleton<IAgentUsageProvider>(services => new ClaudeUsageProvider(
            new HttpClient { Timeout = IntegrationRequestTimeout },
            time: services.GetRequiredService<TimeProvider>()));
        services.AddSingleton<AgentUsageMonitor>();
        services.AddSingleton(services => new AppUpdater(
            new HttpClient { Timeout = UpdateRequestTimeout, DefaultRequestHeaders = { UserAgent = { new("Tammi", AppInfo.Version) } } },
            InstallLocation.Default(),
            CurrentReleaseTarget(),
            services.GetRequiredService<ISettingsStore>(),
            services.GetRequiredService<ILogger<AppUpdater>>()));
        services.AddSingleton<UsageTabState>();
        services.AddSingleton<NewWorkspaceTabState>();
        services.AddSingleton<EditWorkspaceSessions>();
        services.AddSingleton<ToolImages>();
        services.AddSingleton<WorkspaceTools>();
        services.AddSingleton<TerminalLauncher>();
        services.AddSingleton<WorkspaceTerminals>();
        services.AddSingleton<WorkspaceAgents>();
        services.AddSingleton<WorkspaceGitGraphs>();
        services.AddSingleton<WorkspaceHistoryTabs>();
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
        services.AddSingleton<HomeGitStatus>();
        services.AddSingleton<WorkspaceDeleter>();
        services.AddSingleton<WorkspaceCreator>();
        services.AddSingleton<WorkspaceEditor>();
        services.AddSingleton<IRepositoryCatalogue, RepositoryCatalogue>();
        return services;
    }
}
