using System.Text;
using Deosrc.Tammi.Core.Terminals;

namespace Deosrc.Tammi.Core.Tests.Terminals;

public class TerminalTitleParserTests
{
    private readonly TerminalTitleParser _parser = new();

    private IReadOnlyList<string> Parse(string text) => _parser.Parse(Encoding.UTF8.GetBytes(text));

    [Theory]
    [InlineData("\x1b]0;My title\a", "My title")]
    [InlineData("\x1b]2;My title\a", "My title")]
    [InlineData("\x1b]2;My title\x1b\\", "My title")]
    [InlineData("before\x1b]0;  padded \u0001 \a after", "padded")]
    [InlineData("\x1b]2;✳ Claude Code\a", "✳ Claude Code")]
    public void Parse_TitleSequence_FindsTheTitle(string output, string expected) =>
        Assert.Equal([expected], Parse(output));

    [Fact]
    public void Parse_EmptyTitle_ReportsItCleared() => Assert.Equal([""], Parse("\x1b]0;\a"));

    [Fact]
    public void Parse_SeveralTitles_ReturnsThemInOrder() =>
        Assert.Equal(["one", "two"], Parse("\x1b]0;one\a text \x1b]2;two\a"));

    [Theory]
    [InlineData("\x1b]1;icon only\a")]
    [InlineData("\x1b]8;;https://example.com\a")]
    [InlineData("\x1b[31mred\x1b[0m")]
    [InlineData("\x1b]0;unterminated")]
    [InlineData("\x1b]0;cancelled\x18")]
    public void Parse_NotATitle_FindsNothing(string output) => Assert.Empty(Parse(output));

    [Fact]
    public void Parse_SequenceSplitAcrossChunks_FindsTheTitle()
    {
        Assert.Empty(Parse("\x1b"));
        Assert.Empty(Parse("]0;Spl"));
        Assert.Empty(Parse("it\x1b"));
        Assert.Equal(["Split"], Parse("\\done"));
    }

    [Fact]
    public void Parse_TooLong_IsIgnored() =>
        Assert.Empty(Parse("\x1b]0;" + new string('a', TerminalTitleParser.MaxTitleBytes * 4) + "\a"));
}
