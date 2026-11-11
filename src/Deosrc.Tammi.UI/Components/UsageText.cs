namespace Deosrc.Tammi.UI.Components;

/// <summary>
/// Text for showing AI usage.
/// </summary>
public static class UsageText
{
    /// <summary>
    /// How long until a limit resets, e.g. "2h 10m", "3d 4h", or "now" if it already has.
    /// </summary>
    public static string TimeUntil(DateTimeOffset resetsAt, DateTimeOffset now)
    {
        var remaining = resetsAt - now;
        return remaining <= TimeSpan.Zero ? "now"
            : remaining.TotalHours >= 24 ? $"{(int)remaining.TotalDays}d {remaining.Hours}h"
            : remaining.TotalHours >= 1 ? $"{(int)remaining.TotalHours}h {remaining.Minutes}m"
            : $"{Math.Max(1, remaining.Minutes)}m";
    }
}
