namespace Deosrc.Tammi.Core.Installation;

/// <summary>
/// Something Tammi needs that isn't installed.
/// </summary>
/// <param name="Name">What's missing.</param>
/// <param name="Problem">What's wrong.</param>
/// <param name="Fix">How to fix it.</param>
public sealed record DependencyProblem(string Name, string Problem, string Fix);
