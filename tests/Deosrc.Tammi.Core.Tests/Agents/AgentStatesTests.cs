using Deosrc.Tammi.Core.Agents;

namespace Deosrc.Tammi.Core.Tests.Agents;

public class AgentStatesTests
{
    [Theory]
    [InlineData(0, false, AgentState.Exited)]
    [InlineData(1, false, AgentState.Error)]
    [InlineData(-1, false, AgentState.Error)]
    [InlineData(137, false, AgentState.Error)] // Killed with SIGKILL by something else.
    [InlineData(null, false, AgentState.Error)]
    [InlineData(0, true, AgentState.Exited)]
    [InlineData(129, true, AgentState.Exited)] // Hung up by Stop.
    [InlineData(137, true, AgentState.Exited)] // Killed by Stop.
    [InlineData(null, true, AgentState.Exited)]
    public void AfterExit_ReturnsState(int? exitCode, bool stopped, AgentState expected)
    {
        Assert.Equal(expected, AgentStates.AfterExit(exitCode, stopped));
    }

    [Theory]
    [InlineData(AgentState.Running, true)]
    [InlineData(AgentState.Working, true)]
    [InlineData(AgentState.NeedsInput, true)]
    [InlineData(AgentState.Idle, true)]
    [InlineData(AgentState.Exited, false)]
    [InlineData(AgentState.Error, false)]
    public void IsRunning_IsTrueWhileProcessRuns(AgentState state, bool expected)
    {
        Assert.Equal(expected, state.IsRunning());
    }
}
