namespace Deosrc.Tammi.Core.Windowing;

/// <summary>
/// Where and how big a window should open.
/// </summary>
/// <param name="Bounds">The window's position and size when it isn't maximised.</param>
/// <param name="IsMaximized">Whether it should open maximised.</param>
public sealed record WindowPlacement(ScreenRect Bounds, bool IsMaximized);
