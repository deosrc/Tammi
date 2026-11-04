using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Suggests and checks the folder names of a workspace's Workspace Worktrees. Each is a single folder in the Workspace
/// Folder, valid on every platform and different from the others (ignoring case, as Windows does) and from
/// <see cref="Workspace.MetadataFolderName"/>.
/// </summary>
public static class WorktreeFolderNames
{
    /// <summary>
    /// How folder names are compared: ignoring case, because Windows' file systems do.
    /// </summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Suggests a folder name for each repository whose name isn't chosen yet.
    /// </summary>
    /// <remarks>
    /// A repository's folder is its name (e.g. <c>api</c>). When that clashes with another repository's suggested
    /// name, a chosen name or <see cref="Workspace.MetadataFolderName"/>, the owner is put in front (e.g.
    /// <c>acme-api</c>). If the owner is unknown, or the name still clashes, a number is added (e.g. <c>api-2</c>).
    /// </remarks>
    /// <param name="repositories">
    /// The workspace's repositories, in order: each one's name, owner (or <see langword="null"/>) and, if the user
    /// chose it, its folder name.
    /// </param>
    /// <returns>
    /// The folder name for each repository, in the same order: the chosen name where there is one, and a suggestion
    /// otherwise.
    /// </returns>
    public static IReadOnlyList<string> Suggest(IReadOnlyList<(string Name, string? Owner, string? ChosenName)> repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);

        var taken = new HashSet<string>(Comparer) { Workspace.MetadataFolderName };
        foreach (var repository in repositories)
        {
            if (repository.ChosenName is { } chosen)
            {
                taken.Add(chosen.Trim());
            }
        }

        var nameCounts = repositories
            .Where(repository => repository.ChosenName is null)
            .GroupBy(repository => repository.Name, Comparer)
            .ToDictionary(group => group.Key, group => group.Count(), Comparer);

        var names = new string[repositories.Count];
        for (int i = 0; i < repositories.Count; i++)
        {
            var (name, owner, chosenName) = repositories[i];
            if (chosenName is not null)
            {
                names[i] = chosenName;
                continue;
            }

            string candidate = name;
            if ((nameCounts[name] > 1 || taken.Contains(name)) && !string.IsNullOrEmpty(owner))
            {
                candidate = $"{owner}-{name}";
            }

            string unique = candidate;
            for (int number = 2; taken.Contains(unique); number++)
            {
                unique = $"{candidate}-{number}";
            }

            taken.Add(unique);
            names[i] = unique;
        }

        return names;
    }

    /// <summary>
    /// Returns why <paramref name="name"/> can't be a worktree's folder name, or <see langword="null"/> if it can.
    /// Surrounding spaces are ignored, because they're removed when the workspace is created.
    /// </summary>
    /// <param name="name">The folder name.</param>
    /// <param name="otherNames">The workspace's other worktrees' folder names, which it must differ from.</param>
    public static string? Validate(string? name, IEnumerable<string> otherNames)
    {
        ArgumentNullException.ThrowIfNull(otherNames);
        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return "Enter a folder name.";
        }

        if (name is "." or "..")
        {
            return "The folder name can't be '.' or '..'.";
        }

        if (FolderNames.ContainsInvalidCharacter(name))
        {
            return $"The folder name can't contain {string.Join(' ', FolderNames.InvalidCharacters)} or control "
                + "characters.";
        }

        if (name.EndsWith('.'))
        {
            return "The folder name can't end with a full stop, because Windows doesn't allow it.";
        }

        if (FolderNames.IsReservedOnWindows(name))
        {
            return $"'{name}' can't be a folder name on Windows, which reserves it for a device.";
        }

        if (FolderNames.IsTooLong(name))
        {
            return "The folder name is too long.";
        }

        if (Comparer.Equals(name, Workspace.MetadataFolderName))
        {
            return $"'{Workspace.MetadataFolderName}' is where Tammi keeps the workspace's own files.";
        }

        if (otherNames.Any(other => Comparer.Equals(other.Trim(), name)))
        {
            return $"Another repository's folder is '{name}'. Each needs its own folder.";
        }

        return null;
    }
}
