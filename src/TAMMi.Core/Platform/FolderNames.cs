using System.Text;

namespace TAMMi.Core.Platform;

/// <summary>
/// Rules for the name of a single folder (not a path) that's valid on every platform TAMMi runs on.
/// </summary>
public static class FolderNames
{
    /// <summary>
    /// The longest folder name, in UTF-8 bytes, that every platform's file systems accept. Linux file systems limit a
    /// name to 255 bytes, and NTFS to 255 UTF-16 code units, which is never fewer characters.
    /// </summary>
    public const int MaxLength = 255;

    /// <summary>
    /// Characters that Windows doesn't allow in a file or folder name. <c>/</c> separates folders everywhere.
    /// </summary>
    public static IReadOnlyList<char> InvalidCharacters { get; } = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    // Device names that Windows reserves, on their own or followed by an extension (e.g. "con.txt").
    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Whether <paramref name="name"/> is a single folder name that's valid on every platform: not blank, not
    /// <c>.</c> or <c>..</c>, without surrounding spaces or a trailing <c>.</c>, and without
    /// <see cref="InvalidCharacters"/> or control characters. A name that's a path (e.g. <c>../other</c>) isn't valid.
    /// </summary>
    /// <remarks>
    /// It doesn't check <see cref="IsReservedOnWindows"/> or <see cref="MaxLength"/>.
    /// </remarks>
    public static bool IsValid(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name is not ("." or "..")
        && name == name.Trim()
        && !name.EndsWith('.')
        && !ContainsInvalidCharacter(name);

    /// <summary>
    /// Whether <paramref name="name"/> holds one of <see cref="InvalidCharacters"/>, a control character, or another
    /// character the platform doesn't allow in a file name.
    /// </summary>
    public static bool ContainsInvalidCharacter(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.IndexOfAny([.. InvalidCharacters]) >= 0
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.Any(char.IsControl);
    }

    /// <summary>
    /// Whether Windows reserves <paramref name="name"/> for a device (e.g. <c>CON</c>, <c>nul</c> or <c>COM1.txt</c>),
    /// so it can't be a folder's name there.
    /// </summary>
    public static bool IsReservedOnWindows(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        int dot = name.IndexOf('.');
        string stem = (dot < 0 ? name : name[..dot]).TrimEnd();
        return WindowsReservedNames.Contains(stem);
    }

    /// <summary>
    /// Whether <paramref name="name"/> is longer than <see cref="MaxLength"/>.
    /// </summary>
    public static bool IsTooLong(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Encoding.UTF8.GetByteCount(name) > MaxLength;
    }
}
