using System.Text.Json;
using System.Text.Json.Nodes;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Keeps each workspace's metadata in <c>.tammi/workspace.json</c> in its Workspace Folder, and the index of known
/// workspaces in <c>workspaces.json</c> in the app's data folder (<see cref="IAppPaths.DataDirectory"/>).
/// </summary>
/// <remarks>
/// <para>
/// Both files are versioned like the settings file: when a layout changes, its version goes up and a migration is
/// added, which upgrades files written by older versions of Tammi when they're read. A file from a newer version of
/// Tammi is read as far as possible.
/// </para>
/// <para>
/// The metadata doesn't hold the Workspace Folder's path, so a moved workspace still reads correctly. The index holds
/// each workspace's ID, name and folder, so a workspace whose folder is missing can still be listed by name. If the
/// index can't be read, no workspaces are known and the file is copied to <c>workspaces.invalid.json</c>, so the next
/// save doesn't lose it.
/// </para>
/// </remarks>
public sealed class WorkspaceStore : IWorkspaceStore
{
    /// <summary>
    /// The metadata file's name, in the workspace's <c>.tammi/</c> folder.
    /// </summary>
    public const string MetadataFileName = "workspace.json";

    /// <summary>
    /// The index file's name, in the app's data folder.
    /// </summary>
    public const string IndexFileName = "workspaces.json";

    /// <summary>
    /// The name of the copy kept of an index file that couldn't be read.
    /// </summary>
    public const string InvalidIndexFileName = "workspaces.invalid.json";

    /// <summary>
    /// The metadata file migrations (see <see cref="VersionedJsonFile"/>).
    /// </summary>
    private static readonly IReadOnlyList<Action<JsonObject>> MetadataMigrations = [];

    /// <summary>
    /// The index file migrations (see <see cref="VersionedJsonFile"/>).
    /// </summary>
    private static readonly IReadOnlyList<Action<JsonObject>> IndexMigrations = [];

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly string _dataDirectory;
    private readonly IReadOnlyList<Action<JsonObject>> _metadataMigrations;
    private readonly IReadOnlyList<Action<JsonObject>> _indexMigrations;
    private readonly ILogger _logger;
    private readonly Lock _loadLock = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private volatile IReadOnlyList<IndexEntry>? _index;
    private string? _indexProblem;

    /// <summary>
    /// Creates a store with its index in the app's data folder.
    /// </summary>
    public WorkspaceStore(IAppPaths appPaths, ILogger<WorkspaceStore>? logger = null)
        : this(appPaths.DataDirectory, MetadataMigrations, IndexMigrations, logger)
    {
    }

    /// <summary>
    /// Creates a store with its index in <paramref name="dataDirectory"/>, with the given migrations.
    /// </summary>
    internal WorkspaceStore(
        string dataDirectory,
        IReadOnlyList<Action<JsonObject>> metadataMigrations,
        IReadOnlyList<Action<JsonObject>> indexMigrations,
        ILogger<WorkspaceStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _logger = logger ?? NullLogger<WorkspaceStore>.Instance;
        _dataDirectory = dataDirectory;
        _metadataMigrations = metadataMigrations;
        _indexMigrations = indexMigrations;
    }

    /// <summary>
    /// The version of the metadata file this store writes.
    /// </summary>
    public int CurrentMetadataVersion => _metadataMigrations.Count + 1;

    /// <summary>
    /// The version of the index file this store writes.
    /// </summary>
    public int CurrentIndexVersion => _indexMigrations.Count + 1;

    /// <summary>
    /// The index file's full path.
    /// </summary>
    public string IndexFilePath => Path.Combine(_dataDirectory, IndexFileName);

    /// <inheritdoc />
    public string? IndexProblem
    {
        get
        {
            _ = Index;
            return _indexProblem;
        }
    }

    /// <inheritdoc />
    public event Action? Changed;

    private IReadOnlyList<IndexEntry> Index => _index ?? LoadIndex();

