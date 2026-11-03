namespace TAMMi.Core.Windowing;

/// <summary>
/// The main window's size and whether it's maximised, kept so the window opens the same way next time.
/// </summary>
/// <param name="Width">The window's width when it isn't maximised, in the host's units.</param>
/// <param name="Height">The window's height when it isn't maximised, in the host's units.</param>
/// <param name="IsMaximized">Whether the window is maximised.</param>
public sealed record WindowState(int Width, int Height, bool IsMaximized);
