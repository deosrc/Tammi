using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.UI.Visualiser;

namespace Deosrc.Tammi.Windows.PlatformServices;

/// <summary>
/// Shows the visualiser in a second MAUI window holding a plain web view. The page has no Blazor in it: it's shown
/// from a string, and sent each snapshot by calling a script function.
/// </summary>
internal sealed class MauiVisualiserWindow(
    VisualiserFeed feed,
    WorkspaceOpenRequests openRequests,
    ISettingsStore settings) : IVisualiserWindow
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
            Title = "Tammi Visualiser",
            // The smallest the window can be, which is also the size it opens at until it's been resized.
            MinimumWidth = 400,
            MinimumHeight = 300,
            Width = 400,
            Height = 300,
        };
        _window = window;

        // As when Home is opened, the statuses are read again.
        _ = feed.RefreshStatusAsync();

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

        // The setting applies to the window while it's open, and once its native window exists.
        void ApplyAlwaysOnTop() => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native
                && native.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = settings.Current.Visualiser.AlwaysOnTop;
            }
        });
        settings.Changed += ApplyAlwaysOnTop;
        window.HandlerChanged += (_, _) => ApplyAlwaysOnTop();

        // A click on a workspace goes to an address the page can't load: it opens the workspace instead.
        webView.Navigating += (_, e) =>
        {
            const string prefix = "tammi://open/";
            if (e.Url.StartsWith(prefix, StringComparison.Ordinal))
            {
                e.Cancel = true;
                openRequests.Request(Uri.UnescapeDataString(e.Url[prefix.Length..]));
                application.ActivateWindow(application.Windows[0]);
            }
        };
        webView.Navigated += (_, _) => Push();
        window.Destroying += (_, _) =>
        {
            feed.Changed -= Push;
            settings.Changed -= ApplyAlwaysOnTop;
            _window = null;
        };
        application.OpenWindow(window);
    });
}
