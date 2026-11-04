namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// Loads and saves the <see cref="AppSettings"/>.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// The current settings. They're loaded from the settings file the first time they're read.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Why the settings file couldn't be read, or <c>null</c> if it was read (or doesn't exist yet). When it couldn't
    /// be read, <see cref="Current"/> holds the defaults.
    /// </summary>
    string? LoadProblem { get; }

    /// <summary>
    /// Raised after the settings change. It can be raised on any thread.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// Changes the settings and saves them to the settings file.
    /// </summary>
    /// <param name="update">Returns the new settings, given the current ones.</param>
    /// <param name="cancellationToken">Cancels the update before the file is written.</param>
    /// <returns>The saved settings.</returns>
    Task<AppSettings> UpdateAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default);
}
