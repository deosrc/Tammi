using System.Text.Json;

namespace Deosrc.Tammi.Installation;

/// <summary>
/// What's recorded in a version's folder about how it was installed. Its presence also marks the install as complete.
/// </summary>
/// <param name="Version">The installed version.</param>
/// <param name="Flavour">Whether the install bundles the .NET runtime, so an update gets the same kind.</param>
/// <param name="RuntimeIdentifier">The OS and architecture the install is for.</param>
public sealed record InstallMetadata(string Version, ReleaseFlavour Flavour, string RuntimeIdentifier)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>
    /// Writes the metadata to a version's folder.
    /// </summary>
    public void Save(string versionDirectory) =>
        File.WriteAllText(Path.Combine(versionDirectory, InstallLocation.MetadataFileName), JsonSerializer.Serialize(this, Options));

    /// <summary>
    /// Reads the metadata from a version's folder.
    /// </summary>
    /// <returns>The metadata, or <see langword="null"/> if it's missing or unreadable.</returns>
    public static InstallMetadata? Load(string versionDirectory)
    {
        try
        {
            return JsonSerializer.Deserialize<InstallMetadata>(
                File.ReadAllText(Path.Combine(versionDirectory, InstallLocation.MetadataFileName)), Options);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
