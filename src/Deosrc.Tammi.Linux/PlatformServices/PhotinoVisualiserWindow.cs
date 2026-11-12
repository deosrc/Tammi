using System.Text.Json;
using Photino.Blazor;
using Photino.NET;
using Deosrc.Tammi.UI.Visualiser;

namespace Deosrc.Tammi.Linux.PlatformServices;

/// <summary>
/// Shows the visualiser in a second Photino window, a child of the main window. The page has no Blazor in it: it's
/// shown from a string, and sent each snapshot as a web message.
/// </summary>
internal sealed class PhotinoVisualiserWindow(
    PhotinoBlazorApp app,
    VisualiserFeed feed,
    WorkspaceOpenRequests openRequests) : IVisualiserWindow
{
    private PhotinoWindow? _window;

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
