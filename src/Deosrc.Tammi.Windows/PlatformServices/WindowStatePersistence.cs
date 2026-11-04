using Microsoft.UI.Windowing;
using Deosrc.Tammi.Core.Windowing;
using Windows.Graphics;

namespace Deosrc.Tammi.Windows.PlatformServices;

/// <summary>
/// Opens the main window at the size and maximised state it had when it was last closed, and keeps them when it
/// closes.
/// </summary>
/// <remarks>
/// Sizes are the <see cref="AppWindow"/>'s outer size in physical pixels.
/// </remarks>
internal static class WindowStatePersistence
{
    /// <summary>
    /// Restores the window's state and follows its changes. Call it when the window is created, before it's shown.
    /// </summary>
    public static void Attach(Microsoft.UI.Xaml.Window window, WindowStateStore store)
    {
        var appWindow = window.AppWindow;
        var saved = store.Load();
        if (saved is not null)
        {
            Restore(appWindow, saved);
        }

        var tracker = new WindowStateTracker(
            saved ?? new WindowState(appWindow.Size.Width, appWindow.Size.Height, IsMaximized: false));
        appWindow.Changed += (_, args) =>
        {
            if (args.DidSizeChange || args.DidPresenterChange)
            {
                tracker.Observe(appWindow.Size.Width, appWindow.Size.Height, GetMode(appWindow));
            }
        };
        window.Closed += (_, _) => store.Save(tracker.Current);
    }

    private static void Restore(AppWindow appWindow, WindowState state)
    {
        // Centre the window in the work area of its display, and keep it on screen if the display has got smaller.
        var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var width = Math.Min(state.Width, workArea.Width);
        var height = Math.Min(state.Height, workArea.Height);
        appWindow.MoveAndResize(new RectInt32(
            workArea.X + ((workArea.Width - width) / 2),
            workArea.Y + ((workArea.Height - height) / 2),
            width,
            height));

        if (state.IsMaximized && appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    private static WindowMode GetMode(AppWindow appWindow) =>
        (appWindow.Presenter as OverlappedPresenter)?.State switch
        {
            OverlappedPresenterState.Maximized => WindowMode.Maximized,
            OverlappedPresenterState.Minimized => WindowMode.Minimized,
            _ => WindowMode.Normal,
        };
}
