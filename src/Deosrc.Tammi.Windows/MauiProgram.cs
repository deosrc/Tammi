using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Terminals;
using Deosrc.Tammi.Core.Windowing;
using Deosrc.Tammi.UI;
using Deosrc.Tammi.UI.Logging;
using Deosrc.Tammi.Windows.PlatformServices;

namespace Deosrc.Tammi.Windows;

public static class MauiProgram
{
    // Held until the process ends.
    private static SingleInstanceLock? _instanceLock;

    public static MauiApp CreateMauiApp()
    {
        _instanceLock = SingleInstanceLock.TryAcquire(new WindowsAppPaths().DataDirectory);
        if (_instanceLock is null)
        {
            NativeMethods.MessageBox(IntPtr.Zero, SingleInstanceLock.AlreadyRunningMessage, Deosrc.Tammi.Core.AppInfo.Name, 0x30);
            Environment.Exit(0);
        }

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
            window.Closed += (_, _) =>
            {
                Task.Run(() => Task.WhenAll(
                    terminals.DisposeAsync().AsTask(),
                    agents.DisposeAsync().AsTask())).GetAwaiter().GetResult();
                services.StopTammiLogging();
            };
        })));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        // After AddTammiUI, which replaces the logging providers registered before it.
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        app.Services.UseTammiLogging();
        return app;
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern int MessageBox(IntPtr window, string text, string caption, uint type);
    }
}
