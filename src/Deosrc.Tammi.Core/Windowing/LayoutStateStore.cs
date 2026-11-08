using System.Text.Json;
using System.Text.Json.Nodes;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Storage;

namespace Deosrc.Tammi.Core.Windowing;

/// <summary>
/// Keeps the <see cref="LayoutState"/> in <c>layout-state.json</c> in the app's data folder
/// (<see cref="IAppPaths.DataDirectory"/>), beside the window state.
/// </summary>
/// <remarks>
/// It's read once, when it's first needed, and written each time it changes. It's kept apart from the window state,
/// which the host writes as the window closes, so the two never write the same file. Losing it does no harm: the
/// window's parts then have their default sizes.
/// </remarks>
public sealed class LayoutStateStore
{
    /// <summary>
    /// The file's name.
    /// </summary>
    public const string FileName = "layout-state.json";

    private const int CurrentVersion = 1;

    // None yet: the file is at its first version.
    private static readonly IReadOnlyList<Action<JsonObject>> Migrations = [];

    private readonly string _directory;
    private readonly Lazy<LayoutState> _loaded;
    private readonly SemaphoreSlim _writing = new(1, 1);
    private LayoutState? _current;

    /// <summary>
    /// Creates a store for the file in the app's data folder.
    /// </summary>
    public LayoutStateStore(IAppPaths appPaths)
        : this(appPaths.DataDirectory)
    {
    }

    /// <summary>
    /// Creates a store for the file in <paramref name="directory"/>.
    /// </summary>
    internal LayoutStateStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _loaded = new Lazy<LayoutState>(Load);
    }

    /// <summary>
    /// The file's full path.
    /// </summary>
    public string FilePath => Path.Combine(_directory, FileName);

    /// <summary>
    /// The layout state: as last updated, or as kept in the file. The defaults if the file is missing or can't be read.
    /// </summary>
    public LayoutState Current => _current ?? _loaded.Value;

    /// <summary>
    /// Changes the layout state and keeps it for next time.
    /// </summary>
    /// <returns>
    /// Whether it was saved. Failing to save only means the change is lost when the app closes: it still applies
    /// until then.
    /// </returns>
    public async Task<bool> UpdateAsync(Func<LayoutState, LayoutState> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _writing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LayoutState state = update(Current).Normalize();
            _current = state;
            await VersionedJsonFile.WriteAsync(FilePath, state, CurrentVersion, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            _writing.Release();
        }
    }

    private LayoutState Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? VersionedJsonFile.Deserialize<LayoutState>(File.ReadAllText(FilePath), Migrations).Normalize()
                : new LayoutState();
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            return new LayoutState();
        }
    }
}
