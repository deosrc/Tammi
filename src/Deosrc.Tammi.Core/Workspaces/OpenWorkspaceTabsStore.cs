using System.Text.Json;
using System.Text.Json.Nodes;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Keeps the workspaces and repository git graphs open in tabs in <c>open-workspace-tabs.json</c> in the app's data folder
/// (<see cref="IAppPaths.DataDirectory"/>), so they can be opened again when the app starts. A file that can't be
/// read is treated as holding no tabs.
/// </summary>
public sealed class OpenWorkspaceTabsStore
{
    /// <summary>
    /// The file's name.
    /// </summary>
    public const string FileName = "open-workspace-tabs.json";

    // Version 2 keeps the tabs in one list, in tab order, in the shape shared by every tab control (see SavedTab).
    private static readonly IReadOnlyList<Action<JsonObject>> Migrations = [MigrateToShared];

    private static int CurrentVersion => Migrations.Count + 1;

    private readonly string _directory;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a store for the file in the app's data folder.
    /// </summary>
    public OpenWorkspaceTabsStore(IAppPaths appPaths, ILogger<OpenWorkspaceTabsStore>? logger = null)
        : this(appPaths.DataDirectory, logger)
    {
    }

    /// <summary>
    /// Creates a store for the file in <paramref name="directory"/>.
    /// </summary>
    internal OpenWorkspaceTabsStore(string directory, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// The file's full path.
    /// </summary>
    public string FilePath => Path.Combine(_directory, FileName);

    /// <summary>
    /// Reads the kept tabs: none if the file is missing or can't be read.
    /// </summary>
    public async Task<OpenWorkspaceTabs> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var contents = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
            var file = VersionedJsonFile.Deserialize<TabsFile>(contents, Migrations);
            var tabs = new List<SavedTab>();
            foreach (var tab in file.Tabs ?? [])
            {
                var kept = Clean(tab);
                if (kept is not null && !tabs.Any(kept.IsSameTab))
                {
                    tabs.Add(kept);
                }
            }

            return new OpenWorkspaceTabs(tabs, tabs.Find(tab => tab.IsSameTab(file.Active)));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return OpenWorkspaceTabs.None;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Couldn't read the open workspace tabs in '{File}'.", FilePath);
            return OpenWorkspaceTabs.None;
        }
    }

    /// <summary>
    /// Keeps the tabs, in place of those kept before.
    /// </summary>
    /// <returns>Whether they were saved. Failing to save only means they aren't opened again next time.</returns>
    public async Task<bool> SaveAsync(OpenWorkspaceTabs tabs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        try
        {
            await VersionedJsonFile.WriteAsync(
                FilePath,
                new TabsFile(tabs.Tabs, tabs.Active),
                CurrentVersion,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Couldn't save the open workspace tabs to '{File}'.", FilePath);
            return false;
        }
    }

    // A workspace tab needs an ID and a repository tab a path. A repository's own tabs are git graphs, each with a path.
    private static SavedTab? Clean(SavedTab? tab)
    {
        switch (tab?.Kind)
        {
            case SavedTab.WorkspaceKind when !string.IsNullOrWhiteSpace(tab.Id):
                return new SavedTab(SavedTab.WorkspaceKind, Id: tab.Id);

            case SavedTab.RepositoryKind when !string.IsNullOrWhiteSpace(tab.Path):
                var graphs = new List<SavedTab>();
                foreach (var graph in tab.Tabs ?? [])
                {
                    if (graph is { Kind: SavedTab.GitGraphKind, Path: { } path } && !string.IsNullOrWhiteSpace(path)
                        && !graphs.Any(graph.IsSameTab))
                    {
                        graphs.Add(new SavedTab(SavedTab.GitGraphKind, Path: path));
                    }
                }

                return new SavedTab(
                    SavedTab.RepositoryKind,
                    Path: tab.Path,
                    Tabs: graphs.Count > 0 ? graphs : null,
                    Active: graphs.Find(graph => graph.IsSameTab(tab.Active)));

            default:
                return null;
        }
    }

    private static void MigrateToShared(JsonObject json)
    {
        var tabs = new JsonArray();
        var active = (JsonNode?)null;
        var activeWorkspaceId = json["activeWorkspaceId"]?.GetValue<string>();
        var activeRepositoryPath = json["activeRepositoryPath"]?.GetValue<string>();
        foreach (var id in (json["workspaceIds"] as JsonArray ?? []).Select(node => node?.GetValue<string>()))
        {
            var tab = new JsonObject { ["kind"] = SavedTab.WorkspaceKind, ["id"] = id };
            tabs.Add(tab);
            if (id is not null && id == activeWorkspaceId)
            {
                active = tab.DeepClone();
            }
        }

        var secondary = json["secondaryGraphPaths"] as JsonObject;
        var shown = json["shownGraphPaths"] as JsonObject;
        foreach (var path in (json["repositoryPaths"] as JsonArray ?? []).Select(node => node?.GetValue<string>()))
        {
            var tab = new JsonObject { ["kind"] = SavedTab.RepositoryKind, ["path"] = path };
            if (path is not null && secondary?[path] is JsonArray graphs && graphs.Count > 0)
            {
                tab["tabs"] = new JsonArray([.. graphs.Select(graph =>
                    (JsonNode)new JsonObject { ["kind"] = SavedTab.GitGraphKind, ["path"] = graph?.GetValue<string>() })]);
                if (shown?[path]?.GetValue<string>() is { } shownPath)
                {
                    tab["active"] = new JsonObject { ["kind"] = SavedTab.GitGraphKind, ["path"] = shownPath };
                }
            }

            tabs.Add(tab);
            if (path is not null && path == activeRepositoryPath && active is null)
            {
                active = new JsonObject { ["kind"] = SavedTab.RepositoryKind, ["path"] = path };
            }
        }

        foreach (var old in new[] { "workspaceIds", "activeWorkspaceId", "repositoryPaths", "activeRepositoryPath", "secondaryGraphPaths", "shownGraphPaths" })
        {
            json.Remove(old);
        }

        json["tabs"] = tabs;
        if (active is not null)
        {
            json["active"] = active;
        }
    }

    private sealed record TabsFile(IReadOnlyList<SavedTab>? Tabs, SavedTab? Active = null);
}
