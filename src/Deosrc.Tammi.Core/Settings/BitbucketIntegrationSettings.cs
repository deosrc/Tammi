using System.Text.Json.Serialization;
using Deosrc.Tammi.Core.Integrations.Bitbucket;

namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// The Bitbucket Cloud integration the user has connected. Its API token is kept in the credential store, not here.
/// </summary>
public sealed record BitbucketIntegrationSettings
{
    /// <summary>
    /// The integration's <see cref="Integrations.IIntegration.Id"/>. There's only one Bitbucket Cloud, so it's always
    /// <see cref="BitbucketIntegrationOptions.DefaultId"/>.
    /// </summary>
    public string Id { get; init; } = BitbucketIntegrationOptions.DefaultId;

    /// <summary>
    /// The email address of the user's Atlassian account, which Bitbucket Cloud needs with the API token.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// The name shown to the user.
    /// </summary>
    [JsonIgnore]
    public string Name => "Bitbucket";

    /// <summary>
    /// The settings for a new integration with the given account.
    /// </summary>
    public static BitbucketIntegrationSettings For(string? email) =>
        new() { Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim() };

    /// <summary>
    /// Whether the settings can be used: they have an ID and an email address.
    /// </summary>
    public bool IsValid() => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(Email);

    /// <summary>
    /// The options a <see cref="BitbucketIntegration"/> is created with.
    /// </summary>
    public BitbucketIntegrationOptions ToOptions() => new() { Id = Id, Name = Name, Email = Email!.Trim() };
}
