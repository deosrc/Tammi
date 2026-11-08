using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Linux.PlatformServices;

/// <summary>
/// Launches processes on Linux: executables are found on <c>PATH</c>, and things are opened with <c>xdg-open</c>.
/// </summary>
internal sealed class LinuxProcessLauncher : ProcessLauncherBase
{
    private const UnixFileMode AnyExecute =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    // Runs its arguments in the background, with no input and its output discarded, and exits straight away. The
    // process is then adopted by init (or the session's subreaper), so it isn't Tammi's child.
    private const string DetachScript = "\"$@\" </dev/null >/dev/null 2>&1 &";

    public override string? FindExecutable(string name)
    {
        // Read PATH each time, so tools installed while Tammi is running are found.
        var resolver = new ExecutableResolver(Environment.GetEnvironmentVariable("PATH"), [""], IsExecutable);
        return resolver.Resolve(name);
    }

    public override void Open(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        StartDetached(new ProcessRequest("xdg-open", target));
    }

    /// <inheritdoc />
    /// <remarks>
    /// A child process shares Tammi's terminal and output. If Tammi was started from a terminal, closing it would end
    /// the tool too, and once Tammi has exited the tool could be stopped by writing to its closed output. So the tool is
    /// started through <c>sh</c>, without them, and in a new session with <c>setsid</c> where it's installed.
    /// </remarks>
    public override void StartDetached(ProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fileName = FindExecutable(request.FileName)
            ?? throw new FileNotFoundException($"Couldn't find the executable '{request.FileName}'.", request.FileName);
        string[] command = FindExecutable("setsid") is { } setsid
            ? [setsid, fileName, .. request.Arguments]
            : [fileName, .. request.Arguments];

        // The argument after the script is its $0, so the command is all of "$@".
        Start(request with { FileName = "/bin/sh", Arguments = ["-c", DetachScript, "sh", .. command] });
    }

    private static bool IsExecutable(string path) =>
        !OperatingSystem.IsWindows() && File.Exists(path) && (File.GetUnixFileMode(path) & AnyExecute) != 0;
}
