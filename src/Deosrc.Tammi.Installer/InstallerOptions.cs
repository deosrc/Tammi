using Deosrc.Tammi.Installation;

namespace Deosrc.Tammi.Installer;

/// <summary>
/// The installer's command line.
/// </summary>
internal sealed record InstallerOptions
{
    public const string Usage = """
        Installs Tammi for the current user, without needing admin rights.

        Usage: tammi-installer [options]

        Options:
          --prerelease        Install the newest release even if it's a pre-release.
          --tag <tag>         Install a particular release, e.g. v0.2.0.
          --self-contained    Install the build that bundles the .NET runtime, so .NET needn't be installed.
          --help              Show this help.
        """;

    public bool IncludePreReleases { get; init; }

    public string? Tag { get; init; }

    public bool SelfContained { get; init; }

    public bool ShowHelp { get; init; }

    /// <returns>The options, or <see langword="null"/> and an error message if the arguments are invalid.</returns>
    public static InstallerOptions? Parse(IReadOnlyList<string> args, out string? error)
    {
        var options = new InstallerOptions();
        error = null;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--prerelease":
                    options = options with { IncludePreReleases = true };
                    break;
                case "--self-contained":
                    options = options with { SelfContained = true };
                    break;
                case "--help" or "-h" or "-?":
                    options = options with { ShowHelp = true };
                    break;
                case "--tag":
                    if (i + 1 >= args.Count)
                    {
                        error = "--tag needs a value.";
                        return null;
                    }

                    options = options with { Tag = args[++i] };
                    break;
                default:
                    error = $"Unknown option '{args[i]}'.";
                    return null;
            }
        }

        return options;
    }

    public ReleaseFlavour Flavour => SelfContained ? ReleaseFlavour.SelfContained : ReleaseFlavour.FrameworkDependent;
}
