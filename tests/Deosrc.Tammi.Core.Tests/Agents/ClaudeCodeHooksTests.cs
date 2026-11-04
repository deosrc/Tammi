using System.Text.Json;
using Deosrc.Tammi.Core.Agents;

namespace Deosrc.Tammi.Core.Tests.Agents;

public class ClaudeCodeHooksTests
{
    private static readonly AgentHookEndpoint Endpoint =
        new(new Uri("http://127.0.0.1:4567/agent-hooks"), "token");

    public static TheoryData<string, AgentState?> Events => new()
    {
        { """{"hook_event_name":"UserPromptSubmit","prompt":"Hi"}""", AgentState.Working },
        { """{"hook_event_name":"PreToolUse","tool_name":"Bash"}""", AgentState.Working },
        { """{"hook_event_name":"PreToolUse","tool_name":"AskUserQuestion"}""", AgentState.NeedsInput },
        { """{"hook_event_name":"PreToolUse","tool_name":"ExitPlanMode"}""", AgentState.NeedsInput },
        { """{"hook_event_name":"PostToolUse","tool_name":"Bash"}""", AgentState.Working },
        { """{"hook_event_name":"PostToolUseFailure","tool_name":"Bash"}""", AgentState.Working },
        { """{"hook_event_name":"PermissionRequest","tool_name":"Bash"}""", AgentState.NeedsInput },
        { """{"hook_event_name":"PermissionDenied","tool_name":"Bash"}""", AgentState.Working },
        { """{"hook_event_name":"Elicitation"}""", AgentState.NeedsInput },
        { """{"hook_event_name":"ElicitationResult"}""", AgentState.Working },
        { """{"hook_event_name":"Notification","notification_type":"permission_prompt"}""", AgentState.NeedsInput },
        { """{"hook_event_name":"Notification","notification_type":"elicitation_dialog"}""", AgentState.NeedsInput },
        { """{"hook_event_name":"Notification","notification_type":"elicitation_url_dialog"}""", AgentState.NeedsInput },
        { """{"hook_event_name":"Notification","notification_type":"idle_prompt"}""", AgentState.Idle },
        { """{"hook_event_name":"Notification","notification_type":"auth_success"}""", null },
        { """{"hook_event_name":"Notification"}""", null },
        { """{"hook_event_name":"Stop","stop_hook_active":false}""", AgentState.Idle },
        { """{"hook_event_name":"StopFailure","error":"rate_limit"}""", AgentState.Idle },
        { """{"hook_event_name":"SubagentStop"}""", null },
        { """{"hook_event_name":"SessionEnd"}""", null },
        { """{"hook_event_name":42}""", null },
        { """{}""", null },
        { """[]""", null },
    };

    [Theory]
    [MemberData(nameof(Events))]
    public void StateAfter_MapsEventToState(string json, AgentState? expected)
    {
        using var hookEvent = JsonDocument.Parse(json);

        Assert.Equal(expected, ClaudeCodeHooks.Instance.StateAfter(hookEvent.RootElement));
    }

    [Fact]
    public void CreateArguments_PassesHooksAsSettings()
    {
        var arguments = ClaudeCodeHooks.Instance.CreateArguments(Endpoint);

        Assert.Equal(2, arguments.Count);
        Assert.Equal("--settings", arguments[0]);
        using var settings = JsonDocument.Parse(arguments[1]);
        Assert.Equal(["hooks"], settings.RootElement.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public void CreateArguments_HooksEveryEventThatChangesState()
    {
        using var settings = JsonDocument.Parse(ClaudeCodeHooks.Instance.CreateArguments(Endpoint)[1]);
        var events = settings.RootElement.GetProperty("hooks").EnumerateObject().Select(property => property.Name);

        Assert.Equal(
            [
                "Elicitation", "ElicitationResult", "Notification", "PermissionDenied", "PermissionRequest",
                "PostToolUse", "PostToolUseFailure", "PreToolUse", "Stop", "StopFailure", "UserPromptSubmit",
            ],
            events.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CreateArguments_SendsEachEventToEndpointWithTokenFromEnvironment()
    {
        using var settings = JsonDocument.Parse(ClaudeCodeHooks.Instance.CreateArguments(Endpoint)[1]);

        foreach (var hookEvent in settings.RootElement.GetProperty("hooks").EnumerateObject())
        {
            var hook = Assert.Single(Assert.Single(hookEvent.Value.EnumerateArray()).GetProperty("hooks").EnumerateArray());
            Assert.Equal("http", hook.GetProperty("type").GetString());
            Assert.Equal("http://127.0.0.1:4567/agent-hooks", hook.GetProperty("url").GetString());
            Assert.Equal(
                "Bearer $" + AgentHookEndpoint.TokenVariable,
                hook.GetProperty("headers").GetProperty("Authorization").GetString());
            Assert.Equal(
                [AgentHookEndpoint.TokenVariable],
                hook.GetProperty("allowedEnvVars").EnumerateArray().Select(name => name.GetString()));
            Assert.True(hook.GetProperty("timeout").GetInt32() > 0);
        }
    }

    [Fact]
    public void CreateArguments_LeavesTokenOutOfArguments()
    {
        var arguments = ClaudeCodeHooks.Instance.CreateArguments(Endpoint with { Token = "secret-token" });

        Assert.DoesNotContain(arguments, argument => argument.Contains("secret-token", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateArguments_HooksOnlyToolsThatAskTheUserBeforeTheyRun()
    {
        using var settings = JsonDocument.Parse(ClaudeCodeHooks.Instance.CreateArguments(Endpoint)[1]);
        var hooks = settings.RootElement.GetProperty("hooks");

        Assert.Equal(
            "AskUserQuestion|ExitPlanMode",
            hooks.GetProperty("PreToolUse")[0].GetProperty("matcher").GetString());
        Assert.False(hooks.GetProperty("PostToolUse")[0].TryGetProperty("matcher", out _));
    }

    [Theory]
    [InlineData("""{"hook_event_name":"Stop","session_id":"0b1c7a3e-5f7d-4c43-9b25-6f2b1b7a9e10"}""", "0b1c7a3e-5f7d-4c43-9b25-6f2b1b7a9e10")]
    [InlineData("""{"hook_event_name":"Stop"}""", null)]
    [InlineData("""{"session_id":42}""", null)]
    [InlineData("""["session_id"]""", null)]
    public void SessionIdFrom_ReadsEventsSessionId(string json, string? expected)
    {
        using var hookEvent = System.Text.Json.JsonDocument.Parse(json);

        Assert.True(ClaudeCodeHooks.Instance.ReportsSessionId);
        Assert.Equal(expected, ClaudeCodeHooks.Instance.SessionIdFrom(hookEvent.RootElement));
    }
}
