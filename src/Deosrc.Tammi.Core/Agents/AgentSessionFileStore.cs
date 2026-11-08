using System.Text.Json;
using System.Text.Json.Nodes;
using Deosrc.Tammi.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// Keeps Agent Sessions in a versioned JSON file, such as <c>.tammi/agent-sessions.json</c> in a Workspace Folder.
/// </summary>
/// <remarks>
/// The file is versioned like the settings file (see <see cref="VersionedJsonFile"/>). It's read each time the
/// sessions are listed, so changes made by another Tammi window are seen. If it can't be read, no sessions are listed,
/// and the next change first copies it to a file named like it with <c>.invalid</c> before the extension (e.g.
/// <c>agent-sessions.invalid.json</c>), so it isn't lost.
/// </remarks>
public sealed class AgentSessionFileStore : IAgentSessionStore
{
    /// <summary>
    /// The file's usual name.
    /// </summary>
    public const string DefaultFileName = "agent-sessions.json";

    /// <summary>
    /// The file migrations (see <see cref="VersionedJsonFile"/>).
    /// </summary>
    private static readonly IReadOnlyList<Action<JsonObject>> Migrations = [];

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger _logger;
    private volatile string? _problem;

    /// <summary>
    /// Creates a store for the file at <paramref name="filePath"/>. The file and its folder are created when a session
    /// is first recorded.
    /// </summary>
    public AgentSessionFileStore(string filePath, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// The file's full path.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Where a copy of a file that couldn't be read is kept.
    /// </summary>
    public string InvalidFilePath => Path.Combine(
        Path.GetDirectoryName(FilePath)!,
        Path.GetFileNameWithoutExtension(FilePath) + ".invalid" + Path.GetExtension(FilePath));

    /// <summary>
    /// The version of the file this store writes.
    /// </summary>
    public static int CurrentVersion => Migrations.Count + 1;

    /// <inheritdoc />
    public string? Problem => _problem;

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentSession>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Sorted((await ReadAsync(cancellationToken).ConfigureAwait(false)).Sessions);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public Task RecordAsync(AgentSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.Id, nameof(session));

        return UpdateAsync(
            sessions => [session, .. sessions.Where(recorded => recorded.Id != session.Id)],
            cancellationToken);
    }

    /// <inheritdoc />
    public Task ForgetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return UpdateAsync(sessions => [.. sessions.Where(recorded => recorded.Id != id)], cancellationToken);
    }

    private async Task UpdateAsync(
        Func<IReadOnlyList<AgentSession>, IReadOnlyList<AgentSession>> update,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (sessions, readable) = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var updated = update(sessions);
            if (updated.Count == sessions.Count && updated.SequenceEqual(sessions))
            {
                return;
            }

            if (!readable)
            {
                File.Copy(FilePath, InvalidFilePath, overwrite: true);
            }

            await VersionedJsonFile.WriteAsync(
                FilePath,
                new SessionsFile(Sorted(updated)),
                CurrentVersion,
                cancellationToken).ConfigureAwait(false);
            _problem = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Returns the sessions, and whether the file could be read. A missing file holds no sessions.
    private async Task<(IReadOnlyList<AgentSession> Sessions, bool Readable)> ReadAsync(
        CancellationToken cancellationToken)
    {
        string contents;
        try
        {
            contents = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            _problem = null;
            return ([], true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A change copies the file first, so it isn't replaced unless that copy works.
            _problem = $"Couldn't read the agent sessions in '{FilePath}', so none are listed. {ex.Message}";
            _logger.LogWarning("{Problem}", _problem);
            return ([], false);
        }

        try
        {
            var file = VersionedJsonFile.Deserialize<SessionsFile>(contents, Migrations);
            _problem = null;
            return ([.. (file.Sessions ?? []).Where(IsComplete)], true);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            _problem = $"Couldn't read the agent sessions in '{FilePath}', so none are listed. {ex.Message}";
            _logger.LogWarning("{Problem}", _problem);
            return ([], false);
        }
    }

    // A session missing a value (e.g. in a file written by hand) can't be resumed.
    private static bool IsComplete(AgentSession? session) =>
        session is not null
        && !string.IsNullOrWhiteSpace(session.Id)
        && session.AgentName is not null
        && !string.IsNullOrWhiteSpace(session.WorkingDirectory);

    private static AgentSession[] Sorted(IEnumerable<AgentSession> sessions) =>
        [.. sessions.OrderByDescending(session => session.LastStartedAt)];

    private sealed record SessionsFile(IReadOnlyList<AgentSession>? Sessions);
}
