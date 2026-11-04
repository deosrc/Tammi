using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Terminals;
using Deosrc.Tammi.Core.Tests.Platform.Pty;
using Deosrc.Tammi.Core.Tests.Settings;

namespace Deosrc.Tammi.Core.Tests.Terminals;

public class TerminalLauncherTests
{
    private readonly RecordingPtyService _pty = new() { DefaultShell = new ProcessRequest("default-shell", "-NoLogo") };

    [Fact]
    public void GetShell_ByDefault_IsPlatformDefaultShell()
    {
        var launcher = new TerminalLauncher(_pty, new InMemorySettingsStore());

        Assert.Same(_pty.DefaultShell, launcher.GetShell());
    }

    [Fact]
    public void GetShell_WithShellSetting_UsesItAndItsArguments()
    {
        var launcher = new TerminalLauncher(_pty, new InMemorySettingsStore(new AppSettings
        {
            Tools = new ToolSettings { TerminalShell = "zsh", TerminalShellArguments = ["-l", "-o", "no beep"] },
        }));

        var shell = launcher.GetShell();

        Assert.Equal("zsh", shell.FileName);
        Assert.Equal(["-l", "-o", "no beep"], shell.Arguments);
        Assert.Null(shell.WorkingDirectory);
    }

    [Fact]
    public async Task Start_UsesSettingAtTheTimeItStarts()
    {
        var settings = new InMemorySettingsStore();
        var launcher = new TerminalLauncher(_pty, settings);
        launcher.Start(null, PtySize.Default);

        await settings.UpdateAsync(
            s => s with { Tools = s.Tools with { TerminalShell = "fish" } },
            TestContext.Current.CancellationToken);
        launcher.Start(null, PtySize.Default);

        Assert.Equal(["default-shell", "fish"], _pty.Requests.Select(r => r.Request.FileName));
    }

    [Fact]
    public void Start_StartsShellInWorkingDirectory()
    {
        var launcher = new TerminalLauncher(_pty, new InMemorySettingsStore());
        var directory = Path.GetTempPath();

        launcher.Start(directory, new PtySize(100, 30));

        var (request, size) = Assert.Single(_pty.Requests);
        Assert.Equal("default-shell", request.FileName);
        Assert.Equal(["-NoLogo"], request.Arguments);
        Assert.Equal(directory, request.WorkingDirectory);
        Assert.Equal(new PtySize(100, 30), size);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Start_WithoutWorkingDirectory_LeavesItToThePtyService(string? directory)
    {
        var launcher = new TerminalLauncher(_pty, new InMemorySettingsStore());

        launcher.Start(directory, PtySize.Default);

        Assert.Null(Assert.Single(_pty.Requests).Request.WorkingDirectory);
    }
}
