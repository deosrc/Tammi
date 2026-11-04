namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// A connection to an external service, such as GitHub or JIRA. An integration provides tasks
/// (<see cref="ITaskIntegration"/>), repositories (<see cref="IRepositoryIntegration"/>) or both.
/// </summary>
/// <remarks>
/// Tammi only reads from integrations. Credentials are kept in the platform secret store through
/// <see cref="IntegrationCredentials"/>, never in settings.
/// </remarks>
public interface IIntegration
{
    /// <summary>
    /// Identifies this configured integration. It is stable across restarts and unique among integrations, because
    /// the user can connect more than one of a kind (e.g. two JIRA sites). Tasks and repositories record it as their
    /// source.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The name shown to the user, e.g. "GitHub" or "JIRA (acme.atlassian.net)".
    /// </summary>
    string Name { get; }
}
