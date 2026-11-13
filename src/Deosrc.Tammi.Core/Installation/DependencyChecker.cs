using System.Diagnostics;

namespace Deosrc.Tammi.Core.Installation;

/// <summary>
/// Checks that what the installer needs is installed. Tammi checks for git itself when it starts, as git could be
/// removed after it's installed.
/// </summary>
public static class DependencyChecker
{
    /// <summary>
    /// The major version of the .NET runtime that releases need.
    /// </summary>
    public const int RequiredDotNetMajorVersion = 10;

    /// <summary>
    /// Finds problems with what's installed: the .NET runtime.
    /// </summary>
    public static IReadOnlyList<DependencyProblem> Check()
    {
        var problems = new List<DependencyProblem>();
        if (DotNetProblem() is { } dotnet)
        {
            problems.Add(dotnet);
        }

        return problems;
    }

    /// <summary>
    /// Finds a problem with the .NET runtime.
    /// </summary>
    public static DependencyProblem? DotNetProblem()
    {
        var output = Run("dotnet", "--list-runtimes");
        if (output is null)
        {
            return new DependencyProblem(
                $".NET {RequiredDotNetMajorVersion} runtime",
                "The 'dotnet' command wasn't found.",
                DotNetFix());
        }

        return HasRuntime(output, RequiredDotNetMajorVersion)
            ? null
            : new DependencyProblem(
                $".NET {RequiredDotNetMajorVersion} runtime",
                $"No .NET {RequiredDotNetMajorVersion} runtime is installed.",
                DotNetFix());
    }

    /// <summary>
    /// Whether the output of <c>dotnet --list-runtimes</c> lists the shared framework at a major version, e.g.
    /// <c>Microsoft.NETCore.App 10.0.1 [/usr/share/dotnet/shared/Microsoft.NETCore.App]</c>.
    /// </summary>
    internal static bool HasRuntime(string listRuntimesOutput, int majorVersion) =>
        listRuntimesOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Any(parts => parts.Length >= 2
                && parts[0] == "Microsoft.NETCore.App"
                && parts[1].StartsWith(majorVersion + ".", StringComparison.Ordinal));

    private static string DotNetFix() => OperatingSystem.IsWindows()
        ? "Install the .NET 10 runtime: run 'winget install Microsoft.DotNet.Runtime.10', or download it from https://dotnet.microsoft.com/download/dotnet/10.0"
        : "Install the .NET 10 runtime with your package manager (e.g. 'sudo apt install dotnet-runtime-10.0' on Debian or Ubuntu), " +
          "or download it from https://dotnet.microsoft.com/download/dotnet/10.0";

    /// <returns>The command's output, or <see langword="null"/> if it couldn't be run or failed.</returns>
    private static string? Run(string command, string arguments)
    {
        try
        {
            var info = new ProcessStartInfo(command, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var process = Process.Start(info);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync();
            process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(TimeSpan.FromSeconds(15)))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            return process.ExitCode == 0 ? output.GetAwaiter().GetResult() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }
}
