using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;
using static TAMMi.Core.Platform.Pty.WindowsNative;

namespace TAMMi.Core.Platform.Pty;

[SupportedOSPlatform("windows")]
internal sealed class WindowsPtySession : PtySession
{
    private const int ExitQuietChecks = 10;
    private static readonly TimeSpan ExitQuietPeriod = TimeSpan.FromMilliseconds(100);

    private readonly nint _pseudoConsole;
    private readonly FileStream _input;
    private readonly FileStream _output;
    private readonly SafeProcessHandle _process;
    private readonly ProcessWaitHandle _processWaitHandle;
    private readonly RegisteredWaitHandle _exitRegistration;
    private readonly Lock _pseudoConsoleLock = new();
    private bool _pseudoConsoleClosed;
    private long _lastOutputTicks;

    private WindowsPtySession(
        nint pseudoConsole,
        SafeFileHandle input,
        SafeFileHandle output,
        SafeProcessHandle process,
        int processId,
        PtySize size)
        : base(processId, size)
    {
        _pseudoConsole = pseudoConsole;
        _input = new FileStream(input, FileAccess.Write, bufferSize: 0);
        _output = new FileStream(output, FileAccess.Read, bufferSize: 0);
        _process = process;
        _processWaitHandle = new ProcessWaitHandle(process);
        _exitRegistration = ThreadPool.RegisterWaitForSingleObject(
            _processWaitHandle,
            (_, _) => OnProcessExited(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: true);
        StartPumps();
    }

    public static unsafe WindowsPtySession Start(PtyStartInfo startInfo)
    {
        SafeFileHandle? inputRead = null;
        SafeFileHandle? inputWrite = null;
        SafeFileHandle? outputRead = null;
        SafeFileHandle? outputWrite = null;
        nint pseudoConsole = 0;
        nint attributeList = 0;
        var started = false;
        try
        {
            if (!CreatePipe(out inputRead, out inputWrite, 0, 0) || !CreatePipe(out outputRead, out outputWrite, 0, 0))
            {
                throw new Win32Exception();
            }

            Marshal.ThrowExceptionForHR(
                CreatePseudoConsole(ToCoord(startInfo.Size), inputRead, outputWrite, 0, out pseudoConsole));

            // The first call only asks how big the list must be, so it's expected to fail.
            nint attributeListSize = 0;
            InitializeProcThreadAttributeList(0, 1, 0, ref attributeListSize);
            attributeList = Marshal.AllocHGlobal(attributeListSize);
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize)
                || !UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    ProcThreadAttributePseudoConsole,
                    pseudoConsole,
                    sizeof(nint),
                    0,
                    0))
            {
                throw new Win32Exception();
            }

            var startupInfo = new StartupInfoEx
            {
                StartupInfo = new StartupInfo
                {
                    Cb = sizeof(StartupInfoEx),

                    // With no standard handles, the process uses the pseudoconsole's, rather than any it might
                    // inherit from TAMMi.
                    Flags = StartFUseStdHandles,
                },
                AttributeList = attributeList,
            };

            // CreateProcessW can change the command line, so it needs a writable copy.
            var commandLine = (WindowsCommandLine.Build(startInfo.FileName, startInfo.Arguments) + '\0').ToCharArray();
            var environment = BuildEnvironmentBlock(startInfo.Environment).ToCharArray();

            ProcessInformation processInformation;
            fixed (char* commandLinePointer = commandLine)
            fixed (char* environmentPointer = environment)
            {
                if (!CreateProcess(
                    startInfo.FileName,
                    commandLinePointer,
                    0,
                    0,
                    inheritHandles: false,
                    ExtendedStartupInfoPresent | CreateUnicodeEnvironment,
                    environmentPointer,
                    startInfo.WorkingDirectory,
                    ref startupInfo,
                    out processInformation))
                {
                    throw new Win32Exception();
                }
            }

