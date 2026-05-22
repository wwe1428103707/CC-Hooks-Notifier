using System.Text.Json;
using System.Text.Json.Serialization;

namespace HooksNotifier;

// ── JSON options (shared) ──────────────────────────────────────────────
internal static class JsonOpts
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

// ── Hook data models ───────────────────────────────────────────────────
internal sealed record HookData
{
    [JsonPropertyName("hook_event_name")]
    public string? HookEventName { get; init; }

    [JsonPropertyName("hook_event_type")]
    public string? HookEventType { get; init; }

    [JsonPropertyName("hook_event_subtype")]
    public string? HookEventSubtype { get; init; }

    [JsonPropertyName("tool_name")]
    public string? ToolName { get; init; }

    [JsonPropertyName("tool_input")]
    public Dictionary<string, JsonElement>? ToolInput { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

internal sealed record PermissionDecision
{
    [JsonPropertyName("behavior")]
    public string Behavior { get; set; } = "deny";
}

internal sealed record HookOutput
{
    [JsonPropertyName("hookSpecificOutput")]
    public HookSpecificOutput? HookSpecificOutput { get; init; }
}

internal sealed record HookSpecificOutput
{
    [JsonPropertyName("hookEventName")]
    public string? HookEventName { get; init; }

    [JsonPropertyName("decision")]
    public PermissionDecision? Decision { get; init; }
}

// ── IPC message models ─────────────────────────────────────────────────
internal sealed record IpcMessage
{
    [JsonPropertyName("protocol")]
    public int Protocol { get; init; } = 1;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "toast";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("body")]
    public string Body { get; init; } = "";

    [JsonPropertyName("eventName")]
    public string EventName { get; init; } = "";

    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = "";

    [JsonPropertyName("subType")]
    public string SubType { get; init; } = "";

    /// <summary>Blink behavior: "none" (toast only), "short" (5s, P0.5), "long" (10s, P0).</summary>
    [JsonPropertyName("blinkType")]
    public string BlinkType { get; init; } = "none";
}

internal sealed record IpcResponse
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "ok";

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
