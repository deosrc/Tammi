using System.Runtime.InteropServices;

namespace Deosrc.Tammi.Core.Installation;

/// <summary>
/// An operating system and architecture Tammi is released for, named by its .NET runtime identifier (e.g.
/// <c>linux-x64</c>).
/// </summary>
/// <param name="RuntimeIdentifier">The runtime identifier used in release archive names.</param>
public sealed record ReleaseTarget(string RuntimeIdentifier)
{
    /// <summary>
    /// Whether this is a Windows target.
    /// </summary>
    public bool IsWindows => RuntimeIdentifier.StartsWith("win-", StringComparison.Ordinal);

    /// <summary>
    /// The name of the app's executable in a release archive.
    /// </summary>
    public string ExecutableName => IsWindows ? "Deosrc.Tammi.Windows.exe" : "Deosrc.Tammi.Linux";

    /// <summary>
    /// The runtime identifiers that releases are built for.
    /// </summary>
    public static IReadOnlyList<string> Supported { get; } = ["linux-x64", "linux-arm64", "win-x64"];

    /// <summary>
    /// The target for the machine this is running on.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Releases aren't built for this OS and architecture.</exception>
    public static ReleaseTarget Current()
    {
        var os = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsLinux() ? "linux"
            : null;
        var architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null,
        };
        var identifier = $"{os}-{architecture}";
        if (os is null || architecture is null || !Supported.Contains(identifier))
        {
            throw new PlatformNotSupportedException(
                $"Tammi isn't released for {RuntimeInformation.OSDescription} on {RuntimeInformation.OSArchitecture}. " +
                $"It's released for: {string.Join(", ", Supported)}.");
        }

        return new ReleaseTarget(identifier);
    }
}
