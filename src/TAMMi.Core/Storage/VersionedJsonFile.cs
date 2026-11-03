using System.Text.Json;
using System.Text.Json.Nodes;

namespace TAMMi.Core.Storage;

/// <summary>
/// Reads and writes TAMMi's versioned JSON files, such as the settings file and workspace metadata.
/// </summary>
/// <remarks>
/// Each file is a JSON object with a <c>version</c> property. When a file's layout changes, its version goes up and a
/// migration is added for it. The migration at index <c>i</c> upgrades a file from version <c>i + 1</c> to version
/// <c>i + 2</c>, so the current version is one more than the number of migrations. A file from a newer version of
/// TAMMi is read as far as possible.
/// </remarks>
internal static class VersionedJsonFile
{
    private const string VersionProperty = "version";

    /// <summary>
    /// The options used to read and write the files' contents.
    /// </summary>
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Parses a file's contents and upgrades them to the current version.
    /// </summary>
    /// <returns>The file's JSON object, without its <c>version</c> property.</returns>
    /// <exception cref="JsonException">The contents aren't valid JSON.</exception>
    /// <exception cref="InvalidDataException">The contents aren't a JSON object, or the version isn't valid.</exception>
    public static JsonObject Parse(string contents, IReadOnlyList<Action<JsonObject>> migrations)
    {
        if (JsonNode.Parse(contents, documentOptions: DocumentOptions) is not JsonObject json)
        {
            throw new InvalidDataException("The file doesn't hold a JSON object.");
        }

        var version = ReadVersion(json);
        for (var i = version - 1; i < migrations.Count; i++)
        {
            migrations[i](json);
        }

        json.Remove(VersionProperty);
        return json;
    }

    /// <summary>
    /// Parses a file's contents, upgrades them to the current version and deserializes them.
    /// </summary>
    /// <exception cref="JsonException">The contents aren't valid JSON, or don't match <typeparamref name="T"/>.</exception>
    /// <exception cref="InvalidDataException">The contents aren't a JSON object, or the version isn't valid.</exception>
    public static T Deserialize<T>(string contents, IReadOnlyList<Action<JsonObject>> migrations)
        where T : class =>
        Parse(contents, migrations).Deserialize<T>(SerializerOptions)
            ?? throw new InvalidDataException("The file is empty.");

    /// <summary>
    /// Writes <paramref name="value"/> to <paramref name="path"/> with the given version, creating its folder if
    /// needed. The file is written in full or not at all.
    /// </summary>
    public static async Task WriteAsync<T>(
        string path,
        T value,
        int version,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToNode(value, SerializerOptions)!.AsObject();
        json.Insert(0, VersionProperty, version);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write a temporary file and move it into place, so a failed write can't leave a half-written file.
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, json.ToJsonString(SerializerOptions), cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static int ReadVersion(JsonObject json)
    {
        // TAMMi always writes the version, but a file written by hand might not have one.
        if (!json.TryGetPropertyValue(VersionProperty, out var versionNode) || versionNode is null)
        {
            return 1;
        }

        if (versionNode is not JsonValue value || !value.TryGetValue(out int version) || version < 1)
        {
            throw new InvalidDataException($"The file's version '{versionNode.ToJsonString()}' isn't valid.");
        }

        return version;
    }
}
