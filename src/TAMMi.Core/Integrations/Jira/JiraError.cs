namespace TAMMi.Core.Integrations.Jira;

/// <summary>
/// An error response from JIRA's REST API, e.g. for an invalid JQL query.
/// </summary>
/// <param name="ErrorMessages">Messages about the request as a whole.</param>
/// <param name="Errors">Messages about single fields or parameters, by name.</param>
/// <param name="Message">A single message, as some Atlassian Cloud services return.</param>
internal sealed record JiraError(
    IReadOnlyList<string>? ErrorMessages,
    IReadOnlyDictionary<string, string>? Errors,
    string? Message)
{
    /// <summary>
    /// All the messages, or <see langword="null"/> if there are none.
    /// </summary>
    public string? Text
    {
        get
        {
            string[] messages =
            [
                .. ErrorMessages ?? [],
                .. (Errors ?? new Dictionary<string, string>()).Values,
                .. Message is null ? [] : new[] { Message },
            ];
            messages = [.. messages.Where(message => !string.IsNullOrWhiteSpace(message)).Select(m => m.Trim())];
            return messages.Length == 0 ? null : string.Join(" ", messages);
        }
    }
}
