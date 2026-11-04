namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// Identifies a task across all integrations, e.g. in a workspace's metadata.
/// </summary>
/// <param name="Source">The <see cref="IIntegration.Id"/> of the integration the task comes from.</param>
/// <param name="Key">The task's <see cref="TaskInfo.Key"/>.</param>
public sealed record TaskReference(string Source, string Key)
{
    public override string ToString() => $"{Source}:{Key}";
}
