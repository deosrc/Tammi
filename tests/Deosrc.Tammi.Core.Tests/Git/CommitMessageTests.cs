using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class CommitMessageTests
{
    [Fact]
    public void CoAuthors_ReadsTheTrailers()
    {
        const string message = """
            Add the git graph

            Some detail.

            Co-authored-by: Ada Lovelace <ada@example.com>
            co-authored-by:   Alan Turing   <alan@example.com>  
            Co-Authored-By: Ada again <ADA@example.com>
            Co-authored-by: No email
            Signed-off-by: Someone <someone@example.com>
            """;

        Assert.Equal(
            [new CommitCoAuthor("Ada Lovelace", "ada@example.com"), new CommitCoAuthor("Alan Turing", "alan@example.com")],
            CommitMessage.CoAuthors(message));
    }

    [Fact]
    public void CoAuthors_WindowsLineEndings_AreRead()
    {
        Assert.Equal(
            [new CommitCoAuthor("Ada", "ada@example.com")],
            CommitMessage.CoAuthors("Subject\r\n\r\nCo-authored-by: Ada <ada@example.com>\r\n"));
    }

    [Fact]
    public void CoAuthors_NoTrailers_IsEmpty()
    {
        Assert.Empty(CommitMessage.CoAuthors("Just a subject\n"));
    }
}
