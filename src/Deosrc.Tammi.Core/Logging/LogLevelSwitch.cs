using Microsoft.Extensions.Logging;

namespace Deosrc.Tammi.Core.Logging;

/// <summary>
/// The lowest level Tammi logs at. Changing it applies straight away to every logger.
/// </summary>
public sealed class LogLevelSwitch
{
    /// <summary>
    /// The level used until the settings say otherwise.
    /// </summary>
    public const LogLevel DefaultLevel = LogLevel.Information;

    private volatile int _minimumLevel = (int)DefaultLevel;

    /// <summary>
    /// The lowest level that's logged.
    /// </summary>
    public LogLevel MinimumLevel
    {
        get => (LogLevel)_minimumLevel;
        set => _minimumLevel = (int)value;
    }

    /// <summary>
    /// Whether a message at <paramref name="level"/> in <paramref name="category"/> is logged.
    /// </summary>
    /// <remarks>
    /// The framework's own messages (<c>Microsoft.*</c> and <c>System.*</c>) are only logged from Warning up, unless
    /// the level is Trace, so that Debug shows Tammi's messages without the framework's.
    /// </remarks>
    public bool IsEnabled(string? category, LogLevel level)
    {
        var minimumLevel = MinimumLevel;
        if (level == LogLevel.None || level < minimumLevel)
        {
            return false;
        }

        var isFramework = category is not null
            && (category.StartsWith("Microsoft.", StringComparison.Ordinal)
                || category.StartsWith("System.", StringComparison.Ordinal));
        return !isFramework || minimumLevel == LogLevel.Trace || level >= LogLevel.Warning;
    }
}
