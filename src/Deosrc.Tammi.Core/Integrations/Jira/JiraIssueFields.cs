using System.Globalization;

namespace Deosrc.Tammi.Core.Integrations.Jira;

/// <summary>
/// The issue fields Tammi asks for: <see cref="Fields"/>.
/// </summary>
/// <param name="Summary">The issue's title.</param>
/// <param name="Description">The description in JIRA wiki markup, or <see langword="null"/> when it has none.</param>
/// <param name="Updated">When the issue last changed, as JIRA writes it, e.g. <c>2026-11-03T12:00:00.000+0000</c>.</param>
/// <param name="Status">The issue's status, which can be one the site defines.</param>
internal sealed record JiraIssueFields(
    string? Summary,
    string? Description,
    string? Updated = null,
    JiraStatus? Status = null)
{
    /// <summary>
    /// The value of the <c>fields</c> parameter that asks for these fields.
    /// </summary>
    public const string Fields = "summary,description,updated,status";

    /// <summary>
    /// <see cref="Updated"/> as a time, or <see langword="null"/> if it's missing or can't be read.
    /// </summary>
    public DateTimeOffset? UpdatedTime
    {
        get
        {
            if (string.IsNullOrEmpty(Updated))
            {
                return null;
            }

            // JIRA writes the offset without a colon (+0000), which .NET doesn't read.
            string text = Updated.Length > 5 && Updated[^5] is '+' or '-'
                ? Updated.Insert(Updated.Length - 2, ":")
                : Updated;
            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var updated)
                ? updated
                : null;
        }
    }
}
