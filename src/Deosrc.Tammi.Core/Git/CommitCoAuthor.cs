namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Someone a commit message names in a <c>Co-authored-by:</c> trailer.
/// </summary>
/// <param name="Name">Their name, as written.</param>
/// <param name="Email">Their email address, as written.</param>
public sealed record CommitCoAuthor(string Name, string Email);