    /// <summary>
    /// Returns the full path of the metadata file in a Workspace Folder.
    /// </summary>
    public static string GetMetadataFilePath(string folder) =>
        Path.Combine(folder, Workspace.MetadataFolderName, MetadataFileName);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkspaceListing>> ListAsync(CancellationToken cancellationToken = default)
    {
        var listings = Index.Select(entry => ListAsync(entry, cancellationToken));
        return await Task.WhenAll(listings).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkspaceListing?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Index.FirstOrDefault(entry => entry.Id == id) is { } entry
            ? await ListAsync(entry, cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <inheritdoc />
    public Task<Workspace> LoadAsync(string folder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        return ReadAsync(NormalizeFolder(folder), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Workspace> SaveAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        workspace = Normalize(workspace);
        if (Validate(workspace) is { } problem)
        {
            throw new ArgumentException(problem, nameof(workspace));
        }

        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureFolderIsFreeAsync(workspace, cancellationToken).ConfigureAwait(false);

            try
            {
                await VersionedJsonFile.WriteAsync(
                    GetMetadataFilePath(workspace.Folder),
                    ToFile(workspace),
                    CurrentMetadataVersion,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new WorkspaceException(
                    $"Couldn't save the workspace '{workspace.Name}' in '{workspace.Folder}'. {ex.Message}",
                    workspace.Folder,
                    ex);
            }

            await AddToIndexAsync(workspace, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }

        Changed?.Invoke();
        return workspace;
    }

    /// <inheritdoc />
    public async Task<Workspace> AddExistingAsync(string folder, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadAsync(folder, cancellationToken).ConfigureAwait(false);

        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AddToIndexAsync(workspace, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }

        Changed?.Invoke();
        return workspace;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveFromIndexAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = Index;
            var updated = index.Where(entry => entry.Id != id).ToList();
            if (updated.Count == index.Count)
            {
                return false;
            }

            await WriteIndexAsync(updated, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }

        Changed?.Invoke();
        return true;
    }

    private async Task<WorkspaceListing> ListAsync(IndexEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await ReadAsync(entry.Folder, cancellationToken).ConfigureAwait(false);
            if (workspace.Id != entry.Id)
            {
                return new WorkspaceListing(entry.Id, entry.Name, entry.Folder, WorkspaceAvailability.Unreadable)
                {
                    Problem = $"The folder '{entry.Folder}' now holds a different workspace, '{workspace.Name}'.",
                };
            }

            return new WorkspaceListing(entry.Id, workspace.Name, entry.Folder, WorkspaceAvailability.Available)
            {
                Workspace = workspace,
            };
        }
        catch (WorkspaceNotFoundException ex)
        {
            _logger.LogDebug("The workspace {Name} ({Id}) is missing: {Problem}", entry.Name, entry.Id, ex.Message);
            return new WorkspaceListing(entry.Id, entry.Name, entry.Folder, WorkspaceAvailability.Missing)
            {
                Problem = ex.Message,
            };
        }
        catch (InvalidWorkspaceMetadataException ex)
        {
            _logger.LogWarning(
                "Couldn't read the workspace {Name} ({Id}): {Problem}", entry.Name, entry.Id, ex.Message);
            return new WorkspaceListing(entry.Id, entry.Name, entry.Folder, WorkspaceAvailability.Unreadable)
            {
                Problem = ex.Message,
            };
        }
    }

    /// <summary>
    /// Reads the workspace in a folder, given as a normalized full path.
    /// </summary>
    private async Task<Workspace> ReadAsync(string folder, CancellationToken cancellationToken)
    {
        var path = GetMetadataFilePath(folder);
        if (!Directory.Exists(folder))
        {
            throw new WorkspaceNotFoundException($"The workspace folder '{folder}' doesn't exist.", folder);
        }

        string contents;
        try
        {
            contents = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new WorkspaceNotFoundException(
                $"The folder '{folder}' isn't a workspace: it has no '{Workspace.MetadataFolderName}/{MetadataFileName}'.",
                folder,
                ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidWorkspaceMetadataException(
                $"Couldn't read the workspace metadata '{path}'. {ex.Message}", folder, ex);
        }

        Workspace workspace;
        try
        {
            var file = VersionedJsonFile.Deserialize<MetadataFile>(contents, _metadataMigrations);
            workspace = FromFile(file, folder);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw new InvalidWorkspaceMetadataException(
                $"The workspace metadata '{path}' isn't valid. {ex.Message}", folder, ex);
        }

        if (Validate(workspace) is { } problem)
        {
            throw new InvalidWorkspaceMetadataException(
                $"The workspace metadata '{path}' isn't valid. {problem}", folder);
        }

        return workspace;
    }

    /// <summary>
    /// Throws if the workspace's folder holds the metadata of a different workspace.
    /// </summary>
    private async Task EnsureFolderIsFreeAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        Workspace existing;
        try
        {
            existing = await ReadAsync(workspace.Folder, cancellationToken).ConfigureAwait(false);
        }
        catch (WorkspaceException)
        {
            // No workspace there, or one that can't be read: saving replaces it.
            return;
        }

        if (existing.Id != workspace.Id)
        {
            throw new WorkspaceException(
                $"The folder '{workspace.Folder}' already holds a different workspace, '{existing.Name}'.",
                workspace.Folder);
        }
    }

    /// <summary>
    /// Adds a workspace to the index, or updates its entry. An entry for the same ID or the same folder is replaced,
    /// in place. Call it while holding the save lock.
    /// </summary>
    private async Task AddToIndexAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        var entry = new IndexEntry(workspace.Id, workspace.Name, workspace.Folder);
        var index = Index;
        var position = -1;
        var updated = new List<IndexEntry>(index.Count + 1);
        foreach (var existing in index)
        {
            if (existing.Id == entry.Id || PathComparer.Equals(existing.Folder, entry.Folder))
            {
                if (position < 0)
                {
                    position = updated.Count;
                    updated.Add(entry);
                }

                continue;
            }

            updated.Add(existing);
        }

        if (position < 0)
        {
            updated.Add(entry);
        }

        if (updated.SequenceEqual(index))
        {
            return;
        }

        try
        {
            await WriteIndexAsync(updated, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WorkspaceException(
                $"Couldn't add the workspace '{workspace.Name}' to the list of workspaces '{IndexFilePath}'. {ex.Message}",
                workspace.Folder,
                ex);
        }
    }

    private async Task WriteIndexAsync(IReadOnlyList<IndexEntry> index, CancellationToken cancellationToken)
    {
        var file = new IndexFile([.. index.Select(entry => new IndexFileEntry(entry.Id, entry.Name, entry.Folder))]);
        await VersionedJsonFile.WriteAsync(IndexFilePath, file, CurrentIndexVersion, cancellationToken)
            .ConfigureAwait(false);
        _index = index;
        _indexProblem = null;
    }

    private IReadOnlyList<IndexEntry> LoadIndex()
    {
        lock (_loadLock)
        {
            if (_index is not null)
            {
                return _index;
            }

            IReadOnlyList<IndexEntry> index;
            try
            {
                index = ReadIndex();
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException
                or UnauthorizedAccessException)
            {
                _indexProblem = $"Couldn't read the list of workspaces '{IndexFilePath}', so no workspaces are listed. "
                    + ex.Message;
                _logger.LogWarning(ex, "{Problem}", _indexProblem);
                KeepInvalidIndex();
                index = [];
            }

            _index = index;
            return index;
        }
    }

    private IReadOnlyList<IndexEntry> ReadIndex()
    {
        if (!File.Exists(IndexFilePath))
        {
            return [];
        }

        var file = VersionedJsonFile.Deserialize<IndexFile>(File.ReadAllText(IndexFilePath), _indexMigrations);
        var index = new List<IndexEntry>();
        foreach (var entry in file.Workspaces ?? [])
        {
            // Skip entries that can't be used, e.g. from a hand-edited file, rather than losing the whole index.
            if (entry is not { Id: { } id, Folder: { } folder }
                || !IsValidId(id)
                || !Path.IsPathFullyQualified(folder)
                || index.Exists(known => known.Id == id))
            {
                continue;
            }

            string normalized;
            try
            {
                normalized = NormalizeFolder(folder);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(entry.Name) ? Path.GetFileName(normalized) : entry.Name;
            index.Add(new IndexEntry(id, name, normalized));
        }

        return index;
    }

    private void KeepInvalidIndex()
    {
        try
        {
            File.Copy(IndexFilePath, Path.Combine(_dataDirectory, InvalidIndexFileName), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The file couldn't be read, so it probably can't be copied either. Nothing more can be done.
        }
    }

    private static string NormalizeFolder(string folder) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

    private static Workspace Normalize(Workspace workspace) => workspace with
    {
        Name = workspace.Name?.Trim()!,
        // A relative folder is left for Validate to report, rather than taken relative to the current folder.
        Folder = Path.IsPathFullyQualified(workspace.Folder ?? string.Empty)
            ? NormalizeFolder(workspace.Folder!)
            : workspace.Folder!,
    };

    /// <summary>
    /// Returns why a workspace isn't valid, or <see langword="null"/> if it is.
    /// </summary>
    private static string? Validate(Workspace workspace)
    {
        if (workspace.Id is null || !IsValidId(workspace.Id))
        {
            return $"The workspace ID '{workspace.Id}' isn't valid. It must be letters, digits, '-' and '_'.";
        }

        if (string.IsNullOrWhiteSpace(workspace.Name))
        {
            return "The workspace has no name.";
        }

        if (string.IsNullOrWhiteSpace(workspace.Folder) || !Path.IsPathFullyQualified(workspace.Folder))
        {
            return $"The workspace folder '{workspace.Folder}' isn't a full path.";
        }

        if (workspace.Tasks is null || workspace.Repositories is null)
        {
            return "The workspace's tasks or repositories are missing.";
        }

        var tasks = new HashSet<TaskReference>();
        foreach (var task in workspace.Tasks)
        {
            if (task is null || string.IsNullOrWhiteSpace(task.Source) || string.IsNullOrWhiteSpace(task.Key))
            {
                return "A task has no source or key.";
            }

            if (!tasks.Add(task))
            {
                return $"The task '{task}' is in the workspace twice.";
            }
        }

        var folderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Workspace.MetadataFolderName };
        foreach (var repository in workspace.Repositories)
        {
            if (repository is null)
            {
                return "A repository is missing.";
            }

            if (!IsValidFolderName(repository.FolderName))
            {
                return $"The repository folder name '{repository.FolderName}' isn't valid. It must be a single folder "
                    + "name, not a path.";
            }

            if (!folderNames.Add(repository.FolderName))
            {
                return $"The repository folder name '{repository.FolderName}' is used twice, or is reserved.";
            }

            if (string.IsNullOrWhiteSpace(repository.LocalRepositoryPath)
                || !Path.IsPathFullyQualified(repository.LocalRepositoryPath))
            {
                return $"The local repository path '{repository.LocalRepositoryPath}' of '{repository.FolderName}' "
                    + "isn't a full path.";
            }

            if (string.IsNullOrWhiteSpace(repository.Branch) || string.IsNullOrWhiteSpace(repository.BaseBranch))
            {
                return $"The repository '{repository.FolderName}' has no branch or base branch.";
            }
        }

        return null;
    }

    private static bool IsValidId(string id) =>
        id.Length > 0 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    /// <summary>
    /// Whether a repository folder name is a single folder name that's valid on every platform. A name that's a path
    /// (e.g. <c>../other</c>) would put the worktree, and anything done to it, outside the Workspace Folder.
    /// </summary>
    private static bool IsValidFolderName(string? name) => FolderNames.IsValid(name);

    private static MetadataFile ToFile(Workspace workspace) => new(
        workspace.Id,
        workspace.Name,
        [.. workspace.Tasks.Select(task => new MetadataTask(task.Source, task.Key))],
        [.. workspace.Repositories.Select(repository => new MetadataRepository(
            repository.FolderName,
            repository.LocalRepositoryPath,
            repository.Branch,
            repository.BaseBranch))]);

    /// <summary>
    /// Builds a workspace from its metadata. Missing values are left empty or <see langword="null"/>, for
    /// <see cref="Validate"/> to report.
    /// </summary>
    private static Workspace FromFile(MetadataFile file, string folder) => new()
    {
        Id = file.Id ?? string.Empty,
        Name = file.Name ?? string.Empty,
        Folder = folder,
        Tasks = [.. (file.Tasks ?? []).Select(task => task is null ? null! : new TaskReference(task.Source!, task.Key!))],
        Repositories =
        [
            .. (file.Repositories ?? []).Select(repository => repository is null
                ? null!
                : new WorkspaceRepository(
                    repository.Folder!,
                    repository.LocalRepository!,
                    repository.Branch!,
                    repository.BaseBranch!)),
        ],
    };

    private sealed record IndexEntry(string Id, string Name, string Folder);

    private sealed record IndexFile(List<IndexFileEntry?>? Workspaces);

    private sealed record IndexFileEntry(string? Id, string? Name, string? Folder);

    private sealed record MetadataFile(
        string? Id,
        string? Name,
        List<MetadataTask?>? Tasks,
        List<MetadataRepository?>? Repositories);

    private sealed record MetadataTask(string? Source, string? Key);

    private sealed record MetadataRepository(string? Folder, string? LocalRepository, string? Branch, string? BaseBranch);
}
