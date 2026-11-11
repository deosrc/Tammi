using Photino.Blazor;
using Photino.NET;
using Deosrc.Tammi.UI.Visualiser;

namespace Deosrc.Tammi.Linux.PlatformServices;

/// <summary>
/// Shows the visualiser in a second Photino window, a child of the main window. The page has no Blazor in it: it's
/// shown from a string, and sent each snapshot as a web message.
/// </summary>
internal sealed class PhotinoVisualiserWindow(PhotinoBlazorApp app, VisualiserFeed feed) : IVisualiserWindow
{
    private PhotinoWindow? _window;

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

        var window = new PhotinoWindow(app.MainWindow)
            .SetLogVerbosity(0)
            .SetTitle("Visualiser")
            .SetMinSize(400, 300)
            .SetMaxSize(100_000, 100_000)
            .SetSize(1800, 1400)
            .Center()
            .LoadRawString(VisualiserPage.Html);
        _window = window;

        void Push() => app.MainWindow.Invoke(async () => window.SendWebMessage(await feed.SnapshotAsync()));
        feed.Changed += Push;
        window.RegisterWindowClosingHandler((_, _) =>
        {
            feed.Changed -= Push;
            _window = null;
            return false;
        });

        // The page's only message says it has loaded, so the first snapshot goes then.
        window.RegisterWebMessageReceivedHandler((_, _) => Push());
        window.WaitForClose();
    });
}
