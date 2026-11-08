using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Tests.Integrations;

/// <summary>
/// An integration with set Pull Requests, for testing what's done with them. Remote URLs on
/// <c>https://&lt;id&gt;.example/</c> are its repositories.
/// </summary>
internal sealed class FakePullRequestSource(string id) : IPullRequestIntegration
{
    private readonly List<string> _calls = [];

    public string Id { get; } = id;

    public string Name => $"Source {Id}";

    /// <summary>
    /// The Pull Requests it knows, by branch.
    /// </summary>
    public Dictionary<string, List<PullRequestInfo>> PullRequests { get; } = [];

    /// <summary>
    /// What every call throws instead of answering, if anything.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    /// Holds every call until it's completed, if set.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>
    /// The calls made so far, as <c>branch:url,url</c>. Calls can come from several threads at once.
    /// </summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    public PullRequestInfo Add(string branch, int number, PullRequestState state = PullRequestState.Open)
    {
        var pullRequest = new PullRequestInfo(
            Id, "acme/api", number, $"PR {number}", new Uri($"https://{Id}.example/acme/api/pull/{number}"), state);
        if (!PullRequests.TryGetValue(branch, out var list))
        {
            PullRequests[branch] = list = [];
        }

        list.Add(pullRequest);
        return pullRequest;
    }

    public async Task<IReadOnlyList<PullRequestInfo>> GetPullRequestsAsync(
        IReadOnlyList<string> remoteUrls,
        string branch,
        CancellationToken cancellationToken = default)
    {
        lock (_calls)
        {
            _calls.Add($"{branch}:{string.Join(',', remoteUrls)}");
        }

        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        if (Failure is { } failure)
        {
            throw failure;
        }

        return remoteUrls.Any(url => url.StartsWith($"https://{Id}.example/", StringComparison.Ordinal))
            ? [.. PullRequests.GetValueOrDefault(branch) ?? []]
            : [];
    }
}
