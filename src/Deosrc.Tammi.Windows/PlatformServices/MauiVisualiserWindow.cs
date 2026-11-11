using Deosrc.Tammi.UI.Visualiser;

namespace Deosrc.Tammi.Windows.PlatformServices;

/// <summary>
/// Shows the visualiser in a second MAUI window holding a plain web view. The page has no Blazor in it: it's shown
/// from a string, and sent each snapshot by calling a script function.
/// </summary>
internal sealed class MauiVisualiserWindow(VisualiserFeed feed) : IVisualiserWindow
{
    private Window? _window;

    /// <inheritdoc />
    public void OpenOrFocus() => MainThread.BeginInvokeOnMainThread(() =>
    {
        var application = Application.Current!;
        if (_window is { } open)
        {
            application.ActivateWindow(open);
            return;
        }

        var webView = new WebView { Source = new HtmlWebViewSource { Html = VisualiserPage.Html } };
        var window = new Window(new ContentPage { Content = webView, Padding = 0 })
        {
            Title = "Visualiser",
            Width = 1800,
            Height = 1400,
        };
        _window = window;

        async void Push()
        {
            var json = await feed.SnapshotAsync();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // A string literal for the script: the JSON is escaped as one.
                var literal = System.Text.Json.JsonSerializer.Serialize(json);
                await webView.EvaluateJavaScriptAsync($"window.visualiserUpdate({literal})");
            });
        }

        feed.Changed += Push;
        webView.Navigated += (_, _) => Push();
        window.Destroying += (_, _) =>
        {
            feed.Changed -= Push;
            _window = null;
        };
        application.OpenWindow(window);
    });
}
