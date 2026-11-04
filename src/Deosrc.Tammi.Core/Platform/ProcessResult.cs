namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// The outcome of a process run with <see cref="IProcessLauncher.RunAsync"/>.
/// </summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>
    /// Whether the process exited with code 0.
    /// </summary>
    public bool Succeeded => ExitCode == 0;
}
