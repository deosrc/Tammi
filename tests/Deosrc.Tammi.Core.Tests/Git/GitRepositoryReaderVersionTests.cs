using Tammi.Core.Git;

namespace Tammi.Core.Tests.Git;

public class GitRepositoryReaderVersionTests
{
    [Fact]
    public void LibraryVersion_ReportsLibGit2SharpAndNativeLibGit2Versions()
    {
        GitLibraryVersion version = new GitRepositoryReader().LibraryVersion;

        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", version.LibGit2SharpVersion);
        Assert.NotNull(version.LibGit2Version);
        Assert.Matches(@"^\d+\.\d+\.\d+$", version.LibGit2Version);
        Assert.Matches("^[0-9a-f]{7}$", version.LibGit2Commit);
    }
}
