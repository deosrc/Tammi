using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class SideBySideAlignerTests
{
    private static DiffLine Ctx(string t, int o, int n) => new(DiffLineKind.Context, t, o, n);
    private static DiffLine Del(string t, int o) => new(DiffLineKind.Removed, t, o, null);
    private static DiffLine Add(string t, int n) => new(DiffLineKind.Added, t, null, n);

    [Fact]
    public void Align_ContextIsOnBothSides()
    {
        var rows = SideBySideAligner.Align(new DiffHunk("@@", [Ctx("a", 1, 1)]));

        SideBySideRow row = Assert.Single(rows);
        Assert.Equal("a", row.Left?.Text);
        Assert.Equal("a", row.Right?.Text);
    }

    [Fact]
    public void Align_RemovedLinesSitBesideTheAddedLinesThatReplaceThem()
    {
        var rows = SideBySideAligner.Align(new DiffHunk("@@", [Del("x", 1), Del("y", 2), Add("X", 1), Add("Y", 2)]));

        Assert.Equal(2, rows.Count);
        Assert.Equal(("x", "X"), (rows[0].Left?.Text, rows[0].Right?.Text));
        Assert.Equal(("y", "Y"), (rows[1].Left?.Text, rows[1].Right?.Text));
    }

    [Fact]
    public void Align_LeavesBlanksWhereASideHasNoLine()
    {
        var rows = SideBySideAligner.Align(new DiffHunk("@@", [Del("x", 1), Add("A", 1), Add("B", 2), Ctx("c", 2, 3), Del("gone", 3)]));

        Assert.Equal(4, rows.Count);
        Assert.Equal(("x", "A"), (rows[0].Left?.Text, rows[0].Right?.Text));
        Assert.Null(rows[1].Left);
        Assert.Equal("B", rows[1].Right?.Text);
        Assert.Equal("c", rows[2].Left?.Text);
        Assert.Equal("gone", rows[3].Left?.Text);
        Assert.Null(rows[3].Right);
    }

    [Fact]
    public void Align_AddedLinesWithNothingRemovedHaveBlankLeftSide()
    {
        var rows = SideBySideAligner.Align(new DiffHunk("@@", [Add("n", 1)]));

        Assert.Null(Assert.Single(rows).Left);
    }
}
