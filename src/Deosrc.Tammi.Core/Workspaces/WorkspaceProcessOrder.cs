namespace TAMMi.Core.Workspaces;

/// <summary>
/// Hands out <see cref="IWorkspaceProcess.StartOrder"/> numbers, so terminals and agents can be listed together in the
/// order they started.
/// </summary>
internal static class WorkspaceProcessOrder
{
    private static long _last;

    /// <summary>
    /// The next number, higher than every one handed out before.
    /// </summary>
    public static long Next() => Interlocked.Increment(ref _last);
}
