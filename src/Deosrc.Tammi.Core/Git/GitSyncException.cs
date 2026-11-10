namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A fetch, pull or push failed. The message is written to be shown to the user, and <see cref="Details"/> has git's
/// own output.
/// </summary>
public sealed class GitSyncException : GitException
{
    internal GitSyncException(GitSyncOperation operation, GitSyncFailure failure, string message, string details, Exception? inner = null)
        : base(message, inner)
    {
        Operation = operation;
        Failure = failure;
        Details = details;
    }

    /// <summary>
    /// The operation that failed.
    /// </summary>
    public GitSyncOperation Operation { get; }

    /// <summary>
    /// The kind of failure.
    /// </summary>
    public GitSyncFailure Failure { get; }

    /// <summary>
    /// What git printed, without progress lines. Empty when there's nothing more than <see cref="Exception.Message"/>.
    /// </summary>
    public string Details { get; }
}
