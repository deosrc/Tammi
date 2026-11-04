using System.Runtime.Versioning;

namespace Deosrc.Tammi.Core.Platform.Pty;

/// <summary>
/// Starts processes in a Windows pseudoconsole (ConPTY).
/// </summary>
/// <remarks>
/// ConPTY needs Windows 10 version 1809 or later.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsPtyService(IProcessLauncher processLauncher) : PtyServiceBase(processLauncher)
{
    /// <inheritdoc />
    public override ProcessRequest GetDefaultShell()
    {
        var powerShell = ProcessLauncher.FindExecutable("pwsh")
            ?? Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(powerShell)
            ? new ProcessRequest(powerShell, "-NoLogo")
            : new ProcessRequest(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe");
    }

    /// <inheritdoc />
    protected override StringComparer EnvironmentComparer => StringComparer.OrdinalIgnoreCase;

    /// <inheritdoc />
    protected override void ConfigureEnvironment(IDictionary<string, string> environment)
    {
        // ConPTY passes 24-bit colour through, and xterm.js shows it.
        environment["COLORTERM"] = "truecolor";
    }

    /// <inheritdoc />
    protected override IPtySession StartCore(PtyStartInfo startInfo) => WindowsPtySession.Start(startInfo);
}
