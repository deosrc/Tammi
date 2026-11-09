using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Photino.Blazor;
using Photino.NET;
using Deosrc.Tammi.Core;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Terminals;
using Deosrc.Tammi.Core.Windowing;
using Deosrc.Tammi.Linux.PlatformServices;
using Deosrc.Tammi.UI;
using Deosrc.Tammi.UI.Logging;
using Deosrc.Tammi.UI.Workspaces;

[assembly: SupportedOSPlatform("linux")]

namespace Deosrc.Tammi.Linux;

internal static class Program
{
    private const int MaxWindowSize = 100_000;

    private static readonly WindowState DefaultWindowState = new(1280, 800, IsMaximized: false);

    [STAThread]
    private static void Main(string[] args)
    {
        // Held until the process ends.
        using var instanceLock = SingleInstanceLock.TryAcquire(new XdgAppPaths().DataDirectory);
        if (instanceLock is null)
        {
            Console.Error.WriteLine(SingleInstanceLock.AlreadyRunningMessage);
            return;
        }

        var builder = PhotinoBlazorAppBuilder.CreateDefault(CreateFileProvider(), args);
        builder.Services.AddTammiUI();
        builder.Services.AddLinuxPlatformServices();
        builder.RootComponents.Add<Routes>("#app");

        var app = builder.Build();
        app.Services.UseTammiLogging();
        var windowStateStore = app.Services.GetRequiredService<WindowStateStore>();
        var windowState = windowStateStore.Load() ?? DefaultWindowState;

        app.MainWindow
            .SetLogVerbosity(0)
            .SetTitle(AppInfo.Name)
            .SetMinSize(800, 600)
            // On Linux, Photino sends the maximum size along with the minimum size. Left unset it's -1, which
            // stops the window from being resized or maximised, so set a maximum no screen will reach.
            .SetMaxSize(MaxWindowSize, MaxWindowSize)
            .SetSize(windowState.Width, windowState.Height)
            .SetMaximized(windowState.IsMaximized)
            .Center();
        var windowStateTracker = TrackWindowState(app.MainWindow, windowState);
        var agents = app.Services.GetRequiredService<WorkspaceAgents>();
        app.MainWindow.RegisterWindowClosingHandler((_, _) => !ConfirmClose(app.MainWindow, agents));

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            app.MainWindow.ShowMessage("Unhandled error", e.ExceptionObject.ToString());

        app.Run();

        // Run returns once the window has closed.
        windowStateStore.Save(windowStateTracker.Current);
        Task.WhenAll(
            app.Services.GetRequiredService<WorkspaceTerminals>().DisposeAsync().AsTask(),
            agents.DisposeAsync().AsTask()).GetAwaiter().GetResult();
        app.Services.StopTammiLogging();
    }

    /// <summary>
    /// Asks the user whether to close Tammi, if any agents are running.
    /// </summary>
    /// <returns>Whether the window should close.</returns>
    private static bool ConfirmClose(PhotinoWindow window, WorkspaceAgents agents)
    {
        var running = agents.CountRunning();
        return running == 0
            || window.ShowMessage(
                AppCloseWarning.Title,
                AppCloseWarning.Message(running),
                PhotinoDialogButtons.YesNo,
                PhotinoDialogIcon.Warning) == PhotinoDialogResult.Yes;
    }

    private static WindowStateTracker TrackWindowState(PhotinoWindow window, WindowState initial)
    {
        var tracker = new WindowStateTracker(initial);

        void Observe()
        {
            var mode = window.Maximized ? WindowMode.Maximized
                : window.Minimized ? WindowMode.Minimized
                : WindowMode.Normal;
            tracker.Observe(window.Width, window.Height, mode);
        }

        window
            .RegisterSizeChangedHandler((_, _) => Observe())
            .RegisterMaximizedHandler((_, _) => Observe())
            .RegisterRestoredHandler((_, _) => Observe())
            .RegisterMinimizedHandler((_, _) => Observe());
        return tracker;
    }

    /// <summary>
    /// Serves the app's own <c>wwwroot</c>, plus static web assets from referenced projects and packages
    /// when running from a build (see <see cref="StaticWebAssetsFileProvider"/>).
    /// </summary>
    private static IFileProvider CreateFileProvider()
    {
        var wwwroot = new PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        var staticWebAssets = StaticWebAssetsFileProvider.TryCreate();
        return staticWebAssets is null
            ? wwwroot
            : new CompositeFileProvider(wwwroot, staticWebAssets);
    }
}
