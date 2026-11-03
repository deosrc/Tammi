using System.Text.Json;
using TAMMi.Core.Platform;

namespace TAMMi.Core.Windowing;

/// <summary>
/// Keeps the main window's <see cref="WindowState"/> in <c>window-state.json</c> in the app's data folder
/// (<see cref="IAppPaths.DataDirectory"/>).
/// </summary>
/// <remarks>
/// The window state isn't a setting, so it's kept apart from the settings file. Losing it does no harm: the window
/// then opens at its default size.
/// </remarks>
public sealed class WindowStateStore
{
    /// <summary>
    /// The file's name.
    /// </summary>
    public const string FileName = "window-state.json";

    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _directory;

    /// <summary>
    /// Creates a store for the file in the app's data folder.
    /// </summary>
    public WindowStateStore(IAppPaths appPaths)
        : this(appPaths.DataDirectory)
    {
    }

    /// <summary>
    /// Creates a store for the file in <paramref name="directory"/>.
    /// </summary>
    internal WindowStateStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <summary>
    /// The file's full path.
    /// </summary>
    public string FilePath => Path.Combine(_directory, FileName);

    /// <summary>
    /// Reads the kept window state.
    /// </summary>
    /// <returns>
    /// The window state, or <c>null</c> if none was kept or the file can't be read. The window should then open at
    /// its default size.
    /// </returns>
    public WindowState? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var file = JsonSerializer.Deserialize<WindowStateFile>(File.ReadAllText(FilePath), SerializerOptions);
            return file is { Version: CurrentVersion, Width: > 0, Height: > 0 }
                ? new WindowState(file.Width, file.Height, file.IsMaximized)
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Keeps the window state for next time.
    /// </summary>
    /// <returns>Whether it was saved. Failing to save only means the window opens at its default size.</returns>
    public bool Save(WindowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        try
        {
            Directory.CreateDirectory(_directory);

            // Write a temporary file and move it into place, so a failed write can't leave a half-written file.
            var temporaryPath = FilePath + ".tmp";
            var file = new WindowStateFile(CurrentVersion, state.Width, state.Height, state.IsMaximized);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(file, SerializerOptions));
            File.Move(temporaryPath, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record WindowStateFile(int Version, int Width, int Height, bool IsMaximized);
}
