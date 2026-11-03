using System.Text.Json;
using System.Text.Json.Nodes;

namespace TAMMi.Core.Agents;

/// <summary>
/// Claude Code's hooks: HTTP hooks, passed in its <c>--settings</c> argument as JSON. Claude Code merges them with the
/// hooks in its own settings files, and a hook that can't reach TAMMi doesn't hold it up or block anything.
/// </summary>
/// <remarks>
/// See https://code.claude.com/docs/en/hooks. Claude Code doesn't report everything:
/// <list type="bullet">
/// <item>Nothing is reported between starting and the first prompt, so an agent started without an initial prompt
/// shows Running until the user sends one.</item>
/// <item>Nothing is reported when the user answers a permission prompt, so the agent shows Needs input until the tool
/// finishes.</item>
/// <item>Nothing is reported when the user interrupts a turn, so the agent shows Working until Claude Code says it's
/// been idle for a minute.</item>
/// </list>
/// </remarks>
public sealed class ClaudeCodeHooks : AgentHooks
{
    /// <summary>
    /// How long, in seconds, Claude Code waits for TAMMi to accept an event before it carries on without it.
    /// </summary>
    private const int TimeoutSeconds = 10;

    // Tools that ask the user something and wait for the answer.
    private const string QuestionTools = "AskUserQuestion|ExitPlanMode";

    private ClaudeCodeHooks()
    {
    }

    /// <summary>
    /// The hooks for Claude Code.
    /// </summary>
    public static ClaudeCodeHooks Instance { get; } = new();

    /// <inheritdoc />
    public override IReadOnlyList<string> CreateArguments(AgentHookEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        // Each event needs its own copy of the hook, because a node can only have one parent.
        JsonArray Hook(string? matcher = null)
        {
            var entry = new JsonObject();
            if (matcher is not null)
            {
                entry["matcher"] = matcher;
            }

            entry["hooks"] = new JsonArray(new JsonObject
            {
                ["type"] = "http",
                ["url"] = endpoint.Url.AbsoluteUri,
                // Claude Code fills in the variable, but only one listed in allowedEnvVars.
                ["headers"] = new JsonObject { ["Authorization"] = $"Bearer ${AgentHookEndpoint.TokenVariable}" },
                ["allowedEnvVars"] = new JsonArray(AgentHookEndpoint.TokenVariable),
                ["timeout"] = TimeoutSeconds,
            });
            return new JsonArray(entry);
        }

        var settings = new JsonObject
        {
            ["hooks"] = new JsonObject
            {
                ["UserPromptSubmit"] = Hook(),
                ["PreToolUse"] = Hook(QuestionTools),
                ["PostToolUse"] = Hook(),
                ["PostToolUseFailure"] = Hook(),
                ["PermissionRequest"] = Hook(),
                ["PermissionDenied"] = Hook(),
                ["Elicitation"] = Hook(),
                ["ElicitationResult"] = Hook(),
                ["Notification"] = Hook(),
                ["Stop"] = Hook(),
                ["StopFailure"] = Hook(),
            },
        };

        return ["--settings", settings.ToJsonString()];
    }

    /// <inheritdoc />
    public override AgentState? StateAfter(JsonElement hookEvent)
    {
        if (hookEvent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(hookEvent, "hook_event_name") switch
        {
            "UserPromptSubmit" or "PostToolUse" or "PostToolUseFailure" or "PermissionDenied" or "ElicitationResult"
                => AgentState.Working,
            "PreToolUse" => GetString(hookEvent, "tool_name") is "AskUserQuestion" or "ExitPlanMode"
                ? AgentState.NeedsInput
                : AgentState.Working,
            "PermissionRequest" or "Elicitation" => AgentState.NeedsInput,
            "Notification" => GetString(hookEvent, "notification_type") switch
            {
                "permission_prompt" or "elicitation_dialog" or "elicitation_url_dialog" => AgentState.NeedsInput,
                "idle_prompt" => AgentState.Idle,
                _ => null,
            },
            "Stop" or "StopFailure" => AgentState.Idle,
            _ => null,
        };
    }

    /// <inheritdoc />
    /// <remarks>Every event Claude Code sends has the session's ID.</remarks>
    public override bool ReportsSessionId => true;

    /// <inheritdoc />
    public override string? SessionIdFrom(JsonElement hookEvent) =>
        hookEvent.ValueKind == JsonValueKind.Object ? GetString(hookEvent, "session_id") : null;

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
