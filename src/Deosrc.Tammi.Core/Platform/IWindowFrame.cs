namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// The main window's frame. When the platform's title bar is removed, the app bar takes its place: it shows the window
/// controls, and its empty space moves the window and toggles maximising it.
/// </summary>
public interface IWindowFrame
{
    /// <summary>
    /// Whether the window has the platform's title bar. When it doesn't, the app bar takes over its controls and
    /// dragging. It follows the Slim Window setting, so it can change while the app runs.
    /// </summary>
    bool HasTitleBar { get; }

    /// <summary>
    /// Whether the edges of the window can't be dragged to resize it, so the page has to offer them. Only applies when
    /// <see cref="HasTitleBar"/> is <c>false</c>.
    /// </summary>
    bool NeedsResizeEdges { get; }

    /// <summary>
    /// Whether the window is maximised.
    /// </summary>
    bool IsMaximized { get; }

    /// <summary>
    /// Raised after <see cref="HasTitleBar"/> or <see cref="IsMaximized"/> changes. It can be raised on any thread.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// Minimises the window.
    /// </summary>
    void Minimize();

    /// <summary>
    /// Maximises the window, or restores it if it is maximised.
    /// </summary>
    void ToggleMaximized();

    /// <summary>
    /// Moves the window with the pointer, as dragging the title bar would, until the button is let go. Call it while the
    /// primary button is held.
    /// </summary>
    /// <param name="screenX">The pointer's position on the screen.</param>
    /// <param name="screenY">The pointer's position on the screen.</param>
    void BeginMove(int screenX, int screenY);

    /// <summary>
    /// Resizes the window from one of its edges or corners with the pointer, until the button is let go. Call it while
    /// the primary button is held.
    /// </summary>
    /// <param name="edge">The edge or corner being dragged.</param>
    /// <param name="screenX">The pointer's position on the screen.</param>
    /// <param name="screenY">The pointer's position on the screen.</param>
    void BeginResize(WindowEdge edge, int screenX, int screenY);
}
