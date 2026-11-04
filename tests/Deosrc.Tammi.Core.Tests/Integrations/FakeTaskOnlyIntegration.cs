using System.Diagnostics.CodeAnalysis;
using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Tests.Integrations;

/// <summary>
/// An integration that only provides tasks, e.g. JIRA.
/// </summary>
internal sealed class FakeTaskOnlyIntegration(string id) : ITaskIntegration
{
    public string Id { get; } = id;

    public string Name => "Tasks only";

    public Task<IReadOnlyList<TaskInfo>> GetAssignedTasksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TaskInfo>>([]);

    public Task<IReadOnlyList<TaskInfo>> SearchTasksAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TaskInfo>>([]);

    public bool TryParseTaskReference(string? text, [NotNullWhen(true)] out string? key)
    {
        key = null;
        return false;
    }

    public Task<TaskInfo?> GetTaskAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<TaskInfo?>(null);
}
