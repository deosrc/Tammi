using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Runs the <c>git</c> CLI as a child process.
/// </summary>
/// <param name="options">Which git to run, and how.</param>
/// <param name="logger">Logs the commands that are run (at Debug) and the ones that fail.</param>
public sealed partial class GitCli(GitCliOptions? options = null, ILogger<GitCli>? logger = null) : IGitCli
{
    private readonly GitCliOptions _options = options ?? new GitCliOptions();
    private readonly ILogger _logger = logger ?? NullLogger<GitCli>.Instance;

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
        catch (GitNotInstalledException ex)
        {
            _logger.LogError("git isn't installed: {Message}", ex.Message);
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

        // Tammi is a GUI app: a git process waiting for a terminal prompt would hang with nobody to answer it.
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
        long started = Stopwatch.GetTimestamp();
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new GitNotInstalledException(_options.ExecutablePath, ex);
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> stderr = ReadErrorAsync(process.StandardError, options.OnErrorLine);

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
            Kill(process);
            throw;
        }

        var result = new GitCommandResult(
            args,
            workingDirectory,
            process.ExitCode,
            await stdout.ConfigureAwait(false),
            await stderr.ConfigureAwait(false));

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "git {Arguments} in {WorkingDirectory} exited with {ExitCode} after {Elapsed:0} ms.",
                Redact(args),
                workingDirectory,
                result.ExitCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        if (options.ThrowOnError && !result.Succeeded)
        {
            _logger.LogWarning(
                "git {Arguments} in {WorkingDirectory} failed with exit code {ExitCode}: {Error}",
                Redact(args),
                workingDirectory,
                result.ExitCode,
                result.StandardError.Trim());
            throw new GitCommandException(result);
        }

        return result;
    }

    // Reads standard error to the end, reporting each line (a progress line ends with a carriage return) as it arrives.
    private static async Task<string> ReadErrorAsync(StreamReader reader, Action<string>? onLine)
    {
        if (onLine is null)
        {
            return await reader.ReadToEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        var all = new StringBuilder();
        var line = new StringBuilder();
        var buffer = new char[1024];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false)) > 0)
        {
            all.Append(buffer, 0, read);
            foreach (char c in buffer.AsSpan(0, read))
            {
                if (c is '\r' or '\n')
                {
                    Report(line, onLine);
                }
                else
                {
                    line.Append(c);
                }
            }
        }

        Report(line, onLine);
        return all.ToString();
    }

    private static void Report(StringBuilder line, Action<string> onLine)
    {
        if (line.Length == 0)
        {
            return;
        }

        string text = line.ToString();
        line.Clear();
        try
        {
            onLine(text);
        }
        catch (Exception)
        {
            // A failing listener mustn't stop git's output being read.
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    /// <summary>
    /// Joins the arguments for the log, hiding any user name or password in a URL (e.g. a remote's).
    /// </summary>
    internal static string Redact(IEnumerable<string> arguments) =>
        string.Join(' ', arguments.Select(argument => UrlUserInfoPattern().Replace(argument, "://***@")));

    [GeneratedRegex(@"://[^/@\s]+@")]
    private static partial Regex UrlUserInfoPattern();

    [GeneratedRegex(@"git version (?<full>(?<number>\d+\.\d+(\.\d+)?)\S*)")]
    private static partial Regex VersionPattern();
}
