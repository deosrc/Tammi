using TAMMi.Core.Agents;

namespace TAMMi.Core.Tests.Agents;

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
}
