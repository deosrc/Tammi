using System.Text.Json;
using System.Text.Json.Nodes;
using TAMMi.Core.Platform;

namespace TAMMi.Core.Settings;

/// <summary>
/// Keeps the <see cref="AppSettings"/> in <c>settings.json</c> in the app's config folder
/// (<see cref="IAppPaths.ConfigDirectory"/>).
/// </summary>
/// <remarks>
/// <para>
/// The file has a <c>version</c> property. When the file's layout changes, the version goes up and a migration is
/// added to <see cref="Migrations"/>, which upgrades files written by older versions of TAMMi when they're loaded.
/// A file from a newer version of TAMMi is read as far as possible.
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

    private const string VersionProperty = "version";

    /// <summary>
    /// The settings file migrations. The migration at index <c>i</c> upgrades a file from version <c>i + 1</c> to
    /// version <c>i + 2</c>, so the current version is one more than the number of migrations.
    /// </summary>
    private static readonly IReadOnlyList<Action<JsonObject>> Migrations = [];

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

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
    private AppSettings? Read()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        var node = JsonNode.Parse(File.ReadAllText(FilePath), documentOptions: new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });
        if (node is not JsonObject json)
        {
            throw new InvalidDataException("The file doesn't hold a JSON object.");
        }

        var version = ReadVersion(json);
        for (var i = version - 1; i < _migrations.Count; i++)
        {
            _migrations[i](json);
        }

        json.Remove(VersionProperty);
        return json.Deserialize<AppSettings>(SerializerOptions)
            ?? throw new InvalidDataException("The file doesn't hold any settings.");
    }

    private static int ReadVersion(JsonObject json)
    {
        // TAMMi always writes the version, but a file written by hand might not have one.
        if (!json.TryGetPropertyValue(VersionProperty, out var versionNode) || versionNode is null)
        {
            return 1;
        }

        if (versionNode is not JsonValue value || !value.TryGetValue(out int version) || version < 1)
        {
            throw new InvalidDataException($"The file's version '{versionNode.ToJsonString()}' isn't valid.");
        }

        return version;
    }

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

    private async Task WriteAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToNode(settings, SerializerOptions)!.AsObject();
        json.Insert(0, VersionProperty, CurrentVersion);

        Directory.CreateDirectory(_directory);

        // Write a temporary file and move it into place, so a failed write can't leave a half-written file.
        var temporaryPath = FilePath + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, json.ToJsonString(SerializerOptions), cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporaryPath, FilePath, overwrite: true);
    }
}
