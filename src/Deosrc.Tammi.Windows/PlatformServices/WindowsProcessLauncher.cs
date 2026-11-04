using System.Diagnostics;
using TAMMi.Core.Platform;

namespace TAMMi.Windows.PlatformServices;

/// <summary>
/// Launches processes on Windows: executables are found on <c>PATH</c> with the extensions in <c>PATHEXT</c>, so
/// e.g. <c>code</c> finds <c>code.cmd</c>, and things are opened through the shell.
/// </summary>
/// <remarks>
/// Windows runs <c>.cmd</c> and <c>.bat</c> files through <c>cmd.exe</c>, which reads its own special characters
/// (e.g. <c>&amp;</c> and <c>%</c>) in the arguments. Don't pass untrusted arguments to them.
/// </remarks>
internal sealed class WindowsProcessLauncher : ProcessLauncherBase
{
    private static readonly string[] DefaultExtensions = [".COM", ".EXE", ".BAT", ".CMD"];

    public override string? FindExecutable(string name)
    {
        // Read the variables each time, so tools installed while TAMMi is running are found.
        var extensions = Environment.GetEnvironmentVariable("PATHEXT")
            ?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var resolver = new ExecutableResolver(
            Environment.GetEnvironmentVariable("PATH"),
            extensions is { Length: > 0 } ? extensions : DefaultExtensions,
            File.Exists);
        return resolver.Resolve(name);
    }

    public override void Open(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }
}
