using Microsoft.UI.Windowing;
using Deosrc.Tammi.Core.Windowing;
using Windows.Graphics;

namespace Deosrc.Tammi.Windows.PlatformServices;

/// <summary>
/// Opens the visualiser's window where and how big it was when it was last closed, and keeps them as they change.
/// </summary>
/// <remarks>
/// Sizes and positions are the <see cref="AppWindow"/>'s, in physical pixels, as for the main window (see
/// <see cref="WindowStatePersistence"/>). Where the window goes, and whether it's kept there, is decided by
/// <see cref="WindowPlacer"/>.
/// </remarks>
internal static class VisualiserWindowStatePersistence
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Restores the window's state and follows its changes. Call it when the window's native window has been created,
    /// before it's shown.
    /// </summary>
    /// <param name="window">The visualiser's native window.</param>
    /// <param name="mainWindow">The main window, which the visualiser is centred over if it has to be moved.</param>
    public static void Attach(Microsoft.UI.Xaml.Window window, Microsoft.UI.Xaml.Window? mainWindow, VisualiserWindowStateStore store)
    {
        var appWindow = window.AppWindow;

        // It's been created at its default size, in physical pixels.
        var defaultSize = appWindow.Size;
        ScreenRect[] screens =
        [
            .. DisplayArea.FindAll().Select(area => new ScreenRect(
                area.WorkArea.X, area.WorkArea.Y, area.WorkArea.Width, area.WorkArea.Height)),
        ];
        var main = mainWindow?.AppWindow;
        var mainBounds = main is null
            ? (screens.Length > 0 ? screens[0] : new ScreenRect(0, 0, defaultSize.Width, defaultSize.Height))
            : new ScreenRect(main.Position.X, main.Position.Y, main.Size.Width, main.Size.Height);

        var placement = WindowPlacer.Place(store.Load(), defaultSize.Width, defaultSize.Height, screens, mainBounds);
        appWindow.MoveAndResize(new RectInt32(
            placement.Bounds.X, placement.Bounds.Y, placement.Bounds.Width, placement.Bounds.Height));
        if (placement.IsMaximized && appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        // The size and whether it's maximised are tracked as for the main window. The position of a maximised window
        // is where it was before, as for its size.
        var tracker = new WindowStateTracker(
            new WindowState(placement.Bounds.Width, placement.Bounds.Height, placement.IsMaximized));
        var location = new PointInt32(placement.Bounds.X, placement.Bounds.Y);

        void Save() => store.Save(new VisualiserWindowState(
            tracker.Current.Width, tracker.Current.Height, location.X, location.Y, tracker.Current.IsMaximized));

        // Writes wait until changes have stopped.
        var saveTimer = new System.Threading.Timer(_ => Save());
        appWindow.Changed += (_, args) =>
        {
            if (!args.DidSizeChange && !args.DidPositionChange && !args.DidPresenterChange)
            {
                return;
            }

            var mode = WindowStatePersistence.GetMode(appWindow);
            tracker.Observe(appWindow.Size.Width, appWindow.Size.Height, mode);
            if (mode == WindowMode.Normal)
            {
                location = appWindow.Position;
            }

            saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
        };
        window.Closed += (_, _) =>
        {
            saveTimer.Dispose();
            Save();
        };
    }
}
