namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// What a file's diff is computed with. These only change what's shown, never the files.
/// </summary>
/// <param name="Whitespace">How whitespace changes are treated.</param>
/// <param name="IgnoreBlankLines">Whether changes whose lines are all blank are ignored.</param>
/// <param name="ContextLines">How many unchanged lines are shown around each change.</param>
public sealed record GitDiffOptions(
    GitDiffWhitespace Whitespace = GitDiffWhitespace.Show,
    bool IgnoreBlankLines = false,
    int ContextLines = GitDiffOptions.DefaultContextLines)
{
    /// <summary>The <see cref="ContextLines"/> git uses unless told otherwise.</summary>
    public const int DefaultContextLines = 3;

    /// <summary>The most <see cref="ContextLines"/> allowed.</summary>
    public const int MaxContextLines = 100;

    /// <summary>
    /// Whether the diff has to be read by the git CLI, because it ignores something the in-process reader can't.
    /// </summary>
    public bool NeedsCli => Whitespace != GitDiffWhitespace.Show || IgnoreBlankLines;

    /// <summary>
    /// The <c>git diff</c> arguments that choose the context and what is ignored.
    /// </summary>
    public IReadOnlyList<string> ToArguments()
    {
        var arguments = new List<string> { $"-U{Math.Clamp(ContextLines, 0, MaxContextLines)}" };
        switch (Whitespace)
        {
            case GitDiffWhitespace.IgnoreAmount:
                arguments.Add("-b");
                break;
            case GitDiffWhitespace.IgnoreAll:
                arguments.Add("-w");
                break;
            case GitDiffWhitespace.IgnoreAtEol:
                arguments.Add("--ignore-space-at-eol");
                break;
        }

        if (IgnoreBlankLines)
        {
            arguments.Add("--ignore-blank-lines");
        }

        return arguments;
    }
}
