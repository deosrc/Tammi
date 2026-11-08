namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// Identifies a task across all integrations, e.g. in a workspace's metadata.
/// </summary>
/// <param name="Source">The <see cref="IIntegration.Id"/> of the integration the task comes from.</param>
/// <param name="Key">The task's <see cref="TaskInfo.Key"/>.</param>
public sealed record TaskReference(string Source, string Key)
{
    /// <summary>
    /// The short form of a task's key, used in suggested names and on task cards: the key after its last <c>/</c>, so a
    /// GitHub issue <c>acme/api#42</c> gives <c>api#42</c>, and a JIRA issue <c>ABC-123</c> stays <c>ABC-123</c>.
    /// </summary>
    public static string ShortKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key[(key.LastIndexOf('/') + 1)..];
    }

    public override string ToString() => $"{Source}:{Key}";
}
