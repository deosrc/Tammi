namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// A terminal's size, in character cells.
/// </summary>
public readonly record struct PtySize
{
    /// <summary>
    /// Creates a size. Values below 1 are raised to 1.
    /// </summary>
    public PtySize(int columns, int rows)
    {
        Columns = Math.Clamp(columns, 1, short.MaxValue);
        Rows = Math.Clamp(rows, 1, short.MaxValue);
    }

    /// <summary>
    /// The classic 80 × 24 terminal.
    /// </summary>
    public static PtySize Default { get; } = new(80, 24);

    /// <summary>
    /// The number of columns.
    /// </summary>
    public int Columns { get; }

    /// <summary>
    /// The number of rows.
    /// </summary>
    public int Rows { get; }
}
