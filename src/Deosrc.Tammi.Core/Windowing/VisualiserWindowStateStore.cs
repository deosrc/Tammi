using System.Text.Json;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Windowing;

/// <summary>
/// Keeps the <see cref="VisualiserWindowState"/> in <c>visualiser-window-state.json</c> in the app's data folder
/// (<see cref="IAppPaths.DataDirectory"/>), beside the main window's state.
/// </summary>
/// <remarks>
/// Losing it does no harm: the window then opens at its default size, over the main window.
/// </remarks>
public sealed class VisualiserWindowStateStore
{
    /// <summary>
    /// The file's name.
    /// </summary>
    public const string FileName = "visualiser-window-state.json";

    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _directory;

    /// <summary>
    /// Creates a store for the file in the app's data folder.
    /// </summary>
    public VisualiserWindowStateStore(IAppPaths appPaths)
        : this(appPaths.DataDirectory)
    {
    }

    /// <summary>
    /// Creates a store for the file in <paramref name="directory"/>.
    /// </summary>
    internal VisualiserWindowStateStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <summary>
    /// The file's full path.
    /// </summary>
    public string FilePath => Path.Combine(_directory, FileName);

    /// <summary>
    /// Reads the saved state.
    /// </summary>
    /// <returns>The state, or <see langword="null"/> if there's none or it can't be used.</returns>
    public VisualiserWindowState? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var file = JsonSerializer.Deserialize<StateFile>(File.ReadAllText(FilePath), SerializerOptions);
            return file is { Version: CurrentVersion, Width: > 0, Height: > 0 }
                ? new VisualiserWindowState(file.Width, file.Height, file.X, file.Y, file.IsMaximized)
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Keeps the state for next time.
    /// </summary>
    /// <returns>Whether it was saved.</returns>
    public bool Save(VisualiserWindowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        try
        {
            Directory.CreateDirectory(_directory);

            // Write a temporary file and move it into place, so a failed write can't leave a half-written file.
            var temporaryPath = FilePath + ".tmp";
            var file = new StateFile(CurrentVersion, state.Width, state.Height, state.X, state.Y, state.IsMaximized);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(file, SerializerOptions));
            File.Move(temporaryPath, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record StateFile(int Version, int Width, int Height, int? X, int? Y, bool IsMaximized);
}
