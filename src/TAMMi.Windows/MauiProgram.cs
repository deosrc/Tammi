using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using TAMMi.Core.Windowing;
using TAMMi.UI;
using TAMMi.Windows.PlatformServices;

namespace TAMMi.Windows;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddTammiUI();
        builder.Services.AddWindowsPlatformServices();
        builder.ConfigureLifecycleEvents(events => events.AddWindows(windows => windows.OnWindowCreated(window =>
            WindowStatePersistence.Attach(
                window,
                IPlatformApplication.Current!.Services.GetRequiredService<WindowStateStore>()))));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
