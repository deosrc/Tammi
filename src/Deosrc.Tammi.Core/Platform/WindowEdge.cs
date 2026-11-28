namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// An edge or corner of a window, for resizing it. The values are GTK's <c>GdkWindowEdge</c>.
/// </summary>
public enum WindowEdge
{
    NorthWest = 0,
    North = 1,
    NorthEast = 2,
    West = 3,
    East = 4,
    SouthWest = 5,
    South = 6,
    SouthEast = 7,
}
