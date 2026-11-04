namespace TAMMi.Core.Platform;

/// <summary>
/// Deletes folders without following links out of them.
/// </summary>
public static class FileSystemDeletion
{
    /// <summary>
    /// Deletes a folder and everything in it. A link (symbolic link or junction) inside it is deleted, but the folder
    /// it points at isn't touched. If <paramref name="path"/> is itself a link, only the link is deleted. Read-only
    /// files, such as git's object files, are deleted too.
    /// </summary>
    /// <param name="path">The folder to delete. Nothing happens if it doesn't exist.</param>
    /// <param name="last">
    /// The name of an entry in the folder to delete after all the others, e.g. a folder whose loss would make a
    /// partly deleted folder hard to recognise.
    /// </param>
    public static void DeleteFolder(string path, string? last = null)
    {
        var folder = new DirectoryInfo(path);
        if (!folder.Exists && folder.LinkTarget is null)
        {
            return;
        }

        if (folder.LinkTarget is not null)
        {
            folder.Delete();
            return;
        }

        FileSystemInfo? deferred = null;
        foreach (var entry in folder.EnumerateFileSystemInfos())
        {
            if (last is not null && entry.Name == last)
            {
                deferred = entry;
                continue;
            }

            DeleteEntry(entry);
        }

        if (deferred is not null)
        {
            DeleteEntry(deferred);
        }

        ClearReadOnly(folder);
        folder.Delete();
    }

    private static void DeleteEntry(FileSystemInfo entry)
    {
        if (entry is DirectoryInfo directory && directory.LinkTarget is null)
        {
            DeleteFolder(directory.FullName);
            return;
        }

        // A file, or a link to a file or folder: deleting it removes only the link.
        ClearReadOnly(entry);
        entry.Delete();
    }

    private static void ClearReadOnly(FileSystemInfo entry)
    {
        // Git makes object files read-only, which stops them being deleted on Windows. A link's attributes are left
        // alone, because setting them could change what the link points at instead.
        if (entry.LinkTarget is null && entry.Attributes.HasFlag(FileAttributes.ReadOnly))
        {
            entry.Attributes &= ~FileAttributes.ReadOnly;
        }
    }
}
