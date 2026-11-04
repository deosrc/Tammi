using System.Diagnostics;
using Tammi.Core.Platform;
using Tammi.Core.Platform.Pty;

namespace Tammi.Core.Tests.Platform.Pty;

/// <summary>
/// Runs real processes in Windows pseudoconsoles, with <c>cmd.exe</c> and Windows PowerShell.
/// </summary>
public class WindowsPtyServiceTests
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(15);

    // Longer than the hang-up and kill timeouts together, but well short of the 60 seconds the ping runs for.
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Start_RunsProcessAndReportsExitCode()
    {
        await using var session = StartCmd("echo hello& exit /b 3");

        var output = await new PtyOutput(session).ReadToEndAsync();

        Assert.Contains("hello", output);
        Assert.Equal(3, await session.Exited.WaitAsync(ExitTimeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Start_UsesWorkingDirectoryAndEnvironment()
    {
        var directory = Directory.CreateTempSubdirectory("tammi-tests-");
        try
        {
            var request = new ProcessRequest("cmd.exe", "/d", "/c", "cd& echo value:%TAMMI_TEST%")
            {
                WorkingDirectory = directory.FullName,
                Environment = new Dictionary<string, string?> { ["TAMMI_TEST"] = "set" },
            };
            await using var session = CreateService().Start(request, PtySize.Default);

            var output = await new PtyOutput(session).ReadToEndAsync();

            Assert.Contains(directory.Name, output);
            Assert.Contains("value:set", output);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Start_UsesSize()
    {
        var request = new ProcessRequest(
            "powershell.exe",
            "-NoLogo",
            "-NoProfile",
            "-Command",
            "Write-Output \"size:$([Console]::WindowWidth)x$([Console]::WindowHeight)\"");
        await using var session = CreateService().Start(request, new PtySize(100, 30));

        var output = await new PtyOutput(session).ReadToEndAsync();

        Assert.Contains("size:100x30", output);
    }

    [Fact]
    public async Task Write_SendsInput()
    {
        await using var session = StartCmd("set /p line=& call echo got:%line%");

        session.Write("ping\r"u8);
        var output = await new PtyOutput(session).ReadToEndAsync();

        Assert.Contains("got:ping", output);
    }

    [Fact]
    public async Task DisposeAsync_EndsProcess()
    {
        // The ping is a child of the shell that's attached to the pseudoconsole too, so dispose must end it rather
        // than wait the minute it runs for.
        var session = StartCmd("echo ready& ping -n 60 127.0.0.1 > nul");
        await new PtyOutput(session).ReadUntilAsync("ready");

        var stopwatch = Stopwatch.StartNew();
        await session.DisposeAsync();

        Assert.True(session.Exited.IsCompleted);
        Assert.True(stopwatch.Elapsed < DisposeTimeout, $"Dispose took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Start_Throws_WhenExecutableIsMissing()
    {
        Assert.Throws<FileNotFoundException>(
            () => CreateService().Start(new ProcessRequest("tammi-no-such-tool"), PtySize.Default));
    }

    [Fact]
    public void GetDefaultShell_FindsPowerShell()
    {
        var shell = CreateService().GetDefaultShell();

        Assert.True(File.Exists(shell.FileName), shell.FileName);
        Assert.Contains(Path.GetFileNameWithoutExtension(shell.FileName), new[] { "pwsh", "powershell" });
    }

    private static IPtySession StartCmd(string script) =>
        CreateService().Start(new ProcessRequest("cmd.exe", "/d", "/c", script), PtySize.Default);

    private static IPtyService CreateService()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPtyService(new TestProcessLauncher());
        }

        Assert.Skip("Windows pseudoconsoles only work on Windows.");
        return null!;
    }
}
