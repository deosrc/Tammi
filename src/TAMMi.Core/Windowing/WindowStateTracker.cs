namespace TAMMi.Core.Windowing;

/// <summary>
/// Follows the main window's changes to work out the <see cref="WindowState"/> to keep.
/// </summary>
/// <remarks>
/// <para>
/// The size kept is the window's size when it isn't maximised, so restoring a maximised window goes back to it.
/// Hosts call <see cref="Observe"/> whenever the window's size or mode changes.
/// </para>
/// <para>
/// Some platforms report a maximised window's new size before they report that it's maximised. When the window is
/// then reported maximised at the size just recorded, the size from before is put back.
/// </para>
/// </remarks>
public sealed class WindowStateTracker(WindowState initial)
{
    private (int Width, int Height)? _previousSize;

    /// <summary>
    /// The state to keep.
    /// </summary>
    public WindowState Current { get; private set; } = initial;

    /// <summary>
    /// Records the window's current size and mode.
    /// </summary>
    public void Observe(int width, int height, WindowMode mode)
    {
        switch (mode)
        {
            case WindowMode.Minimized:
                // A minimised window's size says nothing about the size to restore, and it stays maximised (or not)
                // underneath.
                break;

            case WindowMode.Maximized:
                if (_previousSize is { } previous && Current.Width == width && Current.Height == height)
                {
                    Current = Current with { Width = previous.Width, Height = previous.Height };
                    _previousSize = null;
                }

                Current = Current with { IsMaximized = true };
                break;

            default:
                if (width > 0 && height > 0 && (width != Current.Width || height != Current.Height))
                {
                    _previousSize = (Current.Width, Current.Height);
                    Current = Current with { Width = width, Height = height };
                }

                Current = Current with { IsMaximized = false };
                break;
        }
    }
}
