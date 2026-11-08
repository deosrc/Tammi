namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Hands out <see cref="IMainAreaTab.StartOrder"/> numbers, so terminals, agents and git graphs can be listed together
/// in the order they started or opened.
/// </summary>
internal static class MainAreaTabOrder
{
    private static long _last;

    /// <summary>
    /// The next number, higher than every one handed out before.
    /// </summary>
    public static long Next() => Interlocked.Increment(ref _last);
}
