using Deosrc.Tammi.Core.Git;
using static Deosrc.Tammi.Core.Git.GitGraphLineHalf;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitGraphLayoutTests
{
    private static readonly CommitSignature Signature = new("Tammi Tests", "tests@tammi.invalid", DateTimeOffset.UnixEpoch);

    [Fact]
    public void Append_LinearHistory_UsesOneLane()
    {
        IReadOnlyList<GitGraphRow> rows = new GitGraphLayout().Append(
            [Commit("c", "b"), Commit("b", "a"), Commit("a")]);

        AssertRow(rows[0], lane: 0, colour: 0, laneCount: 1, Line(0, 0, 0, Bottom));
        AssertRow(rows[1], lane: 0, colour: 0, laneCount: 1, Line(0, 0, 0, Top), Line(0, 0, 0, Bottom));
        AssertRow(rows[2], lane: 0, colour: 0, laneCount: 1, Line(0, 0, 0, Top));
    }

    [Fact]
    public void Append_BranchAndMerge_OpensASecondLaneThatJoinsTheCommonParent()
    {
        IReadOnlyList<GitGraphRow> rows = new GitGraphLayout().Append(
            [Commit("merge", "main", "feature"), Commit("feature", "base"), Commit("main", "base"), Commit("base")]);

        AssertRow(rows[0], lane: 0, colour: 0, laneCount: 2, Line(0, 0, 0, Bottom), Line(0, 1, 1, Bottom));
        AssertRow(
            rows[1], lane: 1, colour: 1, laneCount: 2,
            Line(0, 0, 0, Top), Line(1, 1, 1, Top), Line(0, 0, 0, Bottom), Line(1, 1, 1, Bottom));
        AssertRow(
            rows[2], lane: 0, colour: 0, laneCount: 2,
            Line(0, 0, 0, Top), Line(1, 1, 1, Top), Line(0, 0, 0, Bottom), Line(1, 1, 1, Bottom));
        AssertRow(rows[3], lane: 0, colour: 0, laneCount: 2, Line(0, 0, 0, Top), Line(1, 0, 1, Top));
    }

    [Fact]
    public void Append_TwoBranchTips_EachTakeTheirOwnLaneAndColour()
    {
        IReadOnlyList<GitGraphRow> rows = new GitGraphLayout().Append(
            [Commit("first", "base"), Commit("second", "base"), Commit("base")]);

        AssertRow(rows[0], lane: 0, colour: 0, laneCount: 1, Line(0, 0, 0, Bottom));
        AssertRow(
            rows[1], lane: 1, colour: 1, laneCount: 2, Line(0, 0, 0, Top), Line(0, 0, 0, Bottom), Line(1, 1, 1, Bottom));
        AssertRow(rows[2], lane: 0, colour: 0, laneCount: 2, Line(0, 0, 0, Top), Line(1, 0, 1, Top));
    }

    [Fact]
    public void Append_OctopusMerge_OpensALaneForEachParent()
    {
        IReadOnlyList<GitGraphRow> rows = new GitGraphLayout().Append([Commit("octopus", "a", "b", "c")]);

        AssertRow(
            rows[0], lane: 0, colour: 0, laneCount: 3,
            Line(0, 0, 0, Bottom), Line(0, 1, 1, Bottom), Line(0, 2, 2, Bottom));
    }

    [Fact]
    public void Append_MergeOfAParentAlreadyWaitedFor_JoinsItsLane()
    {
        IReadOnlyList<GitGraphRow> rows = new GitGraphLayout().Append(
            [Commit("tip", "shared"), Commit("merge", "own", "shared")]);

        AssertRow(
            rows[1], lane: 1, colour: 1, laneCount: 2,
            Line(0, 0, 0, Top), Line(1, 0, 0, Bottom), Line(0, 0, 0, Bottom), Line(1, 1, 1, Bottom));
    }

    [Fact]
    public void Append_LaneFreedByAnEndedBranch_IsReused()
    {
        IReadOnlyList<GitGraphRow> rows = new GitGraphLayout().Append(
            [Commit("first", "base"), Commit("second", "base"), Commit("base"), Commit("unrelated")]);

        Assert.Equal(0, rows[3].Lane);
        Assert.Equal(2, rows[3].Colour);
        Assert.Empty(rows[3].Lines);
    }

    [Fact]
    public void Append_InPages_MatchesAppendingAllAtOnce()
    {
        CommitInfo[] commits =
        [
            Commit("merge", "main", "feature"), Commit("feature", "feature-base"), Commit("main", "base"),
            Commit("feature-base", "base"), Commit("base", "root"), Commit("root"),
        ];

        IReadOnlyList<GitGraphRow> allAtOnce = new GitGraphLayout().Append(commits);
        var paged = new GitGraphLayout();
        GitGraphRow[] inPages = [.. paged.Append(commits[..2]), .. paged.Append(commits[2..5]), .. paged.Append(commits[5..])];

        Assert.Equal(allAtOnce.Count, inPages.Length);
        for (int i = 0; i < inPages.Length; i++)
        {
            Assert.Equal(allAtOnce[i].Lane, inPages[i].Lane);
            Assert.Equal(allAtOnce[i].Colour, inPages[i].Colour);
            Assert.Equal(allAtOnce[i].LaneCount, inPages[i].LaneCount);
            Assert.Equal(allAtOnce[i].Lines, inPages[i].Lines);
        }
    }

    [Fact]
    public void Append_ParentNotYetGiven_RunsOffTheBottom()
    {
        IReadOnlyList<GitGraphRow> rows = new GitGraphLayout().Append([Commit("tip", "beyond-the-page")]);

        AssertRow(rows[0], lane: 0, colour: 0, laneCount: 1, Line(0, 0, 0, Bottom));
    }

    private static void AssertRow(GitGraphRow row, int lane, int colour, int laneCount, params GitGraphLine[] lines)
    {
        Assert.Equal(lane, row.Lane);
        Assert.Equal(colour, row.Colour);
        Assert.Equal(laneCount, row.LaneCount);
        Assert.Equal(lines, row.Lines);
    }

    private static GitGraphLine Line(int from, int to, int colour, GitGraphLineHalf half) => new(from, to, colour, half);

    private static CommitInfo Commit(string sha, params string[] parents) =>
        new(sha, parents, Signature, Signature, sha, sha);
}
