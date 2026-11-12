namespace Deosrc.Tammi.Core.Windowing;

/// <summary>
/// A rectangle in screen coordinates, e.g. a monitor's usable area or a window's bounds.
/// </summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// The edge past the rectangle's right side.
    /// </summary>
    public int Right => X + Width;

    /// <summary>
    /// The edge past the rectangle's bottom.
    /// </summary>
    public int Bottom => Y + Height;

    /// <summary>
    /// Whether a point is inside the rectangle.
    /// </summary>
    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    /// <summary>
    /// Whether another rectangle is entirely inside this one.
    /// </summary>
    public bool Contains(ScreenRect other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;
}
