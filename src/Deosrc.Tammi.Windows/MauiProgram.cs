using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using TAMMi.Core.Agents;
using TAMMi.Core.Terminals;
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
        {
            var services = IPlatformApplication.Current!.Services;
            WindowStatePersistence.Attach(window, services.GetRequiredService<WindowStateStore>());

            // Closing the window warns first if any agents are running.
            var agents = services.GetRequiredService<WorkspaceAgents>();
            CloseConfirmation.Attach(window, agents);

            // All terminals and agents end when the app closes. They're ended off the UI thread, which is waiting for
            // them.
            var terminals = services.GetRequiredService<WorkspaceTerminals>();
            window.Closed += (_, _) => Task.Run(() => Task.WhenAll(
                terminals.DisposeAsync().AsTask(),
                agents.DisposeAsync().AsTask())).GetAwaiter().GetResult();
        })));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
