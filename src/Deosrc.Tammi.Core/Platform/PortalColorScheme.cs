using System.Text.RegularExpressions;

namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Reads the <c>org.freedesktop.appearance</c> <c>color-scheme</c> setting of the XDG desktop portal, as printed by
/// <c>gdbus</c>.
/// </summary>
public static partial class PortalColorScheme
{
    /// <summary>
    /// Finds whether the scheme is dark in <c>gdbus</c> output such as <c>(&lt;&lt;uint32 1&gt;&gt;,)</c>.
    /// </summary>
    /// <returns>
    /// <c>true</c> for 1 (prefer dark), <c>false</c> for 2 (prefer light), or <c>null</c> for 0 (no preference) or
    /// output that isn't a scheme.
    /// </returns>
    public static bool? ParseIsDark(string? output)
    {
        var match = output is null ? null : SchemeRegex().Match(output);
        return match is { Success: true } ? match.Groups[1].Value switch
        {
            "1" => true,
            "2" => false,
            _ => null,
        } : null;
    }

    [GeneratedRegex(@"uint32\s+(\d+)")]
    private static partial Regex SchemeRegex();
}
