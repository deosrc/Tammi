using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static TAMMi.Core.Platform.Pty.LinuxNative;

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

[SupportedOSPlatform("linux")]
internal sealed class LinuxPtySession : PtySession
{
    // How often blocked reads and writes check whether the session is closing.
    private const int PollIntervalMilliseconds = 100;

    private readonly int _master;
    private readonly Lock _masterLock = new();
    private bool _masterClosed;

    private LinuxPtySession(int master, int processId, PtySize size)
        : base(processId, size)
    {
        _master = master;

        var exitThread = new Thread(WaitForExit)
        {
            IsBackground = true,
            Name = $"PTY exit ({processId})",
        };
        exitThread.Start();
        StartPumps();
    }

    public static unsafe LinuxPtySession Start(PtyStartInfo startInfo)
    {
        var master = PosixOpenPt(ORdWr | ONoCtty | OCloExec);
        ThrowIfFailed(master, "posix_openpt");
        try
        {
            ThrowIfFailed(GrantPt(master), "grantpt");
            ThrowIfFailed(UnlockPt(master), "unlockpt");

            const int nameLength = 256;
            var name = stackalloc byte[nameLength];
            ThrowIfError(PtsNameR(master, name, nameLength), "ptsname_r");

            SetWindowSize(master, startInfo.Size);

            // Reads and writes wait in poll(), so they can stop when the session closes.
            var flags = Fcntl(master, FGetFl, 0);
            ThrowIfFailed(flags, "fcntl");
            ThrowIfFailed(Fcntl(master, FSetFl, flags | ONonBlock), "fcntl");

            // TAMMi never opens the terminal's other end itself. Only the process holds it, so once the process and
            // its children have closed it, reads report the end of the output.
            var processId = Spawn(startInfo, name);
            return new LinuxPtySession(master, processId, startInfo.Size);
        }
        catch
        {
            Close(master);
            throw;
        }
    }

    protected override unsafe int ReadOutput(Span<byte> buffer)
    {
        var pollFd = new PollFd { Fd = _master, Events = PollIn };
        while (!IsClosing)
        {
            var ready = Poll(&pollFd, 1, PollIntervalMilliseconds);
            if (ready < 0)
            {
                if (Marshal.GetLastPInvokeError() == EIntr)
                {
                    continue;
                }

                return 0;
            }

            if (ready == 0)
            {
                // The process has exited and its output has been read. A background process it started may still
                // have the terminal open, but the session is over, as it is in other terminal emulators.
                if (Exited.IsCompleted)
                {
                    return 0;
                }

                continue;
            }

            nint count;
            fixed (byte* bytes = buffer)
            {
                count = LinuxNative.Read(_master, bytes, (nuint)buffer.Length);
            }

            if (count >= 0)
            {
                return (int)count;
            }

            var error = Marshal.GetLastPInvokeError();
            if (error != EIntr && error != EAgain)
            {
                // EIO: every process has closed the terminal.
                return 0;
            }
        }

        return 0;
    }

    protected override unsafe void WriteInput(ReadOnlySpan<byte> data)
    {
        fixed (byte* bytes = data)
        {
            var written = 0;
            while (written < data.Length && !IsClosing)
            {
                var count = LinuxNative.Write(_master, bytes + written, (nuint)(data.Length - written));
                if (count >= 0)
                {
                    written += (int)count;
                    continue;
                }

                var error = Marshal.GetLastPInvokeError();
                if (error == EAgain)
                {
                    // The process isn't reading its input fast enough. Wait for room.
                    var pollFd = new PollFd { Fd = _master, Events = PollOut };
                    Poll(&pollFd, 1, PollIntervalMilliseconds);
                }
                else if (error != EIntr)
                {
                    ThrowIfFailed(-1, "write");
                }
            }
        }
    }

    protected override void ResizeCore(PtySize size)
    {
        lock (_masterLock)
        {
            if (!_masterClosed)
            {
                SetWindowSize(_master, size);
            }
        }
    }

    protected override async Task ClosePseudoterminalAsync()
    {
        // Both loops use the descriptor, so wait for them to stop before closing it. They check IsClosing between
        // polls, so this is quick.
        await OutputEnded.ConfigureAwait(false);
        await InputWritten.ConfigureAwait(false);

        lock (_masterLock)
        {
            // Closing the terminal hangs it up: the kernel sends SIGHUP to the process, as when a terminal window
            // is closed.
            _masterClosed = true;
            Close(_master);
        }
    }

    protected override void Kill()
    {
        // The new session made the process the leader of its own process group, so this also kills the children
        // that are in its group.
        if (LinuxNative.Kill(-ProcessId, SigKill) < 0)
        {
            LinuxNative.Kill(ProcessId, SigKill);
        }
    }

