namespace TAMMi.Core.Git;

/// <summary>
/// The versions of the library TAMMi uses for in-process git reads.
/// </summary>
/// <param name="LibGit2SharpVersion">The LibGit2Sharp version, e.g. <c>0.32.0</c>.</param>
/// <param name="LibGit2Version">
/// The version of the native libgit2 library, e.g. <c>1.8.4</c>, or <see langword="null"/> if it couldn't be read.
/// </param>
/// <param name="LibGit2Commit">The short hash of the libgit2 commit the native library was built from.</param>
public sealed record GitLibraryVersion(string LibGit2SharpVersion, string? LibGit2Version, string LibGit2Commit);
