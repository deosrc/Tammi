namespace TAMMi.Core.Git;

/// <summary>
/// Configuration for <see cref="GitCli"/>.
/// </summary>
public sealed record GitCliOptions
{
    /// <summary>
    /// The git executable: a name looked up on the PATH, or a full path.
    /// </summary>
    public string ExecutablePath { get; init; } = "git";

    /// <summary>
    /// Environment variables set for every git process, on top of the inherited environment.
    /// A <see langword="null"/> value removes the variable.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();
}
