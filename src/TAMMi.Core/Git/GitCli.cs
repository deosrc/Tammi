using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace TAMMi.Core.Git;

/// <summary>
/// Runs the <c>git</c> CLI as a child process.
/// </summary>
public sealed partial class GitCli(GitCliOptions? options = null) : IGitCli
{
    // How long a cancelled command waits for its killed git process to end.
    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(5);

    private readonly GitCliOptions _options = options ?? new GitCliOptions();

    /// <inheritdoc />
    public async Task<GitInstallation?> DetectAsync(CancellationToken cancellationToken = default)
    {
        GitCommandResult result;
        try
        {
            result = await RunAsync(
                Directory.GetCurrentDirectory(),
                ["--version"],
                new GitCommandOptions { ThrowOnError = false },
                cancellationToken).ConfigureAwait(false);
        }
        catch (GitNotInstalledException)
        {
            return null;
        }

        if (!result.Succeeded)
        {
            return null;
        }

        // e.g. "git version 2.43.0" or "git version 2.47.1.windows.2"
        Match match = VersionPattern().Match(result.StandardOutput);
        if (!match.Success)
        {
            return null;
        }

        return new GitInstallation(
            _options.ExecutablePath,
            Version.Parse(match.Groups["number"].Value),
            match.Groups["full"].Value);
    }

    /// <inheritdoc />
    public async Task<GitCommandResult> RunAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        GitCommandOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new GitCommandOptions();
        string[] args = [.. arguments];

        var startInfo = new ProcessStartInfo(_options.ExecutablePath)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // TAMMi is a GUI app: a git process waiting for a terminal prompt would hang with nobody to answer it.
        // Credential helpers with their own UI still work.
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach ((string name, string? value) in _options.Environment)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new GitNotInstalledException(_options.ExecutablePath, ex);
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            if (options.StandardInput is not null)
            {
                await process.StandardInput.WriteAsync(options.StandardInput.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
            }

            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await KillAsync(process).ConfigureAwait(false);
            throw;
        }

        var result = new GitCommandResult(
            args,
            workingDirectory,
            process.ExitCode,
            await stdout.ConfigureAwait(false),
            await stderr.ConfigureAwait(false));

        if (options.ThrowOnError && !result.Succeeded)
        {
            throw new GitCommandException(result);
        }

        return result;
    }

    private static async Task KillAsync(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
            return;
        }

        // Kill doesn't wait for the process to end. Until it has, Windows won't let the caller delete or move the
        // working directory, e.g. to clean up after cancelling. The wait is bounded so a process that won't die can't
        // hang the caller.
        try
        {
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(KillTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Give up waiting: the caller asked to cancel.
        }
    }

    [GeneratedRegex(@"git version (?<full>(?<number>\d+\.\d+(\.\d+)?)\S*)")]
    private static partial Regex VersionPattern();
}
