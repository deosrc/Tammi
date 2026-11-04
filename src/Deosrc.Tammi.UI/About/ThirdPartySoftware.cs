namespace Deosrc.Tammi.UI.About;

/// <summary>
/// Third-party software that Tammi ships with, and its licenses.
/// </summary>
/// <remarks>
/// Keep this up to date when adding a dependency that ships with the app. Each license text is a file in
/// <c>Licenses/</c>, copied from the package or its source repository.
/// </remarks>
public static class ThirdPartySoftware
{
    /// <summary>
    /// The components, in alphabetical order.
    /// </summary>
    public static IReadOnlyList<ThirdPartyComponent> All { get; } =
    [
        new(".NET", "Runtime libraries, e.g. Microsoft.Extensions", ThirdPartyPlatforms.All, "MIT",
            "https://github.com/dotnet/runtime", LicenseFile: "dotnet.txt"),
        new(".NET MAUI", "The Windows host", ThirdPartyPlatforms.Windows, "MIT",
            "https://github.com/dotnet/maui", LicenseFile: "dotnet.txt"),
        new("ASP.NET Core Blazor", "The UI framework", ThirdPartyPlatforms.All, "MIT",
            "https://github.com/dotnet/aspnetcore", LicenseFile: "dotnet.txt"),
        new("libgit2", "The native git library used by LibGit2Sharp, and the libraries bundled with it",
            ThirdPartyPlatforms.All, "GPL-2.0 with linking exception",
            "https://github.com/libgit2/libgit2", LicenseFile: "libgit2.txt"),
        new("LibGit2Sharp", "In-process git reads", ThirdPartyPlatforms.All, "MIT",
            "https://github.com/libgit2/libgit2sharp", LicenseFile: "libgit2sharp.txt"),
        new("Photino", "Photino.Blazor, Photino.NET and Photino.Native: the Linux host window",
            ThirdPartyPlatforms.Linux, "Apache-2.0",
            "https://github.com/tryphotino", LicenseFile: "photino.txt"),
        new("Simple Icons", "The GitHub, JIRA and Claude logos. The logos are trademarks of their owners.",
            ThirdPartyPlatforms.All, "CC0-1.0", "https://simpleicons.org", LicenseFile: "simple-icons.txt"),
        new("WebView2", "Hosts the UI on Windows", ThirdPartyPlatforms.Windows, "BSD-style (Microsoft)",
            "https://aka.ms/webview", LicenseFile: "webview2.txt"),
        new("Win2D", "Used by .NET MAUI", ThirdPartyPlatforms.Windows, "Microsoft Software License Terms",
            "https://github.com/microsoft/Win2D",
            LicenseUrl: "http://www.microsoft.com/web/webpi/eula/eula_win2d_10012014.htm"),
        new("Windows App SDK", "Used by .NET MAUI", ThirdPartyPlatforms.Windows, "Microsoft Software License Terms",
            "https://github.com/microsoft/windowsappsdk", LicenseFile: "windows-app-sdk.txt"),
        new("xterm.js", "@xterm/xterm, @xterm/addon-fit and @xterm/addon-unicode11: the embedded terminal",
            ThirdPartyPlatforms.All, "MIT", "https://github.com/xtermjs/xterm.js", LicenseFile: "xterm.txt"),
    ];

    /// <summary>
    /// Reads a license text embedded from <c>Licenses/</c>.
    /// </summary>
    public static string ReadLicense(string licenseFile)
    {
        var name = $"Deosrc.Tammi.UI.Licenses.{licenseFile}";
        using var stream = typeof(ThirdPartySoftware).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"License text '{licenseFile}' isn't embedded in Deosrc.Tammi.UI.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
