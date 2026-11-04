namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// Why one Task Source couldn't be searched. The other sources' results are still shown.
/// </summary>
/// <param name="Source">The <see cref="IIntegration.Id"/> of the source.</param>
/// <param name="SourceName">The source's <see cref="IIntegration.Name"/>.</param>
/// <param name="Message">What went wrong.</param>
/// <param name="IsAuthentication">Whether the source needs authenticating again in Settings.</param>
public sealed record TaskSourceError(string Source, string SourceName, string Message, bool IsAuthentication);
