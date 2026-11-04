namespace TAMMi.Core.Integrations.Jira;

/// <summary>
/// What a JIRA Cloud site says about itself at <c>_edge/tenant_info</c>, which needs no authentication.
/// </summary>
/// <param name="CloudId">The site's ID, which addresses it through <c>api.atlassian.com</c>.</param>
internal sealed record JiraTenantInfo(string? CloudId);
