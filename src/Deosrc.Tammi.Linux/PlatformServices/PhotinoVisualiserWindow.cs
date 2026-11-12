using System.Drawing;
using System.Text.Json;
using Photino.Blazor;
using Photino.NET;
using Deosrc.Tammi.Core.Windowing;
using Deosrc.Tammi.UI.Visualiser;

namespace Deosrc.Tammi.Linux.PlatformServices;

/// <summary>
/// Shows the visualiser in a second Photino window, a child of the main window. The page has no Blazor in it: it's
/// shown from a string, and sent each snapshot as a web message.
/// </summary>
internal sealed class PhotinoVisualiserWindow(
    PhotinoBlazorApp app,
    VisualiserFeed feed,
    WorkspaceOpenRequests openRequests,
    VisualiserWindowStateStore stateStore) : IVisualiserWindow
{
    // The smallest the window can be, which is also the size it opens at until it's been resized.
    private const int MinWidth = 400;
    private const int MinHeight = 300;
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private PhotinoWindow? _window;

    private WindowStateTracker? _tracker;
    private Point _location;
    private Timer? _saveTimer;

    // Keeps the window's size, position and whether it's maximised, as they change, so they're there next time. The
    // position of a maximised window is where it was before, as for its size. Writes wait until changes have stopped.
    private void TrackState(PhotinoWindow window, WindowPlacement placement)
    {
        _tracker = new WindowStateTracker(new WindowState(
            placement.Bounds.Width, placement.Bounds.Height, placement.IsMaximized));
        _location = new Point(placement.Bounds.X, placement.Bounds.Y);
        _saveTimer = new Timer(_ => SaveState(window));

        void Observe()
        {
            var mode = window.Maximized ? WindowMode.Maximized
                : window.Minimized ? WindowMode.Minimized
                : WindowMode.Normal;
            _tracker.Observe(window.Width, window.Height, mode);
            if (mode == WindowMode.Normal)
            {
                _location = new Point(window.Left, window.Top);
            }

            _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
        }

        window
            .RegisterSizeChangedHandler((_, _) => Observe())
            .RegisterLocationChangedHandler((_, _) => Observe())
            .RegisterMaximizedHandler((_, _) => Observe())
            .RegisterRestoredHandler((_, _) => Observe())
            .RegisterMinimizedHandler((_, _) => Observe());
    }

    private void SaveState(PhotinoWindow window)
    {
        if (_tracker is { Current: var state })
        {
            stateStore.Save(new VisualiserWindowState(state.Width, state.Height, _location.X, _location.Y, state.IsMaximized));
        }
    }

    private static string? TryGetWorkspaceToOpen(string message)
    {
        try
        {
            using var json = JsonDocument.Parse(message);
            return json.RootElement.TryGetProperty("open", out var id) ? id.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void ShowMainWindow() => app.MainWindow.Invoke(() =>
    {
        app.MainWindow.SetMinimized(false);
        app.MainWindow.SetTopMost(true);
        app.MainWindow.SetTopMost(false);
    });

    /// <inheritdoc />
    public void OpenOrFocus() => app.MainWindow.Invoke(() =>
    {
        if (_window is { } open)
        {
            open.SetMinimized(false);
            open.SetTopMost(true);
            open.SetTopMost(false);
            return;
        }

        // Where it was left, unless that's no longer on a screen or fits on it.
        var main = app.MainWindow;
        var placement = WindowPlacer.Place(
            stateStore.Load(),
            MinWidth,
            MinHeight,
            [.. main.Monitors.Select(monitor => new ScreenRect(
                monitor.WorkArea.X, monitor.WorkArea.Y, monitor.WorkArea.Width, monitor.WorkArea.Height))],
            new ScreenRect(main.Left, main.Top, main.Width, main.Height));

        var window = new PhotinoWindow(main)
            .SetLogVerbosity(0)
            .SetTitle("Visualiser")
            .SetMinSize(MinWidth, MinHeight)
            .SetMaxSize(100_000, 100_000)
            .SetUseOsDefaultSize(false)
            .SetSize(placement.Bounds.Width, placement.Bounds.Height)
            .SetUseOsDefaultLocation(false)
            .SetLocation(new Point(placement.Bounds.X, placement.Bounds.Y))
            .SetMaximized(placement.IsMaximized)
            .LoadRawString(VisualiserPage.Html);
        _window = window;
        TrackState(window, placement);

        // As when Home is opened, the statuses are read again.
        _ = feed.RefreshStatusAsync();

        void Push() => app.MainWindow.Invoke(async () => window.SendWebMessage(await feed.SnapshotAsync()));
        feed.Changed += Push;
        window.RegisterWindowClosingHandler((_, _) =>
        {
            feed.Changed -= Push;
            _saveTimer?.Dispose();
            _saveTimer = null;
            SaveState(window);
            _window = null;
            return false;
        });

        // The page says when it has loaded, so the first snapshot goes then, and which workspace to open when one is
        // clicked.
        window.RegisterWebMessageReceivedHandler((_, message) =>
        {
            if (message == "ready")
            {
                Push();
            }
            else if (TryGetWorkspaceToOpen(message) is { } workspaceId)
            {
                openRequests.Request(workspaceId);
                ShowMainWindow();
            }
        });
        window.WaitForClose();
    });
}
