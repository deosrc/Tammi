using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class FileChangeTests
{
    [Theory]
    [InlineData(SubmoduleCommitChange.Staged, FileChangeKind.Modified, FileChangeKind.None)]
    [InlineData(SubmoduleCommitChange.Unstaged, FileChangeKind.None, FileChangeKind.Modified)]
    [InlineData(SubmoduleCommitChange.Staged | SubmoduleCommitChange.Unstaged, FileChangeKind.Modified, FileChangeKind.Modified)]
    public void ForSubmodule_ChangedCommit_IsAChangeToTheSubmodulePath(
        SubmoduleCommitChange commitChange,
        FileChangeKind staged,
        FileChangeKind unstaged)
    {
        FileChange? change = FileChange.ForSubmodule(new SubmoduleStatus("lib", "libs/lib", commitChange, null));

        Assert.Equal(new FileChange("libs/lib", staged, unstaged, IsSubmodule: true), change);
    }

    [Fact]
    public void ForSubmodule_UnchangedCommit_IsNull()
    {
        Assert.Null(FileChange.ForSubmodule(new SubmoduleStatus("lib", "lib", SubmoduleCommitChange.None, null)));
    }
}
