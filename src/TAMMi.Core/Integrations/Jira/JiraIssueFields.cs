namespace TAMMi.Core.Integrations.Jira;

/// <summary>
/// The issue fields TAMMi asks for: <see cref="Fields"/>.
/// </summary>
/// <param name="Summary">The issue's title.</param>
/// <param name="Description">The description in JIRA wiki markup, or <see langword="null"/> when it has none.</param>
internal sealed record JiraIssueFields(string? Summary, string? Description)
{
    /// <summary>
    /// The value of the <c>fields</c> parameter that asks for these fields.
    /// </summary>
    public const string Fields = "summary,description";
}
