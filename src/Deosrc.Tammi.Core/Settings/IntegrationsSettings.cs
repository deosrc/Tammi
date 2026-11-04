namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// The Integrations section of the Settings screen: the integrations the user has connected. Their secrets are kept
/// in the credential store, never here.
/// </summary>
public sealed record IntegrationsSettings
{
    /// <summary>
    /// The connected GitHub integrations, in the order they were added.
    /// </summary>
    public IReadOnlyList<GitHubIntegrationSettings> GitHub { get; init; } = [];

    /// <summary>
    /// The connected JIRA sites, in the order they were added.
    /// </summary>
    public IReadOnlyList<JiraIntegrationSettings> Jira { get; init; } = [];

    /// <summary>
    /// Drops integrations that are missing values Tammi needs, or that have the same ID as one before them.
    /// </summary>
    public IntegrationsSettings Normalize()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        return this with
        {
            GitHub = [.. (GitHub ?? []).Where(github => github?.IsValid() == true && ids.Add(github.Id))],
            Jira = [.. (Jira ?? []).Where(jira => jira?.IsValid() == true && ids.Add(jira.Id))],
        };
    }

    // Compare the lists' contents rather than the list instances, so normalized settings equal the originals.
    public bool Equals(IntegrationsSettings? other) =>
        other is not null
        && (GitHub ?? []).SequenceEqual(other.GitHub ?? [])
        && (Jira ?? []).SequenceEqual(other.Jira ?? []);

    public override int GetHashCode() => HashCode.Combine(GitHub?.Count ?? 0, Jira?.Count ?? 0);
}
