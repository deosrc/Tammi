using System.Text;

namespace Deosrc.Tammi.Core.Installation;

/// <summary>
/// Builds the freedesktop <c>.desktop</c> file that puts Tammi in a Linux desktop's application menu.
/// </summary>
public static class DesktopEntry
{
    /// <summary>
    /// The file's name.
    /// </summary>
    public const string FileName = "tammi.desktop";

    /// <summary>
    /// Renders the file for an executable.
    /// </summary>
    public static string Render(string executablePath)
    {
        var builder = new StringBuilder();
        builder.Append("[Desktop Entry]\n");
        builder.Append("Type=Application\n");
        builder.Append("Name=Tammi\n");
        builder.Append("Comment=Task-first software development workspace manager\n");
        builder.Append("Exec=").Append(QuoteExec(executablePath)).Append('\n');
        builder.Append("Path=").Append(Path.GetDirectoryName(executablePath)).Append('\n');
        builder.Append("Terminal=false\n");
        builder.Append("Categories=Development;\n");
        return builder.ToString();
    }

    /// <summary>
    /// Quotes a program path for the <c>Exec</c> key, which treats <c>"</c>, <c>`</c>, <c>$</c> and <c>\</c> specially,
    /// and <c>%</c> as the start of a field code.
    /// </summary>
    internal static string QuoteExec(string path)
    {
        var escaped = path
            .Replace("\\", "\\\\\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\\\"", StringComparison.Ordinal)
            .Replace("`", "\\\\`", StringComparison.Ordinal)
            .Replace("$", "\\\\$", StringComparison.Ordinal)
            .Replace("%", "%%", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
