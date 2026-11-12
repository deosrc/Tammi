namespace Deosrc.Tammi.Core.Windowing;

/// <summary>
/// How the visualiser's window was left, kept so it opens the same way next time. Like the main window's state, it's
/// not a setting.
/// </summary>
/// <param name="Width">The width of the window when it isn't maximised.</param>
/// <param name="Height">The height of the window when it isn't maximised.</param>
/// <param name="X">The left edge of the window on the screen when it isn't maximised, or <see langword="null"/> if it isn't known.</param>
/// <param name="Y">The top edge of the window on the screen when it isn't maximised, or <see langword="null"/> if it isn't known.</param>
/// <param name="IsMaximized">Whether the window was maximised.</param>
public sealed record VisualiserWindowState(int Width, int Height, int? X, int? Y, bool IsMaximized);
