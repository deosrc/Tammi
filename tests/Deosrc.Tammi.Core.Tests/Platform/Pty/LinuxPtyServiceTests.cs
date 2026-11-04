using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Platform.Pty;

namespace Deosrc.Tammi.Core.Tests.Platform.Pty;

/// <summary>
/// Runs real processes in Linux pseudoterminals, with <c>/bin/sh</c>.
/// </summary>
public class LinuxPtyServiceTests
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Start_RunsProcessAndReportsExitCode()
    {
        await using var session = Start("printf hello; exit 3");

        var output = await new PtyOutput(session).ReadToEndAsync();

        Assert.Contains("hello", output);
        Assert.Equal(3, await session.Exited.WaitAsync(ExitTimeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Start_MakesTerminalTheControllingTerminal()
    {
        // Opening /dev/tty fails without a controlling terminal, and job control needs one.
        await using var session = Start("tty; echo controlling > /dev/tty");

        var output = await new PtyOutput(session).ReadToEndAsync();

        Assert.Contains("/dev/pts/", output);
        Assert.Contains("controlling", output);
    }

    [Fact]
    public async Task Start_UsesSizeWorkingDirectoryAndEnvironment()
    {
        var directory = Directory.CreateTempSubdirectory("tammi-tests-");
        try
        {
            var request = new ProcessRequest("/bin/sh", "-c", "stty size; pwd; echo \"$TERM $TAMMI_TEST\"")
            {
                WorkingDirectory = directory.FullName,
                Environment = new Dictionary<string, string?> { ["TAMMI_TEST"] = "set" },
            };
            await using var session = CreateService().Start(request, new PtySize(100, 30));

            var output = await new PtyOutput(session).ReadToEndAsync();

            Assert.Contains("30 100", output);
            Assert.Contains(directory.FullName, output);
            Assert.Contains("xterm-256color set", output);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Write_SendsInput()
    {
        await using var session = Start("read line; echo \"got:$line\"");

        session.Write("ping\r"u8);
        var output = await new PtyOutput(session).ReadToEndAsync();

        Assert.Contains("got:ping", output);
    }

    [Fact]
    public async Task Output_KeepsUnicode()
    {
        await using var session = Start("printf 'h\\303\\251llo \\342\\234\\223 \\360\\237\\232\\200'");

        var output = await new PtyOutput(session).ReadToEndAsync();

        Assert.Contains("héllo ✓ 🚀", output);
    }

    [Fact]
    public async Task Resize_TellsProcessTheNewSize()
    {
        await using var session = Start("trap 'stty size' WINCH; echo ready; while :; do sleep 0.1; done");
        var output = new PtyOutput(session);
        await output.ReadUntilAsync("ready");

        session.Resize(new PtySize(120, 40));

        await output.ReadUntilAsync("40 120");
        Assert.Equal(new PtySize(120, 40), session.Size);
    }

    [Fact]
    public async Task ReadAsync_ReturnsLargeOutputInFull()
    {
        // More output than the queue holds, so the output thread has to wait for the reader.
        await using var session = Start("head -c 3000000 /dev/zero | tr '\\0' x; echo; echo done");

        var output = await new PtyOutput(session).ReadToEndAsync();

        Assert.Equal(3_000_000, output.Count(c => c == 'x'));
        Assert.Contains("done", output);
    }

    [Fact]
    public async Task DisposeAsync_HangsUpProcess()
    {
        var session = Start("echo ready; sleep 60");
        await new PtyOutput(session).ReadUntilAsync("ready");

        await session.DisposeAsync();

        Assert.True(session.Exited.IsCompleted);
    }

    [Fact]
    public async Task DisposeAsync_KillsProcessThatIgnoresHangUp()
    {
        var session = Start("trap '' HUP; echo ready; while :; do sleep 0.1; done");
        await new PtyOutput(session).ReadUntilAsync("ready");

        await session.DisposeAsync();

        Assert.True(session.Exited.IsCompleted);
    }

    [Fact]
    public void Start_Throws_WhenExecutableIsMissing()
    {
        Assert.Throws<FileNotFoundException>(
            () => CreateService().Start(new ProcessRequest("tammi-no-such-tool"), PtySize.Default));
    }

    [Fact]
    public void Start_Throws_WhenWorkingDirectoryIsMissing()
    {
        var request = new ProcessRequest("/bin/sh")
        {
            WorkingDirectory = Path.Combine(Path.GetTempPath(), "tammi-no-such-folder"),
        };

        Assert.Throws<DirectoryNotFoundException>(() => CreateService().Start(request, PtySize.Default));
    }

    [Fact]
    public void GetDefaultShell_FindsAShell()
    {
        var shell = CreateService().GetDefaultShell();

        Assert.NotNull(new TestProcessLauncher().FindExecutable(shell.FileName));
    }

    private static IPtySession Start(string script) =>
        CreateService().Start(new ProcessRequest("/bin/sh", "-c", script), PtySize.Default);

    private static IPtyService CreateService()
    {
        if (OperatingSystem.IsLinux())
        {
            return new LinuxPtyService(new TestProcessLauncher());
        }

        Assert.Skip("Linux pseudoterminals only work on Linux.");
        return null!;
    }
}
