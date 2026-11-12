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

    // Set once the main window has been created.
    private static bool _mainWindowCreated;

    public static MauiApp CreateMauiApp()
    {
        var dataDirectory = new WindowsAppPaths().DataDirectory;
        _instanceLock = SingleInstanceLock.TryAcquire(dataDirectory);
        if (_instanceLock is null)
        {
            // Ask the running copy to show its window, rather than starting another.
            if (!SingleInstanceLock.SignalRunningInstance(dataDirectory))
            {
                NativeMethods.MessageBox(
                    IntPtr.Zero,
                    SingleInstanceLock.AlreadyRunningMessage,
                    Deosrc.Tammi.Core.AppInfo.Name,
                    0x30);
            }

            Environment.Exit(0);
        }

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddTammiUI();
        builder.Services.AddWindowsPlatformServices();
        builder.ConfigureLifecycleEvents(events => events.AddWindows(windows => windows.OnWindowCreated(window =>
        {
            // This is called for every window, and what follows is only for the main window, which is created first.
            // The visualiser's window is another one: with this it would open at the main window's size, save its own
            // size as the main window's, and ask about running agents and end them all when it closed.
            if (_mainWindowCreated)
            {
                return;
            }

            _mainWindowCreated = true;
            var services = IPlatformApplication.Current!.Services;
            WindowStatePersistence.Attach(window, services.GetRequiredService<WindowStateStore>());

            // Another copy being started shows this window instead.
            _instanceLock!.Activated += () => window.DispatcherQueue.TryEnqueue(() => ShowWindow(window));
            _instanceLock.StartListening();

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

    private static void ShowWindow(Microsoft.UI.Xaml.Window window)
    {
        if (window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter { State: Microsoft.UI.Windowing.OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        window.Activate();
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern int MessageBox(IntPtr window, string text, string caption, uint type);
    }
}
