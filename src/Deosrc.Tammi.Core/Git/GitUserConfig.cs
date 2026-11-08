namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Reads and changes the user's global git config with the git CLI.
/// </summary>
/// <param name="git">Runs git.</param>
/// <param name="workingDirectory">
/// Where git runs. It defaults to the user's home folder, which is outside any repository, so a repository's own config
/// can't affect what's read.
/// </param>
public sealed class GitUserConfig(IGitCli git, string? workingDirectory = null) : IGitUserConfig
{
    /// <summary>
    /// The config key of the author's name.
    /// </summary>
    public const string NameKey = "user.name";

    /// <summary>
    /// The config key of the author's email address.
    /// </summary>
    public const string EmailKey = "user.email";

    // git config exits with 1 when --get finds no value, and with 5 when --unset finds nothing to unset.
    private const int NotFoundExitCode = 1;
    private const int NothingToUnsetExitCode = 5;

    private readonly string _workingDirectory = workingDirectory
        ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <inheritdoc />
    public async Task<GitIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
    {
        string? name = await GetAsync(NameKey, cancellationToken).ConfigureAwait(false);
        string? email = await GetAsync(EmailKey, cancellationToken).ConfigureAwait(false);
        return new GitIdentity(name, email);
    }

    /// <inheritdoc />
    public Task SetNameAsync(string? name, CancellationToken cancellationToken = default) =>
        SetAsync(NameKey, name, cancellationToken);

    /// <inheritdoc />
    public Task SetEmailAsync(string? email, CancellationToken cancellationToken = default) =>
        SetAsync(EmailKey, email, cancellationToken);

    private async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        GitCommandResult result = await RunAsync(["config", "--global", "--get", key], cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode == NotFoundExitCode)
        {
            return null;
        }

        ThrowIfFailed(result);
        string value = result.StandardOutput.TrimEnd('\r', '\n');
        return value.Length == 0 ? null : value;
    }

    private async Task SetAsync(string key, string? value, CancellationToken cancellationToken)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            GitCommandResult unset = await RunAsync(["config", "--global", "--unset", key], cancellationToken)
                .ConfigureAwait(false);
            if (unset.ExitCode != NothingToUnsetExitCode)
            {
                ThrowIfFailed(unset);
            }

            return;
        }

        ThrowIfFailed(await RunAsync(["config", "--global", key, value], cancellationToken).ConfigureAwait(false));
    }

    private Task<GitCommandResult> RunAsync(string[] arguments, CancellationToken cancellationToken) =>
        git.RunAsync(_workingDirectory, arguments, new GitCommandOptions { ThrowOnError = false }, cancellationToken);

    private static void ThrowIfFailed(GitCommandResult result)
    {
        if (!result.Succeeded)
        {
            throw new GitCommandException(result);
        }
    }
}
