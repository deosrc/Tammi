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
    public async Task<IReadOnlyList<SavedTab>> ListAsync(CancellationToken cancellationToken = default)
    {
        string contents;
        try
        {
            contents = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Couldn't read the saved tabs in '{File}'.", FilePath);
            return [];
        }

        try
        {
            var file = VersionedJsonFile.Deserialize<TabsFile>(contents, Migrations);
            return [.. (file.Tabs ?? []).Where(tab => !string.IsNullOrWhiteSpace(tab?.Kind))];
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            _logger.LogWarning(ex, "Couldn't read the saved tabs in '{File}'.", FilePath);
            return [];
        }
    }

    /// <summary>
    /// Saves the tabs, in place of those saved before.
    /// </summary>
    /// <exception cref="IOException">The tabs couldn't be saved.</exception>
    /// <exception cref="UnauthorizedAccessException">The tabs couldn't be saved.</exception>
    public Task SetAsync(IReadOnlyList<SavedTab> tabs, CancellationToken cancellationToken = default) =>
        VersionedJsonFile.WriteAsync(FilePath, new TabsFile(tabs), Migrations.Count + 1, cancellationToken);

    private sealed record TabsFile(IReadOnlyList<SavedTab>? Tabs);
}
