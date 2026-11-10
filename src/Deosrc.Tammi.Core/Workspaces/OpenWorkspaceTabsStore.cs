using System.Text.Json;
using System.Text.Json.Nodes;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Keeps the workspaces open in tabs in <c>open-workspace-tabs.json</c> in the app's data folder
/// (<see cref="IAppPaths.DataDirectory"/>), so they can be opened again when the app starts. A file that can't be
/// read is treated as holding no tabs.
/// </summary>
public sealed class OpenWorkspaceTabsStore
{
    /// <summary>
    /// The file's name.
    /// </summary>
    public const string FileName = "open-workspace-tabs.json";

    private const int CurrentVersion = 1;

    // None yet: the file is at its first version.
    private static readonly IReadOnlyList<Action<JsonObject>> Migrations = [];

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
            var ids = (file.WorkspaceIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
            var active = ids.Contains(file.ActiveWorkspaceId!) ? file.ActiveWorkspaceId : null;
            return new OpenWorkspaceTabs(ids, active);
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
                new TabsFile(tabs.WorkspaceIds, tabs.ActiveWorkspaceId),
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

    private sealed record TabsFile(IReadOnlyList<string>? WorkspaceIds, string? ActiveWorkspaceId = null);
}
