namespace Deosrc.Tammi.Core.Catalogue;

/// <summary>
/// A Monitored Folder or manually added local repository that couldn't be catalogued, e.g. because it's missing.
/// </summary>
/// <param name="Path">The Monitored Folder or repository, as it is in the settings.</param>
/// <param name="Message">What's wrong, for showing to the user.</param>
public sealed record CatalogueProblem(string Path, string Message);
