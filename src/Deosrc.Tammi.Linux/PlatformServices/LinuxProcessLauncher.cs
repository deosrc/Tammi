using TAMMi.Core.Platform;

namespace TAMMi.Linux.PlatformServices;

/// <summary>
/// Launches processes on Linux: executables are found on <c>PATH</c>, and things are opened with <c>xdg-open</c>.
/// </summary>
internal sealed class LinuxProcessLauncher : ProcessLauncherBase
{
    private const UnixFileMode AnyExecute =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    public override string? FindExecutable(string name)
    {
        // Read PATH each time, so tools installed while TAMMi is running are found.
        var resolver = new ExecutableResolver(Environment.GetEnvironmentVariable("PATH"), [""], IsExecutable);
        return resolver.Resolve(name);
    }

    public override void Open(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        Start(new ProcessRequest("xdg-open", target));
    }

    private static bool IsExecutable(string path) =>
        !OperatingSystem.IsWindows() && File.Exists(path) && (File.GetUnixFileMode(path) & AnyExecute) != 0;
}
