using System.Text.Json;
using System.Text.Json.Nodes;
using Deosrc.Tammi.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Keeps a workspace's <see cref="SavedTab"/>s in a versioned JSON file, <c>.tammi/open-tabs.json</c> in its Workspace
/// Folder. A file that can't be read is treated as holding no tabs.
/// </summary>
public sealed class SavedTabsFileStore(string filePath, ILogger? logger = null)
{
    /// <summary>
    /// The file's usual name.
    /// </summary>
    public const string DefaultFileName = "open-tabs.json";

    private static readonly IReadOnlyList<Action<JsonObject>> Migrations = [];

    private readonly ILogger _logger = logger ?? NullLogger.Instance;

    /// <summary>
    /// The file's full path.
    /// </summary>
    public string FilePath { get; } = Path.GetFullPath(filePath);

    /// <summary>
    /// Lists the saved tabs, in tab order.
    /// </summary>
    public async Task<IReadOnlyList<SavedTab>> ListAsync(CancellationToken cancellationToken = default) =>
        (await ReadAsync(cancellationToken).ConfigureAwait(false)).Tabs;

    /// <summary>
    /// Reads the saved tabs and the one that was shown, which is <c>null</c> if the dashboard (or a tab that isn't
    /// saved, like a terminal) was.
    /// </summary>
    public async Task<(IReadOnlyList<SavedTab> Tabs, SavedTab? Active)> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        string contents;
        try
        {
            contents = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return ([], null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Couldn't read the saved tabs in '{File}'.", FilePath);
            return ([], null);
        }

        try
        {
            var file = VersionedJsonFile.Deserialize<TabsFile>(contents, Migrations);
            var active = file.Active is { } shown && !string.IsNullOrWhiteSpace(shown.Kind) ? shown : null;
            return ([.. (file.Tabs ?? []).Where(tab => !string.IsNullOrWhiteSpace(tab?.Kind))], active);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            _logger.LogWarning(ex, "Couldn't read the saved tabs in '{File}'.", FilePath);
            return ([], null);
        }
    }

    /// <summary>
    /// Saves the tabs and the one that is shown (<c>null</c> for the dashboard), in place of those saved before.
    /// </summary>
    /// <exception cref="IOException">The tabs couldn't be saved.</exception>
    /// <exception cref="UnauthorizedAccessException">The tabs couldn't be saved.</exception>
    public Task SetAsync(
        IReadOnlyList<SavedTab> tabs,
        SavedTab? active = null,
        CancellationToken cancellationToken = default) =>
        VersionedJsonFile.WriteAsync(FilePath, new TabsFile(tabs, active), Migrations.Count + 1, cancellationToken);

    private sealed record TabsFile(IReadOnlyList<SavedTab>? Tabs, SavedTab? Active = null);
}
