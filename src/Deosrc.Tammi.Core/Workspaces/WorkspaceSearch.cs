namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Matches workspaces against the text in Home's search box.
/// </summary>
public static class WorkspaceSearch
{
    /// <summary>
    /// Returns whether a workspace matches the search text. Each word of the text must be found, ignoring case, in the
    /// workspace's name, one of its task keys (e.g. <c>acme/api#42</c>), or one of its repositories' folder names or
    /// branches. Blank text matches every workspace. A workspace whose metadata can't be read is matched by its name
    /// only.
    /// </summary>
    public static bool Matches(WorkspaceListing listing, string? text)
    {
        ArgumentNullException.ThrowIfNull(listing);
        var words = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return true;
        }

        var fields = Fields(listing).ToArray();
        return words.All(word => fields.Any(field => field.Contains(word, StringComparison.CurrentCultureIgnoreCase)));
    }

    private static IEnumerable<string> Fields(WorkspaceListing listing)
    {
        yield return listing.Name;
        if (listing.Workspace is not { } workspace)
        {
            yield break;
        }

        foreach (var task in workspace.Tasks)
        {
            yield return task.Key;
        }

        foreach (var repository in workspace.Repositories)
        {
            yield return repository.FolderName;
            yield return repository.Branch;
        }
    }
}
