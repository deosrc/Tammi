namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// The author git records on new commits: <c>user.name</c> and <c>user.email</c> in the user's global git config.
/// </summary>
/// <param name="Name">The author's name, or <see langword="null"/> if it isn't set.</param>
/// <param name="Email">The author's email address, or <see langword="null"/> if it isn't set.</param>
public sealed record GitIdentity(string? Name, string? Email);
