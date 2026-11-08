namespace Deosrc.Tammi.Core.Integrations.GitHub;

/// <summary>
/// The answer to a request to GitHub's GraphQL API. GitHub answers errors with status 200, and the data can be partial:
/// a field it couldn't resolve is <see langword="null"/>, with an error naming its path.
/// </summary>
/// <param name="Data">The data, or <see langword="null"/> if the query couldn't run at all.</param>
/// <param name="Errors">The errors, if there were any.</param>
internal sealed record GitHubGraphQlResponse<TData>(TData? Data, IReadOnlyList<GitHubGraphQlError>? Errors)
    where TData : class
{
    /// <summary>
    /// The first error at <paramref name="path"/> (e.g. <c>repository.pullRequests</c>), or <see langword="null"/>.
    /// </summary>
    public GitHubGraphQlError? ErrorAt(string path) => Errors?.FirstOrDefault(error => error.PathText == path);
}