            CloseHandle(processInformation.Thread);
            var session = new WindowsPtySession(
                pseudoConsole,
                inputWrite,
                outputRead,
                new SafeProcessHandle(processInformation.Process, ownsHandle: true),
                processInformation.ProcessId,
                startInfo.Size);
            started = true;
            return session;
        }
        finally
        {
            // The pseudoconsole has its own copies of its ends of the pipes.
            inputRead?.Dispose();
            outputWrite?.Dispose();

            if (attributeList != 0)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (!started)
            {
                if (pseudoConsole != 0)
                {
                    ClosePseudoConsole(pseudoConsole);
                }

                inputWrite?.Dispose();
                outputRead?.Dispose();
            }
        }
    }

    protected override int ReadOutput(Span<byte> buffer)
    {
        try
        {
            var count = _output.Read(buffer);
            Volatile.Write(ref _lastOutputTicks, Environment.TickCount64);
            return count;
        }
        catch (IOException)
        {
            // The pseudoconsole has closed the pipe.
            return 0;
        }
    }

    protected override void WriteInput(ReadOnlySpan<byte> data) => _input.Write(data);

    protected override void ResizeCore(PtySize size)
    {
        lock (_pseudoConsoleLock)
        {
            if (!_pseudoConsoleClosed)
            {
                ResizePseudoConsole(_pseudoConsole, ToCoord(size));
            }
        }
    }

    protected override async Task ClosePseudoterminalAsync()
    {
        // Closing the pseudoconsole ends the processes attached to it, as closing a console window does. Older
        // versions of Windows wait until its output has been read, so close it off the caller's thread while the
        // output thread keeps reading.
        await Task.Run(ClosePseudoConsoleOnce).ConfigureAwait(false);
        await OutputEnded.ConfigureAwait(false);
        await InputWritten.ConfigureAwait(false);
        await _input.DisposeAsync().ConfigureAwait(false);
        await _output.DisposeAsync().ConfigureAwait(false);
    }

    protected override void Kill() => TerminateProcess(_process, 1);

    protected override void ReleaseProcess()
    {
        _exitRegistration.Unregister(null);
        _processWaitHandle.Dispose();
        _process.Dispose();
    }

    private static Coord ToCoord(PtySize size) => new() { X = (short)size.Columns, Y = (short)size.Rows };

    /// <summary>
    /// Builds a Unicode environment block: <c>NAME=value</c> strings, each ending with a null character, sorted by
    /// name and followed by one more null character.
    /// </summary>
    private static string BuildEnvironmentBlock(IReadOnlyDictionary<string, string> environment)
    {
        var block = new StringBuilder();
        foreach (var (name, value) in environment.OrderBy(variable => variable.Key, StringComparer.OrdinalIgnoreCase))
        {
            block.Append(name).Append('=').Append(value).Append('\0');
        }

        return block.Append('\0').ToString();
    }

    private void OnProcessExited()
    {
        int exitCode;
        try
        {
            exitCode = GetExitCodeProcess(_process, out var code) ? unchecked((int)code) : -1;
        }
        catch (ObjectDisposedException)
        {
            // The session was disposed as the process exited.
            exitCode = -1;
        }

        OnExited(exitCode);
        _ = CloseAfterExitAsync();
    }

    /// <summary>
    /// Closes the pseudoconsole once the exited process's last output has arrived. The pseudoconsole stays open
    /// until it's closed, so this ends the output, as other terminals do when their shell exits.
    /// </summary>
    private async Task CloseAfterExitAsync()
    {
        // Older versions of Windows drop output that's still in the pseudoconsole when it's closed. Wait until the
        // output has been quiet for a moment, but not for ever.
        for (var i = 0; i < ExitQuietChecks; i++)
        {
            await Task.Delay(ExitQuietPeriod).ConfigureAwait(false);
            if (Environment.TickCount64 - Volatile.Read(ref _lastOutputTicks) >= ExitQuietPeriod.TotalMilliseconds)
            {
                break;
            }
        }

        ClosePseudoConsoleOnce();
    }

    private void ClosePseudoConsoleOnce()
    {
        lock (_pseudoConsoleLock)
        {
            if (_pseudoConsoleClosed)
            {
                return;
            }

            _pseudoConsoleClosed = true;
        }

        ClosePseudoConsole(_pseudoConsole);
    }

    /// <summary>
    /// Lets a process handle be waited on, e.g. with <see cref="ThreadPool.RegisterWaitForSingleObject(WaitHandle,
    /// WaitOrTimerCallback, object, int, bool)"/>.
    /// </summary>
    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeProcessHandle process)
        {
            SafeWaitHandle = new SafeWaitHandle(process.DangerousGetHandle(), ownsHandle: false);
        }
    }
}
