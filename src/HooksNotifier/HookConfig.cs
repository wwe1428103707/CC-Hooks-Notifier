using Microsoft.Win32;

namespace HooksNotifier;

internal static class HookConfig
{
    private const string RegKey = @"Software\ClaudeCode\HooksNotifier\HookConfig";

    /// <summary>All supported hook events with their default enabled state.</summary>
    public static Dictionary<string, (bool Default, string Level, string Description)> AllHooks { get; } = new()
    {
        ["Notification(idle_prompt)"]        = (true,  "P0",   "Task complete notification"),
        ["Notification(permission_prompt)"]  = (true,  "P0",   "Permission prompt notification"),
        ["StopFailure"]                      = (true,  "P0",   "API error alerts"),
        ["Stop"]                             = (true,  "P0.5", "Claude finished responding"),
        ["TaskCompleted"]                    = (true,  "P0.5", "Task marked complete"),
        ["SessionEnd"]                       = (true,  "P0.5", "Session ended"),
        ["Notification(auth_success)"]       = (false, "P1",   "Authentication success"),
        ["Notification(elicitation_dialog)"] = (false, "P1",   "MCP input requested"),
        ["Notification(elicitation_complete)"]=(false,"P1",   "MCP input submitted"),
        ["PermissionDenied"]                 = (false, "P1",   "Tool call denied"),
        ["PostToolUse"]                      = (false, "P1",   "File edited/written"),
        ["PostToolUseFailure"]               = (false, "P1",   "Tool execution failed"),
        ["SubagentStop"]                     = (false, "P1",   "Subagent finished"),
        ["SessionStart"]                     = (false, "P1",   "Session started/resumed"),
        ["PostCompact"]                      = (false, "P1",   "Context compaction complete"),
        ["ConfigChange"]                     = (false, "P1",   "Config file modified"),
        ["SubagentStart"]                    = (false, "P2",   "Subagent launched"),
        ["TaskCreated"]                      = (false, "P2",   "Task created"),
        ["PreCompact"]                       = (false, "P2",   "Context compaction start"),
    };

    /// <summary>Is this hook currently enabled?</summary>
    public static bool IsEnabled(string key)
    {
        if (!AllHooks.ContainsKey(key)) return false;
        try
        {
            using var key2 = Registry.CurrentUser.OpenSubKey(RegKey);
            if (key2?.GetValue(key) is int val) return val == 1;
            return AllHooks[key].Default;
        }
        catch { return AllHooks[key].Default; }
    }

    /// <summary>Set a hook's enabled state.</summary>
    public static void SetEnabled(string key, bool enabled)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RegKey);
            k?.SetValue(key, enabled ? 1 : 0);
        }
        catch { }
    }

    /// <summary>Get all hook states as a dictionary.</summary>
    public static Dictionary<string, bool> GetAllStates()
    {
        var result = new Dictionary<string, bool>();
        foreach (var key in AllHooks.Keys)
            result[key] = IsEnabled(key);
        return result;
    }
}