    private static unsafe void SetWindowSize(int fd, PtySize size)
    {
        var winSize = new WinSize { Columns = (ushort)size.Columns, Rows = (ushort)size.Rows };
        ThrowIfFailed(Ioctl(fd, TiocSWinSz, &winSize), "ioctl(TIOCSWINSZ)");
    }

    private static unsafe int Spawn(PtyStartInfo startInfo, byte* terminalPath)
    {
        var allocations = new List<nint>();
        var fileActions = NativeMemory.AllocZeroed(OpaqueStructSize);
        var attributes = NativeMemory.AllocZeroed(OpaqueStructSize);
        var signalMask = NativeMemory.AllocZeroed(OpaqueStructSize);
        var defaultSignals = NativeMemory.AllocZeroed(OpaqueStructSize);
        var fileActionsReady = false;
        var attributesReady = false;
        try
        {
            ThrowIfError(FileActionsInit(fileActions), "posix_spawn_file_actions_init");
            fileActionsReady = true;

            // As the leader of a new session with no controlling terminal, the first terminal the process opens
            // becomes its controlling terminal. glibc starts the session before the file actions run.
            ThrowIfError(FileActionsAddOpen(fileActions, 0, terminalPath, ORdWr, 0), "addopen");
            ThrowIfError(FileActionsAddDup2(fileActions, 0, 1), "adddup2");
            ThrowIfError(FileActionsAddDup2(fileActions, 0, 2), "adddup2");
            ThrowIfError(
                FileActionsAddChdir(fileActions, ToUtf8(startInfo.WorkingDirectory, allocations)),
                "addchdir_np");

            ThrowIfError(SpawnAttrInit(attributes), "posix_spawnattr_init");
            attributesReady = true;

            // .NET ignores some signals (e.g. SIGPIPE), and ignored signals are inherited. Give the process the
            // defaults that a process started from a shell would have.
            SigEmptySet(signalMask);
            SigFillSet(defaultSignals);
            ThrowIfError(SpawnAttrSetSigMask(attributes, signalMask), "posix_spawnattr_setsigmask");
            ThrowIfError(SpawnAttrSetSigDefault(attributes, defaultSignals), "posix_spawnattr_setsigdefault");
            ThrowIfError(
                SpawnAttrSetFlags(attributes, PosixSpawnSetSid | PosixSpawnSetSigMask | PosixSpawnSetSigDef),
                "posix_spawnattr_setflags");

            var path = ToUtf8(startInfo.FileName, allocations);
            var argv = ToUtf8Array([startInfo.FileName, .. startInfo.Arguments], allocations);
            var envp = ToUtf8Array(
                startInfo.Environment.Select(variable => $"{variable.Key}={variable.Value}").ToList(),
                allocations);

            int processId;
            ThrowIfError(PosixSpawn(&processId, path, fileActions, attributes, argv, envp), "posix_spawn");
            return processId;
        }
        finally
        {
            if (fileActionsReady)
            {
                FileActionsDestroy(fileActions);
            }

            if (attributesReady)
            {
                SpawnAttrDestroy(attributes);
            }

            NativeMemory.Free(fileActions);
            NativeMemory.Free(attributes);
            NativeMemory.Free(signalMask);
            NativeMemory.Free(defaultSignals);
            foreach (var allocation in allocations)
            {
                Marshal.FreeCoTaskMem(allocation);
            }
        }
    }

    private static unsafe byte* ToUtf8(string value, List<nint> allocations)
    {
        var pointer = Marshal.StringToCoTaskMemUTF8(value);
        allocations.Add(pointer);
        return (byte*)pointer;
    }

    /// <summary>
    /// Creates a null-terminated array of strings, as <c>argv</c> and <c>envp</c> are.
    /// </summary>
    private static unsafe byte** ToUtf8Array(IReadOnlyList<string> values, List<nint> allocations)
    {
        var array = (byte**)Marshal.AllocCoTaskMem((values.Count + 1) * sizeof(byte*));
        allocations.Add((nint)array);
        for (var i = 0; i < values.Count; i++)
        {
            array[i] = ToUtf8(values[i], allocations);
        }

        array[values.Count] = null;
        return array;
    }

    private unsafe void WaitForExit()
    {
        int status;
        while (WaitPid(ProcessId, &status, 0) < 0)
        {
            if (Marshal.GetLastPInvokeError() != EIntr)
            {
                // Something else reaped the process, so its exit code is unknown.
                OnExited(-1);
                return;
            }
        }

        var signal = status & 0x7f;
        OnExited(signal == 0 ? (status >> 8) & 0xff : 128 + signal);
    }
}
