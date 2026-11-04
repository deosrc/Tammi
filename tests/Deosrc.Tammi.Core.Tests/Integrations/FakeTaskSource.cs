using System.Diagnostics.CodeAnalysis;
using Tammi.Core.Integrations;

namespace Tammi.Core.Tests.Integrations;

/// <summary>
/// A Task Source with set answers, for testing what's done with them. Keys of the form <c>&lt;prefix&gt;-n</c> and
/// URLs on <c>https://&lt;id&gt;.example/</c> are its references.
/// </summary>
internal sealed class FakeTaskSource(string id, string keyPrefix) : ITaskIntegration
{
    public string Id { get; } = id;

    public string Name => $"Source {Id}";

    /// <summary>
    /// The tasks it knows. They're all assigned to the user, and searching finds those whose title holds the text.
    /// </summary>
    public List<TaskInfo> Tasks { get; } = [];

    /// <summary>
    /// What every call throws instead of answering, if anything.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    /// The calls made so far. Calls can come from several threads at once.
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

    /// <summary>
    /// Holds every call until it completes, if set.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    private readonly List<string> _calls = [];

    public TaskInfo Add(int number, string title, DateTimeOffset? updated = null)
    {
        string key = $"{keyPrefix}-{number}";
        var task = new TaskInfo(Id, key, title, new Uri($"https://{Id}.example/{key}")) { Updated = updated };
        Tasks.Add(task);
        return task;
    }

    public Task<IReadOnlyList<TaskInfo>> GetAssignedTasksAsync(CancellationToken cancellationToken = default) =>
        Answer("assigned", () => [.. Tasks]);

    public Task<IReadOnlyList<TaskInfo>> SearchTasksAsync(string text, CancellationToken cancellationToken = default) =>
        Answer($"search:{text}", () => [.. Tasks.Where(task => task.Title.Contains(text, StringComparison.Ordinal))]);

    public bool TryParseTaskReference(string? text, [NotNullWhen(true)] out string? key)
    {
        text = text?.Trim() ?? "";
        string urlPrefix = $"https://{Id}.example/";
        if (text.StartsWith(urlPrefix, StringComparison.Ordinal))
        {
            text = text[urlPrefix.Length..];
        }

        key = text.StartsWith(keyPrefix + "-", StringComparison.Ordinal) ? text : null;
        return key is not null;
    }

    public async Task<TaskInfo?> GetTaskAsync(string key, CancellationToken cancellationToken = default) =>
        (await Answer($"get:{key}", () => [.. Tasks.Where(task => task.Key == key)], cancellationToken))
            .SingleOrDefault();

    private async Task<IReadOnlyList<TaskInfo>> Answer(
        string call,
        Func<IReadOnlyList<TaskInfo>> answer,
        CancellationToken cancellationToken = default)
    {
        lock (_calls)
        {
            _calls.Add(call);
        }

        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        return Failure is null ? answer() : throw Failure;
    }
}
