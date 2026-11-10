using Deosrc.Tammi.Core.Agents;

namespace Deosrc.Tammi.Core.Tests.Agents;

public class AgentSessionTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 3, 9, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("ses_e427d777fffe6WDj5dcMZ1iGSn", true)]
    [InlineData("ses_1", true)]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301", true)]
    [InlineData("ses_", false)]
    [InlineData("ses", false)]
    [InlineData("ses_abc def", false)]
    [InlineData("ses_abc;rm", false)]
    [InlineData("ses_-x", false)]
    [InlineData("--session", false)]
    [InlineData("-ses_abc", false)]
    [InlineData("SES_abc", false)]
    [InlineData(null, false)]
    public void IsValidId_AcceptsUuidsAndOpenCodeIds(string? id, bool expected)
    {
        Assert.Equal(expected, AgentSession.IsValidId(id));
    }

    [Fact]
    public async Task Of_RecordsAgentsSession()
    {
        var id = Guid.NewGuid().ToString();
        await using var agent = new Agent(AgentDefinition.ClaudeCode, "/ws", new FakePtySession(), sessionId: id);

        Assert.Equal(new AgentSession(id, "Claude Code", "/ws", StartedAt), AgentSession.Of(agent, StartedAt));
    }

    [Fact]
    public async Task Of_WithoutSessionId_ReturnsNull()
    {
        await using var agent = new Agent(new AgentDefinition("Test", "agent"), "/ws", new FakePtySession());

        Assert.Null(AgentSession.Of(agent, StartedAt));
    }

    [Theory]
    [InlineData("0b1c7a3e-5f7d-4c43-9b25-6f2b1b7a9e10", true)]
    [InlineData("0B1C7A3E-5F7D-4C43-9B25-6F2B1B7A9E10", true)]
    [InlineData("0b1c7a3e5f7d4c439b256f2b1b7a9e10", false)]
    [InlineData("{0b1c7a3e-5f7d-4c43-9b25-6f2b1b7a9e10}", false)]
    [InlineData("--resume", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidId_AcceptsUuidsOnly(string? id, bool expected)
    {
        Assert.Equal(expected, AgentSession.IsValidId(id));
    }

    [Fact]
    public void FindDefinition_MatchesNameIgnoringCaseAndSpaces()
    {
        var custom = new AgentDefinition(" My Agent ", "agent");
        var session = new AgentSession(Guid.NewGuid().ToString(), "my agent", "/ws", StartedAt);

        Assert.Same(custom, session.FindDefinition([AgentDefinition.ClaudeCode, custom]));
        Assert.Null((session with { AgentName = "Other" }).FindDefinition([AgentDefinition.ClaudeCode, custom]));
    }
}
