using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Tests;

/// <summary>
/// A process launcher that finds executables on <c>PATH</c>, for tests that run real processes.
/// </summary>
internal sealed class TestProcessLauncher : ProcessLauncherBase
{
    private readonly ExecutableResolver _resolver = new(
        Environment.GetEnvironmentVariable("PATH"),
        OperatingSystem.IsWindows() ? [".exe"] : [""],
        File.Exists);

    public override string? FindExecutable(string name) => _resolver.Resolve(name);

    public override void Open(string target) => throw new NotSupportedException();
}
