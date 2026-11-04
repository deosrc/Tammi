using System.Text.Json;
using System.Text.Json.Nodes;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Storage;

namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// Keeps the <see cref="AppSettings"/> in <c>settings.json</c> in the app's config folder
/// (<see cref="IAppPaths.ConfigDirectory"/>).
/// </summary>
/// <remarks>
/// <para>
/// The file has a <c>version</c> property. When the file's layout changes, the version goes up and a migration is
/// added to <see cref="Migrations"/>, which upgrades files written by older versions of Tammi when they're loaded.
/// A file from a newer version of Tammi is read as far as possible.
/// </para>
/// <para>
/// If the file can't be read, the defaults are used and the file is copied to <c>settings.invalid.json</c>, so the
/// next save doesn't lose it.
/// </para>
/// </remarks>
public sealed class SettingsStore : ISettingsStore
{
    /// <summary>
    /// The settings file's name.
    /// </summary>
    public const string FileName = "settings.json";

    /// <summary>
    /// The name of the copy kept of a settings file that couldn't be read.
    /// </summary>
    public const string InvalidFileName = "settings.invalid.json";

    /// <summary>
    /// The settings file migrations. The migration at index <c>i</c> upgrades a file from version <c>i + 1</c> to
    /// version <c>i + 2</c>, so the current version is one more than the number of migrations.
    /// </summary>
    private static readonly IReadOnlyList<Action<JsonObject>> Migrations = [];

    private readonly string _directory;
    private readonly IReadOnlyList<Action<JsonObject>> _migrations;
    private readonly Lock _loadLock = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private volatile AppSettings? _current;
    private string? _loadProblem;

    /// <summary>
    /// Creates a store for the settings file in the app's config folder.
    /// </summary>
    public SettingsStore(IAppPaths appPaths)
        : this(appPaths.ConfigDirectory, Migrations)
    {
    }

    /// <summary>
    /// Creates a store for the settings file in <paramref name="directory"/>, with the given migrations.
    /// </summary>
    internal SettingsStore(string directory, IReadOnlyList<Action<JsonObject>> migrations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _migrations = migrations;
    }

    /// <summary>
    /// The version of the settings file this store writes.
    /// </summary>
    public int CurrentVersion => _migrations.Count + 1;

    /// <summary>
    /// The settings file's full path.
    /// </summary>
    public string FilePath => Path.Combine(_directory, FileName);

    /// <inheritdoc />
    public AppSettings Current => _current ?? Load();

    /// <inheritdoc />
    public string? LoadProblem
    {
        get
        {
            _ = Current;
            return _loadProblem;
        }
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public async Task<AppSettings> UpdateAsync(
        Func<AppSettings, AppSettings> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        AppSettings settings;
        try
        {
            settings = update(Current).Normalize();
            await WriteAsync(settings, cancellationToken).ConfigureAwait(false);
            _current = settings;
            _loadProblem = null;
        }
        finally
        {
            _saveLock.Release();
        }

        Changed?.Invoke();
        return settings;
    }

    private AppSettings Load()
    {
        lock (_loadLock)
        {
            if (_current is not null)
            {
                return _current;
            }

            AppSettings settings;
            try
            {
                settings = Read() ?? new AppSettings();
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException
                or UnauthorizedAccessException)
            {
                _loadProblem = $"Couldn't read the settings file '{FilePath}', so the default settings are used. "
                    + $"{ex.Message}";
                KeepInvalidFile();
                settings = new AppSettings();
            }

            _current = settings.Normalize();
            return _current;
        }
    }

    /// <summary>
    /// Reads the settings file, or returns <c>null</c> if there isn't one.
    /// </summary>
    private AppSettings? Read() =>
        File.Exists(FilePath)
            ? VersionedJsonFile.Deserialize<AppSettings>(File.ReadAllText(FilePath), _migrations)
            : null;

    private void KeepInvalidFile()
    {
        try
        {
            File.Copy(FilePath, Path.Combine(_directory, InvalidFileName), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The file couldn't be read, so it probably can't be copied either. Nothing more can be done.
        }
    }

    private Task WriteAsync(AppSettings settings, CancellationToken cancellationToken) =>
        VersionedJsonFile.WriteAsync(FilePath, settings, CurrentVersion, cancellationToken);
}
