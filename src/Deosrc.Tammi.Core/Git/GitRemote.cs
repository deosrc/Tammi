namespace TAMMi.Core.Git;

/// <summary>
/// A remote of a git repository.
/// </summary>
/// <param name="Name">The remote's name, e.g. <c>origin</c> or <c>upstream</c>.</param>
/// <param name="Url">
/// The URL fetched from, as configured. SSH URLs such as <c>git@github.com:acme/api.git</c> aren't valid
/// <see cref="Uri"/>s, so this is a string.
/// </param>
/// <param name="PushUrl">The URL pushed to, if it's set and differs from <paramref name="Url"/>.</param>
public sealed record GitRemote(string Name, string Url, string? PushUrl = null);
