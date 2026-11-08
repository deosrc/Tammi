namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Finds the icon of the application an executable belongs to, e.g. to show a tool with its own icon.
/// </summary>
public interface IApplicationIconProvider
{
    /// <summary>
    /// Finds an executable's application icon.
    /// </summary>
    /// <param name="executablePath">The executable's full path.</param>
    /// <returns>The icon as a <c>data:</c> URI, or <c>null</c> if it has none that can be found.</returns>
    Task<string?> GetIconAsync(string executablePath, CancellationToken cancellationToken = default);
}
