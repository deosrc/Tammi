namespace TAMMi.Core.Integrations;

/// <summary>
/// The result of the last connection check of an integration, shown in Settings.
/// </summary>
/// <param name="State">Whether the integration is connected.</param>
/// <param name="Account">
/// The account the credentials belong to, as the service names it, when <paramref name="State"/> is
/// <see cref="IntegrationConnectionState.Connected"/>.
/// </param>
/// <param name="Message">Why the check failed, when it did.</param>
public sealed record IntegrationStatus(
    IntegrationConnectionState State,
    string? Account = null,
    string? Message = null)
{
    /// <summary>
    /// The status of an integration that hasn't been checked yet.
    /// </summary>
    public static IntegrationStatus NotChecked { get; } = new(IntegrationConnectionState.NotChecked);

    /// <summary>
    /// The status while a check runs.
    /// </summary>
    public static IntegrationStatus Checking { get; } = new(IntegrationConnectionState.Checking);
}
