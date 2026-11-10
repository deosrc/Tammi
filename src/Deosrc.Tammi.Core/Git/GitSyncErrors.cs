namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Turns the output of a failed fetch, pull or push into an error that says what went wrong and what to do.
/// </summary>
internal static class GitSyncErrors
{
    public static GitSyncException Classify(GitSyncOperation operation, GitCommandResult result)
    {
        string details = Details(result);
        string text = details.ToLowerInvariant();
        string verb = operation.ToString().ToLowerInvariant();

        (GitSyncFailure failure, string message) = Match(operation, text, verb);
        if (failure == GitSyncFailure.Other)
        {
            message = details.Length == 0
                ? $"The {verb} failed (git exited with code {result.ExitCode})."
                : $"The {verb} failed. {FirstMessageLine(details)}";
        }

        return new GitSyncException(operation, failure, message, details);
    }

    private static (GitSyncFailure, string) Match(GitSyncOperation operation, string text, string verb)
    {
        if (Contains(text, "automatic merge failed", "merge conflict in", "could not apply", "fix conflicts")
            || (text.Contains("conflict", StringComparison.Ordinal) && operation == GitSyncOperation.Pull))
        {
            return (
                GitSyncFailure.Conflict,
                "The pull stopped because of conflicts. Resolve them in the working changes, then commit (or abort the merge in another tool).");
        }

        if (Contains(text, "would be overwritten by merge", "would be overwritten by checkout", "your local changes to the following files would be overwritten", "please commit your changes or stash them"))
        {
            return (
                GitSyncFailure.LocalChanges,
                "The pull would overwrite uncommitted changes. Commit or stash them, then pull again.");
        }

        if (Contains(text, "divergent branches", "not possible to fast-forward", "need to specify how to reconcile"))
        {
            return (
                GitSyncFailure.Diverged,
                "The branch and its upstream have both changed, and your git config says to only fast-forward. Merge or rebase in another tool.");
        }

        if (Contains(text, "authentication failed", "could not read username", "could not read password", "permission denied", "invalid username or password", "terminal prompts disabled", "http basic: access denied", "access denied", "publickey", "returned error: 401", "returned error: 403", "remote: invalid credentials", "repository not found"))
        {
            return (
                GitSyncFailure.Authentication,
                $"The {verb} was refused: authentication failed, or you don't have access to this repository. Check that your credentials (credential helper or SSH key) are set up and allowed to access the remote.");
        }

        if (Contains(text, "could not resolve host", "unable to access", "could not read from remote repository", "connection timed out", "connection refused", "network is unreachable", "does not appear to be a git repository", "no route to host", "ssl certificate"))
        {
            return (
                GitSyncFailure.Network,
                $"The {verb} couldn't reach the remote. Check your network connection and the remote's address.");
        }

        if (operation == GitSyncOperation.Push
            && Contains(text, "non-fast-forward", "[rejected]", "failed to push some refs", "fetch first", "tip of your current branch is behind"))
        {
            return (
                GitSyncFailure.PushRejected,
                "The push was rejected because the remote has commits you don't have. Pull first, then push again.");
        }

        if (Contains(text, "no tracking information", "no upstream", "has no upstream branch", "no remote repository specified"))
        {
            return (
                GitSyncFailure.NoUpstream,
                $"The branch has no upstream to {verb} from. Push it first to set one up.");
        }

        if (Contains(text, "no remote", "no such remote"))
        {
            return (GitSyncFailure.NoRemote, "This repository has no remote to sync with.");
        }

        return (GitSyncFailure.Other, "");
    }

    private static bool Contains(string text, params string[] needles) =>
        needles.Any(needle => text.Contains(needle, StringComparison.Ordinal));

    // Without progress lines ("Receiving objects: 40% …"), which end in a percentage or "done." and bury the error.
    private static string Details(GitCommandResult result)
    {
        // A conflicted merge reports on standard output, most other failures on standard error.
        string text = $"{result.StandardError}\n{result.StandardOutput}";
        IEnumerable<string> lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !IsProgress(line));
        return string.Join('\n', lines);
    }

    private static bool IsProgress(string line) =>
        (line.StartsWith("remote: Counting", StringComparison.Ordinal)
            || line.StartsWith("remote: Compressing", StringComparison.Ordinal)
            || line.StartsWith("Receiving objects", StringComparison.Ordinal)
            || line.StartsWith("Resolving deltas", StringComparison.Ordinal)
            || line.StartsWith("Counting objects", StringComparison.Ordinal)
            || line.StartsWith("Compressing objects", StringComparison.Ordinal)
            || line.StartsWith("Writing objects", StringComparison.Ordinal)
            || line.StartsWith("Unpacking objects", StringComparison.Ordinal)
            || line.StartsWith("Enumerating objects", StringComparison.Ordinal))
        && !line.Contains("fatal", StringComparison.Ordinal);

    private static string FirstMessageLine(string details)
    {
        string[] lines = details.Split('\n');
        string line = lines.FirstOrDefault(l => l.StartsWith("fatal:", StringComparison.Ordinal) || l.StartsWith("error:", StringComparison.Ordinal))
            ?? lines[0];
        return line.StartsWith("fatal: ", StringComparison.Ordinal) ? line[7..]
            : line.StartsWith("error: ", StringComparison.Ordinal) ? line[7..]
            : line;
    }
}
