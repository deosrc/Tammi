namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Options for <see cref="IFileSystemWatchService.Watch"/>.
/// </summary>
public sealed record FileSystemWatchOptions
{
    /// <summary>
    /// Whether to watch subfolders too. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeSubdirectories { get; init; } = true;

    /// <summary>
    /// How long the folder must be quiet before a batch of changes is reported. Defaults to 250 ms.
    /// </summary>
    public TimeSpan QuietPeriod { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The longest a change waits before it's reported, even if the folder keeps changing (e.g. during a build).
    /// Defaults to 2 seconds.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(2);
}
