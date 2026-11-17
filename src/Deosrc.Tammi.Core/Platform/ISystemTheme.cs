namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Reports whether the system's theme is dark, for platforms where the web view doesn't follow it by itself.
/// </summary>
public interface ISystemTheme
{
    /// <summary>
    /// Whether the system theme is dark, or <c>null</c> if it can't be detected (the web view's own detection is used
    /// instead).
    /// </summary>
    bool? IsDark { get; }

    /// <summary>
    /// Raised after <see cref="IsDark"/> changes. It can be raised on any thread.
    /// </summary>
    event Action? Changed;
}
