using System.Diagnostics;
using System.Text;

namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// The parts of <see cref="IProcessLauncher"/> that work the same on every platform, built on
/// <see cref="Process"/>. Each host provides how to find executables and how to open things.
/// </summary>
public abstract class ProcessLauncherBase : IProcessLauncher
{
    /// <inheritdoc />
    public abstract string? FindExecutable(string name);

    /// <inheritdoc />
    public abstract void Open(string target);

    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = CreateStartInfo(request);
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Couldn't start '{startInfo.FileName}'.");
        process.StandardInput.Close();

        // Read both streams at once, so a process that fills one pipe while we wait on the other can't block.
        var standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }

        return new ProcessResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

    /// <inheritdoc />
    public int Start(ProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var process = Process.Start(CreateStartInfo(request))
            ?? throw new InvalidOperationException($"Couldn't start '{request.FileName}'.");

        // Keep the Process until it exits, so the runtime still reaps it on Linux, then release it.
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => process.Dispose();
        return process.Id;
    }

    /// <summary>
    /// Creates the start info for a request, with the executable resolved to a full path.
    /// </summary>
    /// <exception cref="FileNotFoundException">The executable can't be found.</exception>
    protected ProcessStartInfo CreateStartInfo(ProcessRequest request)
    {
        var fileName = FindExecutable(request.FileName)
            ?? throw new FileNotFoundException($"Couldn't find the executable '{request.FileName}'.", request.FileName);

        var startInfo = new ProcessStartInfo(fileName, request.Arguments)
        {
            UseShellExecute = false,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
        };

        foreach (var (name, value) in request.Environment)
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

        return startInfo;
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It has already exited.
        }
    }
}
