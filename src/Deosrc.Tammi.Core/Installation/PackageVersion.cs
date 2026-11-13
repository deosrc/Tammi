using System.Globalization;

namespace Deosrc.Tammi.Core.Installation;

/// <summary>
/// A semantic version, e.g. <c>1.2.3</c> or <c>1.2.3-beta.1</c>. Build metadata (after <c>+</c>) is dropped.
/// </summary>
public sealed class PackageVersion : IComparable<PackageVersion>, IEquatable<PackageVersion>
{
    private readonly int _major;
    private readonly int _minor;
    private readonly int _patch;
    private readonly string[] _preRelease;

    private PackageVersion(int major, int minor, int patch, string[] preRelease)
    {
        _major = major;
        _minor = minor;
        _patch = patch;
        _preRelease = preRelease;
    }

    /// <summary>
    /// Whether this has a pre-release suffix, e.g. <c>-beta.1</c>.
    /// </summary>
    public bool IsPreRelease => _preRelease.Length > 0;

    /// <summary>
    /// Reads a version, with or without a leading <c>v</c>.
    /// </summary>
    /// <returns>Whether <paramref name="text"/> was a valid version.</returns>
    public static bool TryParse(string? text, out PackageVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.Trim();
        if (span.StartsWith('v') || span.StartsWith('V'))
        {
            span = span[1..];
        }

        var plus = span.IndexOf('+');
        if (plus >= 0)
        {
            span = span[..plus];
        }

        string[] preRelease = [];
        var dash = span.IndexOf('-');
        if (dash >= 0)
        {
            preRelease = span[(dash + 1)..].Split('.');
            if (preRelease.Any(string.IsNullOrEmpty))
            {
                return false;
            }

            span = span[..dash];
        }

        var parts = span.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        version = new PackageVersion(major, minor, patch, preRelease);
        return true;
    }

    /// <summary>
    /// Reads a version.
    /// </summary>
    /// <exception cref="FormatException"><paramref name="text"/> isn't a valid version.</exception>
    public static PackageVersion Parse(string text) =>
        TryParse(text, out var version) ? version : throw new FormatException($"'{text}' isn't a valid version.");

    /// <inheritdoc />
    public int CompareTo(PackageVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = _major.CompareTo(other._major);
        if (result == 0)
        {
            result = _minor.CompareTo(other._minor);
        }

        if (result == 0)
        {
            result = _patch.CompareTo(other._patch);
        }

        if (result != 0)
        {
            return result;
        }

        // A version with a pre-release suffix comes before the same version without one.
        if (_preRelease.Length == 0 || other._preRelease.Length == 0)
        {
            return other._preRelease.Length.CompareTo(_preRelease.Length);
        }

        for (var i = 0; i < Math.Min(_preRelease.Length, other._preRelease.Length); i++)
        {
            var a = _preRelease[i];
            var b = other._preRelease[i];
            var aNumeric = int.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var aNumber);
            var bNumeric = int.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var bNumber);
            // Numeric identifiers come before text ones.
            var compared = aNumeric && bNumeric ? aNumber.CompareTo(bNumber)
                : aNumeric ? -1
                : bNumeric ? 1
                : string.CompareOrdinal(a, b);
            if (compared != 0)
            {
                return compared;
            }
        }

        return _preRelease.Length.CompareTo(other._preRelease.Length);
    }

    /// <inheritdoc />
    public bool Equals(PackageVersion? other) => other is not null && CompareTo(other) == 0;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PackageVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_major, _minor, _patch, string.Join('.', _preRelease));

    /// <inheritdoc />
    public override string ToString() =>
        $"{_major}.{_minor}.{_patch}{(IsPreRelease ? "-" + string.Join('.', _preRelease) : "")}";

    public static bool operator >(PackageVersion left, PackageVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(PackageVersion left, PackageVersion right) => left.CompareTo(right) < 0;
}
