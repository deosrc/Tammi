using System.Runtime.Versioning;

namespace TAMMi.Core.Platform.Pty;

/// <summary>
/// Starts processes in a Linux pseudoterminal.
/// </summary>
/// <remarks>
/// <para>
/// This does what <c>forkpty</c> does, but with <c>posix_spawn</c>. A .NET process can't safely run code in a forked
/// child before it calls <c>exec</c>, and <c>posix_spawn</c> needs no code in the child: it opens the terminal,
/// starts a new session (so the terminal becomes the process's controlling terminal) and sets the working folder
/// itself.
/// </para>
/// <para>
/// It needs glibc 2.29 or later, for <c>posix_spawn_file_actions_addchdir_np</c>.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class LinuxPtyService(IProcessLauncher processLauncher) : PtyServiceBase(processLauncher)
{
    private const string FallbackShell = "/bin/sh";

    // Variables that describe the terminal TAMMi was started from, if any, rather than TAMMi's own.
    private static readonly string[] OuterTerminalVariables =
        ["COLUMNS", "LINES", "TERM_PROGRAM", "TERM_PROGRAM_VERSION", "VTE_VERSION", "WINDOWID"];

    /// <inheritdoc />
    public override ProcessRequest GetDefaultShell()
    {
        var shell = Environment.GetEnvironmentVariable("SHELL");
        return string.IsNullOrWhiteSpace(shell) || ProcessLauncher.FindExecutable(shell) is null
            ? new ProcessRequest(FallbackShell)
            : new ProcessRequest(shell);
    }

    /// <inheritdoc />
    protected override void ConfigureEnvironment(IDictionary<string, string> environment)
    {
        foreach (var name in OuterTerminalVariables)
        {
            environment.Remove(name);
        }

        // What xterm.js emulates.
        environment["TERM"] = "xterm-256color";
        environment["COLORTERM"] = "truecolor";

        // Desktop sessions usually set a UTF-8 locale. If nothing is set, programs would assume ASCII.
        if (!environment.ContainsKey("LANG") && !environment.ContainsKey("LC_ALL")
            && !environment.ContainsKey("LC_CTYPE"))
        {
            environment["LANG"] = "C.UTF-8";
        }
    }

    /// <inheritdoc />
    protected override IPtySession StartCore(PtyStartInfo startInfo) => LinuxPtySession.Start(startInfo);
}
