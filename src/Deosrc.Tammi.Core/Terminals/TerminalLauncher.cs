using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Terminals;

/// <summary>
/// Starts terminals: the terminal shell from the settings, in the embedded terminal's pseudoterminals.
/// </summary>
public sealed class TerminalLauncher(IPtyService ptyService, ISettingsStore settingsStore)
{
    /// <summary>
    /// The shell new terminals run: the Terminal shell setting, or the platform's default shell when that isn't set.
    /// </summary>
    /// <returns>A request to start the shell, without a working directory.</returns>
    public ProcessRequest GetShell() =>
        settingsStore.Current.Tools is { TerminalShell: { } shell } tools
            ? new ProcessRequest(shell, tools.TerminalShellArguments)
            : ptyService.GetDefaultShell();

    /// <summary>
    /// Starts a terminal.
    /// </summary>
    /// <param name="workingDirectory">The folder to start it in, or <c>null</c> for the user's home folder.</param>
    /// <param name="size">The terminal's starting size. The terminal resizes it to fit once it's shown.</param>
    /// <returns>The session. Dispose it to end the shell.</returns>
    /// <exception cref="FileNotFoundException">The shell can't be found.</exception>
    /// <exception cref="DirectoryNotFoundException">The working directory doesn't exist.</exception>
    public IPtySession Start(string? workingDirectory, PtySize size) =>
        ptyService.Start(
            GetShell() with { WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? null : workingDirectory },
            size);
}
