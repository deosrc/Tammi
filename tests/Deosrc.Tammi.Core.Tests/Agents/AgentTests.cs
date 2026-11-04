using Tammi.Core.Agents;

namespace Tammi.Core.Tests.Agents;

public class AgentTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void State_IsRunning_UntilProcessExits()
    {
        var agent = CreateAgent(new FakePtySession());

        Assert.Equal(AgentState.Running, agent.State);
        Assert.Null(agent.ExitCode);
        Assert.False(agent.StopRequested);
        Assert.False(agent.Ended.IsCompleted);
    }

    [Fact]
    public async Task State_IsExited_WhenProcessExitsWithZero()
    {
        var session = new FakePtySession();
        var agent = CreateAgent(session);

        session.Exit(0);

        Assert.Equal(AgentState.Exited, await WaitForEndAsync(agent));
        Assert.Equal(AgentState.Exited, agent.State);
        Assert.Equal(0, agent.ExitCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(137)]
    public async Task State_IsError_WhenProcessExitsWithNonZeroOrIsKilled(int exitCode)
    {
        var session = new FakePtySession();
        var agent = CreateAgent(session);

        session.Exit(exitCode);

        Assert.Equal(AgentState.Error, await WaitForEndAsync(agent));
        Assert.Equal(exitCode, agent.ExitCode);
    }

    [Fact]
    public async Task State_IsError_WhenExitCantBeFollowed()
    {
        var session = new FakePtySession();
        var agent = CreateAgent(session);

        session.Crash();

        Assert.Equal(AgentState.Error, await WaitForEndAsync(agent));
        Assert.Null(agent.ExitCode);
    }

    [Fact]
    public async Task StateChanged_IsRaisedOnceWithNewState()
    {
        var session = new FakePtySession();
        var agent = CreateAgent(session);
        var raised = new TaskCompletionSource<AgentState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        agent.StateChanged += changed =>
        {
            Interlocked.Increment(ref count);
            raised.TrySetResult(changed.State);
        };

        session.Exit(2);

        Assert.Equal(AgentState.Error, await raised.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task StopAsync_EndsSessionAndIsExited()
    {
        var session = new FakePtySession();
        var agent = CreateAgent(session);

        await agent.StopAsync();

        Assert.True(session.Disposed);
        Assert.True(agent.StopRequested);
        Assert.Equal(AgentState.Exited, agent.State);
        Assert.Equal(129, agent.ExitCode);
    }

    [Fact]
    public async Task StopAsync_AfterProcessFailed_KeepsError()
    {
        var session = new FakePtySession();
        var agent = CreateAgent(session);
        session.Exit(1);
        await WaitForEndAsync(agent);

        await agent.StopAsync();

        Assert.True(session.Disposed);
        Assert.False(agent.StopRequested);
        Assert.Equal(AgentState.Error, agent.State);
        Assert.Equal(1, agent.ExitCode);
    }

    [Fact]
    public async Task StopAsync_WhenCalledTwice_StopsOnce()
    {
        var agent = CreateAgent(new FakePtySession());

        var first = agent.StopAsync();
        var second = agent.StopAsync();

        Assert.Same(first, second);
        await first;
    }

    [Fact]
    public async Task StopAsync_Completes_WhenProcessDoesNotEnd()
    {
        var agent = CreateAgent(new FakePtySession { ExitCodeWhenDisposed = null });

        await agent.StopAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(AgentState.Running, agent.State);
    }

    [Fact]
    public async Task DisposeAsync_StopsAgent()
    {
        var session = new FakePtySession();
        var agent = CreateAgent(session);

        await agent.DisposeAsync();

        Assert.True(session.Disposed);
        Assert.Equal(AgentState.Exited, agent.State);
    }

    private static Agent CreateAgent(FakePtySession session) =>
        new(AgentDefinition.ClaudeCode, "/work", session);

    private static Task<AgentState> WaitForEndAsync(Agent agent) =>
        agent.Ended.WaitAsync(Timeout, TestContext.Current.CancellationToken);
}
