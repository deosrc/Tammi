namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// An <see cref="IWindowFrame"/> for platforms that keep their own title bar, which does all of it.
/// </summary>
public sealed class TitledWindowFrame : IWindowFrame
{
    /// <inheritdoc />
    public bool HasTitleBar => true;

    /// <inheritdoc />
    public bool NeedsResizeEdges => false;

    /// <inheritdoc />
    public bool IsMaximized => false;

    /// <inheritdoc />
    public event Action? Changed
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public void Minimize()
    {
    }

    /// <inheritdoc />
    public void ToggleMaximized()
    {
    }

    /// <inheritdoc />
    public void BeginMove(int screenX, int screenY)
    {
    }

    /// <inheritdoc />
    public void BeginResize(WindowEdge edge, int screenX, int screenY)
    {
    }
}
