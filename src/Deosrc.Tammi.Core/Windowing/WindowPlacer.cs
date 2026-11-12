namespace Deosrc.Tammi.Core.Windowing;

/// <summary>
/// Works out where a window that was saved should open, which may not be where it was: the screens can have changed
/// since.
/// </summary>
public static class WindowPlacer
{
    /// <summary>
    /// Returns where the window should open.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>It opens where it was, at the size it was.</item>
    /// <item>
    /// If its top left corner isn't on any screen, or it has no saved position, it's centred over the main window.
    /// </item>
    /// <item>
    /// If it then goes beyond the edge of the screen it's on, it goes back to its default size, kept where it was if it
    /// fits there and otherwise centred over the main window.
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="saved">The saved state, or <see langword="null"/> if there's none.</param>
    /// <param name="defaultWidth">The width it opens at when there's no usable saved size.</param>
    /// <param name="defaultHeight">The height it opens at when there's no usable saved size.</param>
    /// <param name="screens">The usable area of each screen.</param>
    /// <param name="mainWindow">The main window's bounds.</param>
    public static WindowPlacement Place(
        VisualiserWindowState? saved,
        int defaultWidth,
        int defaultHeight,
        IReadOnlyList<ScreenRect> screens,
        ScreenRect mainWindow)
    {
        ArgumentNullException.ThrowIfNull(screens);

        int width = saved?.Width ?? defaultWidth;
        int height = saved?.Height ?? defaultHeight;
        bool maximized = saved?.IsMaximized ?? false;

        // Where it was, if that's on a screen.
        ScreenRect? savedScreen = saved is { X: { } x, Y: { } y } ? ScreenAt(screens, x, y) : null;
        bool keptPosition = savedScreen is not null;
        ScreenRect screen = savedScreen ?? ScreenAt(screens, mainWindow.X + (mainWindow.Width / 2), mainWindow.Y + (mainWindow.Height / 2))
            ?? (screens.Count > 0 ? screens[0] : mainWindow);
        ScreenRect bounds = keptPosition
            ? new ScreenRect(saved!.X!.Value, saved.Y!.Value, width, height)
            : CenteredOver(mainWindow, width, height);

        if (!screen.Contains(bounds))
        {
            // Too big for where it would be, so it's the default size, still where it was if that fits.
            ScreenRect reset = keptPosition
                ? bounds with { Width = defaultWidth, Height = defaultHeight }
                : CenteredOver(mainWindow, defaultWidth, defaultHeight);
            if (keptPosition && !screen.Contains(reset))
            {
                reset = CenteredOver(mainWindow, defaultWidth, defaultHeight);
            }

            bounds = reset;
        }

        return new WindowPlacement(Clamp(bounds, screen), maximized);
    }

    private static ScreenRect? ScreenAt(IReadOnlyList<ScreenRect> screens, int x, int y)
    {
        foreach (ScreenRect screen in screens)
        {
            if (screen.Contains(x, y))
            {
                return screen;
            }
        }

        return null;
    }

    private static ScreenRect CenteredOver(ScreenRect outer, int width, int height) => new(
        outer.X + ((outer.Width - width) / 2),
        outer.Y + ((outer.Height - height) / 2),
        width,
        height);

    // Last resort, e.g. a screen smaller than the default size: moved so as much as possible is on the screen.
    private static ScreenRect Clamp(ScreenRect bounds, ScreenRect screen) => bounds with
    {
        X = Math.Max(screen.X, Math.Min(bounds.X, screen.Right - bounds.Width)),
        Y = Math.Max(screen.Y, Math.Min(bounds.Y, screen.Bottom - bounds.Height)),
    };
}
