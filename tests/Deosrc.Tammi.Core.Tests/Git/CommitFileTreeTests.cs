using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class CommitFileTreeTests
{
    [Theory]
    [InlineData("src/new.cs", null, "new.cs")]
    [InlineData("src/new.cs", "src/old.cs", "old.cs → new.cs")]
    [InlineData("lib/same.cs", "src/same.cs", "same.cs")]
    public void DisplayName_ShowsTheOldNameOfARenamedFile(string path, string? oldPath, string expected) =>
        Assert.Equal(expected, CommitFileTree.DisplayName(path, oldPath));

    [Theory]
    [InlineData("src/new.cs", null, null)]
    [InlineData("src/new.cs", "src/old.cs", null)]
    [InlineData("lib/new.cs", "src/old.cs", "src")]
    [InlineData("lib/new.cs", "old.cs", "/")]
    public void MovedFrom_IsTheOldFolderOfAFileMovedToAnother(string path, string? oldPath, string? expected) =>
        Assert.Equal(expected, CommitFileTree.MovedFrom(path, oldPath));

    [Fact]
    public void Build_PutsFoldersFirstThenFiles_EachInNameOrder()
    {
        IReadOnlyList<CommitFileTreeNode> tree = CommitFileTree.Build(
        [
            Change("readme.md"),
            Change("src/b.cs"),
            Change("docs/guide.md"),
            Change("src/A.cs"),
            Change("Build.props"),
            Change("src/lib/c.cs"),
        ]);

        Assert.Equal(["docs", "src", "Build.props", "readme.md"], tree.Select(node => node.Name));
        CommitFileTreeNode src = tree[1];
        Assert.True(src.IsFolder);
        Assert.Equal("src", src.Path);
        Assert.Equal(["lib", "A.cs", "b.cs"], src.Children.Select(node => node.Name));
        Assert.Equal("src/lib/c.cs", src.Children[0].Children.Single().Path);
        Assert.False(tree[2].IsFolder);
        Assert.Empty(tree[2].Children);
    }

    [Fact]
    public void Build_JoinsFoldersThatHoldOnlyOneFolder()
    {
        IReadOnlyList<CommitFileTreeNode> tree = CommitFileTree.Build(
        [
            Change("src/Deosrc.Tammi.UI/Workspaces/GitGraphView.razor"),
            Change("src/Deosrc.Tammi.UI/Workspaces/GitGraphView.razor.js"),
            Change("src/Deosrc.Tammi.UI/wwwroot/css/app.css"),
        ]);

        CommitFileTreeNode ui = Assert.Single(tree);
        Assert.Equal("src/Deosrc.Tammi.UI", ui.Name);
        Assert.Equal("src/Deosrc.Tammi.UI", ui.Path);
        Assert.Equal(["Workspaces", "wwwroot/css"], ui.Children.Select(node => node.Name));
        Assert.Equal("src/Deosrc.Tammi.UI/wwwroot/css", ui.Children[1].Path);
    }

    [Fact]
    public void Build_KeepsEachFilesChange()
    {
        var change = new CommitFileChange("a/new.txt", FileChangeKind.Renamed, "a/old.txt");

        CommitFileTreeNode file = Assert.Single(Assert.Single(CommitFileTree.Build([change])).Children);

        Assert.Same(change, file.Change);
    }

    [Theory]
    [InlineData("file.txt", "", "file.txt")]
    [InlineData("src/app/main.cs", "src/app", "main.cs")]
    public void FolderOfAndNameOf_SplitThePath(string path, string folder, string name)
    {
        Assert.Equal(folder, CommitFileTree.FolderOf(path));
        Assert.Equal(name, CommitFileTree.NameOf(path));
    }

    private static CommitFileChange Change(string path) => new(path, FileChangeKind.Modified);
}
