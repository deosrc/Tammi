using System.Runtime.Versioning;
using Microsoft.Extensions.FileProviders;
using Photino.Blazor;
using TAMMi.Core;
using TAMMi.Linux.PlatformServices;
using TAMMi.UI;

[assembly: SupportedOSPlatform("linux")]

namespace TAMMi.Linux;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(CreateFileProvider(), args);
        builder.RootComponents.Add<Routes>("#app");
        builder.Services.AddLinuxPlatformServices();

        var app = builder.Build();
        app.MainWindow
            .SetTitle(AppInfo.Name)
            .SetMinSize(800, 600)
            .SetSize(1280, 800)
            .Center();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            app.MainWindow.ShowMessage("Unhandled error", e.ExceptionObject.ToString());

        app.Run();
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
