# Development Plan — Hooks Notifier v1.2.0

## Branch Strategy

```
main          v1.1.0 ── v1.1.x ── v1.2.0 (merge)
                            │
feature/                  ├── step-1-notification-matchers
hooks-expansion           ├── step-2-stopfailure
                          ├── step-3-permissiondenied
                          ├── step-4-posttooluse
                          ├── step-5-subagent-task-lifecycle
                          ├── step-6-tray-status
                          └── step-7-p0.5-stop-completed-end
```

Each step is a separate feature branch from the same base (`v1.1.0`).  
Merge in order to avoid conflicts.

## Versioning

| Step | Version | Type | Reason |
|------|---------|------|--------|
| Initial | 1.1.0 | baseline | Current state |
| Step 1-7 | 1.2.0 | minor | New features, non-breaking |

## Steps

### Step 1 — 扩展 Notification Matchers
**Branch:** `feature/step-1-notification-matchers`

| File | Change |
|------|--------|
| `HookMode.cs` | Add handlers for `permission_prompt`, `auth_success`, `elicitation_dialog`, `elicitation_complete` notification types |
| `IpcService.cs` | Add `blinkType` field to `IpcMessage` (none / short / long) |
| `TrayMode.cs` | Add `blinkType` parameter to blink methods (short=10 ticks, long=20 ticks) |
| `setup.ps1` | Register Notification matchers: `permission_prompt`, `auth_success`, `elicitation_dialog`, `elicitation_complete` |

**Verify:**
```bash
echo '{"hook_event_name":"Notification","hook_event_type":"permission_prompt"}' | hooks-notifier.exe --hook
echo '{"hook_event_name":"Notification","hook_event_type":"auth_success"}' | hooks-notifier.exe --hook
```

### Step 2 — StopFailure 错误告警
**Branch:** `feature/step-2-stopfailure`

| File | Change |
|------|--------|
| `HookMode.cs` | Add `HandleStopFailure()` — categorize error types (rate_limit, server_error, auth_failed), show different messages |
| `setup.ps1` | Register `StopFailure` with matcher `""` |

**Verify:**
```bash
echo '{"hook_event_name":"StopFailure","hook_event_type":"rate_limit"}' | hooks-notifier.exe --hook
```

### Step 3 — PermissionDenied 工具被拒通知
**Branch:** `feature/step-3-permissiondenied`

| File | Change |
|------|--------|
| `HookMode.cs` | Add `HandlePermissionDenied()` — extract tool name, show Toast |
| `setup.ps1` | Register `PermissionDenied` with matcher `""` |

**Verify:**
```bash
echo '{"hook_event_name":"PermissionDenied","tool_name":"Bash","tool_input":{"command":"rm -rf /"}}' | hooks-notifier.exe --hook
```

### Step 4 — PostToolUse / PostToolUseFailure 文件变更通知
**Branch:** `feature/step-4-posttooluse`

| File | Change |
|------|--------|
| `HookMode.cs` | Add `HandlePostToolUse()` and `HandlePostToolUseFailure()` — show edited file path, failure reason |
| `setup.ps1` | Register `PostToolUse(Edit\|Write)` and `PostToolUseFailure(Bash\|Edit)` |

**Verify:**
```bash
echo '{"hook_event_name":"PostToolUse","tool_name":"Edit","tool_input":{"file_path":"src/test.ts"}}' | hooks-notifier.exe --hook
echo '{"hook_event_name":"PostToolUseFailure","tool_name":"Bash","error":"Command failed"}' | hooks-notifier.exe --hook
```

### Step 5 — Subagent / Task 生命周期通知
**Branch:** `feature/step-5-subagent-task-lifecycle`

| File | Change |
|------|--------|
| `HookMode.cs` | Add `HandleSubagentStart()`, `HandleSubagentStop()`, `HandleTaskCreated()`, `HandleTaskCompleted()` |
| `IpcService.cs` | Add `IpcMessage.Type = "stateful"` for tray status updates |
| `TrayMode.cs` | Accept `stateful` messages, update in-memory counters |
| `setup.ps1` | Register `SubagentStart`, `SubagentStop`, `TaskCreated`, `TaskCompleted` |

**Verify:**
```bash
echo '{"hook_event_name":"SubagentStart","hook_event_type":"Explore"}' | hooks-notifier.exe --hook
echo '{"hook_event_name":"TaskCompleted","hook_event_subtype":"Implement login"}' | hooks-notifier.exe --hook
```

### Step 6 — 托盘菜单状态显示
**Branch:** `feature/step-6-tray-status`

| File | Change |
|------|--------|
| `TrayMode.cs` | Add dynamic status items to ContextMenuStrip: notification count, active subagent, latest task |
| `TrayMode.cs` | Update status items on each `stateful` IPC message |
| `TrayMode.cs` | Add "Clear notifications" menu item |

### Step 7 — P0.5 Stop / TaskCompleted / SessionEnd
**Branch:** `feature/step-7-p0.5-stop-completed-end`

| File | Change |
|------|--------|
| `HookMode.cs` | Add `HandleStop()`, move existing `TaskCompleted` to P0.5 blink, add `HandleSessionEnd()` |
| `IpcService.cs` | `IpcMessage.BlinkType` = `"short"` for P0.5 events |
| `TrayMode.cs` | Support `blinkType: "short"` — 10 ticks instead of 20 |
| `setup.ps1` | Register `Stop(””)`, `SessionEnd(clear|logout|prompt_input_exit)` |

## Commit Checklist (per step)

1. Implement changes in `src/HooksNotifier/`
2. Update `setup.ps1` to register new hook events
3. Update `CLAUDE.md` if needed
4. Update version in `setup.iss` and `.claude-plugin/plugin.json` (only in final step)
5. Rebuild installer at the end
6. Create commit with conventional commit message:
   ```
   feat(step-N): description
   ```
7. Merge to `main`

---

# Development Plan — Hooks Notifier v1.3.0 (P2)

## Overview

Complete remaining P2 hook events coverage: SessionStart, PreCompact/PostCompact, ConfigChange.

| Event | Priority | Type | Description |
|-------|----------|------|-------------|
| `SessionStart` | P2 📢 | Toast | Session started/resumed |
| `PreCompact` | P2 🟢 | stateful | Context compaction beginning |
| `PostCompact` | P2 📢 | Toast | Context compaction completed |
| `ConfigChange` | P2 📢 | Toast | Config file modified externally |

## Branch Strategy

```
master        v1.2.0 ─── step-8 ─── step-9 ─── step-10 ─── v1.3.0
                            │
feature/                  ├── step-8-sessionstart
p2-coverage               ├── step-9-compact
                          └── step-10-configchange
```

## Steps

### Step 8 — SessionStart

**Branch:** `feature/step-8-sessionstart`

| File | Change |
|------|--------|
| `HookMode.cs` | Add `HandleSessionStart()` — show Toast on `startup`/`resume`, stateful update only for `compact`/`clear` |
| `setup.ps1` | Register `SessionStart(” ”)` |

**Notification mapping:**

| event_type | Level | Behavior |
|------------|-------|----------|
| `startup` | 📢 Toast | "Session started" |
| `resume` | 📢 Toast | "Session resumed" |
| `clear` | 🟢 stateful | Silent counter update |
| `compact` | 🟢 stateful | Silent counter update |

### Step 9 — PreCompact / PostCompact

**Branch:** `feature/step-9-compact`

| File | Change |
|------|--------|
| `HookMode.cs` | Add `HandlePreCompact()` — stateful only ("Compacting context..."), add `HandlePostCompact()` — 📢 Toast ("Context compaction complete") |
| `TrayMode.cs` | Add compact state tracking field |
| `setup.ps1` | Register `PreCompact(” ”)`, `PostCompact(” ”)` |

### Step 10 — ConfigChange

**Branch:** `feature/step-10-configchange`

| File | Change |
|------|--------|
| `HookMode.cs` | Add `HandleConfigChange()` — 📢 Toast with file path and change source |
| `setup.ps1` | Register `ConfigChange(” ”)` |

**Notification mapping:**

| source | Level | Message |
|--------|-------|---------|
| `user_settings` | 📢 | "User settings modified" |
| `project_settings` | 📢 | "Project settings modified" |
| `local_settings` | 📢 | "Local settings modified" |
| `policy_settings` | 📢 | "Policy settings modified" |
| `skills` | 📢 | "Skills configuration changed" |

## Version

| Step | Version | Type | Reason |
|------|---------|------|--------|
| Initial | 1.2.0 | baseline | v1.2.0 complete |
| Step 8-10 | 1.3.0 | minor | New hook event coverage |

---

# Development Plan — i18n Multi-Language Support (v2.0.0)

## Overview

Add Chinese/English language switching capability with a scalable i18n system that supports adding more languages without code changes.

## Architecture

```
src/HooksNotifier/
├── i18n/
│   ├── I18n.cs              # Translation service (singleton)
│   ├── en.json               # English string table
│   └── zh.json               # Chinese string table
└── (all other files)         # Use I18n.Get("key") instead of hardcoded strings
```

### Design Principles

| Principle | Implementation |
|-----------|---------------|
| **Zero recompilation** for new languages | New `.json` file only |
| **Runtime switching** | Tray menu → language submenu → immediate refresh |
| **System language auto-detect** | `CultureInfo.CurrentUICulture` fallback chain |
| **Key-based lookup** | `I18n.Get("toast.task_complete")` instead of hardcoded strings |
| **Format string support** | `I18n.Get("tool.denied", toolName)` → `"Tool call denied: Bash"` |
| **Persist preference** | Save selected language to `HKCU\Software\ClaudeCode\HooksNotifier\Language` |

### String Key Naming Convention

```
{domain}.{specific}
```

| Domain | Examples |
|--------|---------|
| `toast.*` | `toast.idle_prompt`, `toast.auth_success`, `toast.session_started` |
| `error.*` | `error.rate_limit`, `error.server_error`, `error.auth_failed` |
| `tool.*` | `tool.edit_file`, `tool.denied`, `tool.mcp_denied` |
| `menu.*` | `menu.configure_hooks`, `menu.open_at_login`, `menu.exit` |
| `dialog.*` | `dialog.perm_title`, `dialog.allow`, `dialog.deny` |
| `status.*` | `status.subagent`, `status.task`, `status.notifications` |
| `installer.*` | Strings for the Inno Setup installer wizard |

### String File Format (`en.json`)

```json
{
  "language": {
    "code": "en",
    "name": "English"
  },
  "toast": {
    "idle_prompt": "Task complete — ready for your input",
    "permission_prompt": "Claude is waiting for you to approve a tool call",
    "auth_success": "Authentication successful",
    "session_started": "Session started",
    "session_resumed": "Session resumed",
    "context_compacted": "Context compaction complete",
    "tool_edited": "Edited: {path}",
    "tool_denied": "Tool call denied: {tool}",
    "tool_mcp_denied": "MCP tool denied: {tool}",
    "tool_failed": "Tool failed: {tool}",
    "cmd_failed": "Command failed — see terminal for details",
    "subagent_finished": "Subagent finished: {agent}",
    "task_completed": "Task completed: {desc}",
    "session_ended": "Session ended",
    "config_modified": "{source} modified",
    "config_file_modified": "{source}: {file}"
  },
  "error": {
    "rate_limit": "API rate limit reached. Claude may retry shortly.",
    "server_error": "Claude API encountered a server error.",
    "auth_failed": "Authentication failed. Check your API credentials.",
    "billing_error": "There is a billing issue with your API account.",
    "max_tokens": "Response was truncated (max output tokens reached).",
    "model_not_found": "Requested model is not available.",
    "unknown": "API error: {type}"
  },
  "dialog": {
    "perm_title": "Claude needs authorization",
    "allow": "Allow",
    "deny": "Deny",
    "tool_label": "Tool: {tool}",
    "no_details": "(no details)"
  },
  "menu": {
    "running": "Hooks Notifier — running",
    "notifications": "Notifications: {count}",
    "subagent": "Subagent: {status}",
    "subagent_idle": "Subagent: IDLE",
    "task": "Task: {status}",
    "task_idle": "Task: IDLE",
    "configure_hooks": "Configure Hooks",
    "update_hook_path": "Update Hook Path",
    "clear_counters": "Clear counters",
    "open_at_login": "Open at Login",
    "language": "Language",
    "about": "About...",
    "exit": "Exit"
  },
  "about": {
    "title": "Claude Code Hooks Notifier",
    "version": "v{version}\nBell icon tray + toast notifications"
  }
}
```

## Branch Strategy

```
master        v1.3.0 ─── step-11 ─── step-12 ─── step-13 ─── v2.0.0
                            │
feature/                  ├── step-11-i18n-engine
i18n                      ├── step-12-localize-ui
                          └── step-13-localize-installer
```

## Steps

### Step 11 — i18n Engine (Core)

**Branch:** `feature/step-11-i18n-engine`

| File | Change |
|------|--------|
| `src/HooksNotifier/i18n/en.json` | Create — all English strings (base file) |
| `src/HooksNotifier/i18n/zh.json` | Create — Chinese translations |
| `src/HooksNotifier/i18n/I18n.cs` | Create — translation service class |
| `.csproj` | Add `i18n/*.json` as `EmbeddedResource` or `Content` |

**I18n.cs API:**

```csharp
internal static class I18n
{
    static I18n() => Load(SystemCulture);

    public static string CurrentLanguage { get; private set; }  // "en" / "zh"
    public static string[] AvailableLanguages => ["en", "zh"];

    public static string Get(string key);
    public static string Get(string key, params object?[] args);
    public static void SetLanguage(string code);  // runtime switch
}
```

**Lookup strategy:**
1. Exact match from current language file
2. Fallback to `en.json` if key missing in target
3. Return `"?{key}?"` if missing in both (visible missing-key indicator)

### Step 12 — Localize All UI Strings

**Branch:** `feature/step-12-localize-ui`

Replace every hardcoded UI string across all files with `I18n.Get(...)` calls.

**Files to modify:**

| File | Strings to replace |
|------|-------------------|
| `HookMode.cs` | All `title`/`body` strings in event handlers, dialog labels |
| `PermissionDialog` (in HookMode.cs) | Title, button text, labels |
| `TrayMode.cs` | Menu items, status text, About balloon, notifications |
| `ToastService.cs` | (no changes needed — uses string params) |
| `Program.cs` | Usage text (optional, or keep bilingual) |

**Tray menu update — add Language submenu:**

```
Hooks Notifier — running
Notifications: 0
Subagent: IDLE
Task: IDLE
──────────────────────────
Configure Hooks
Update Hook Path
Clear counters
Open at Login
Language
  → English      ● (current)
  → 中文
About...
──────────────────────────
Exit
```

### Step 13 — Localize Installer

**Branch:** `feature/step-13-localize-installer`

| File | Change |
|------|--------|
| `setup.iss` | Add `[Languages]` section with English + Chinese Simplified |
| `setup.iss` | Add Chinese language `.isl` file from Inno Setup installation |
| `setup.iss` | Translate wizard messages for Chinese |

**Inno Setup multi-language example:**

```iss
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinese"; MessagesFile: "compiler:ChineseSimplified.isl"

[CustomMessages]
english.WelcomeLabel2 = This will install [name] on your computer.
chinese.WelcomeLabel2 = 此程序将安装 [name] 到您的计算机。
```

**Note:** ChineseSimplified.isl may not ship with Inno Setup by default. If missing, it needs to be downloaded separately from the Inno Setup website and placed in the `Languages` folder.

## Version

| Step | Version | Type | Reason |
|------|---------|------|--------|
| Initial | 1.3.0 | baseline | Current state |
| Step 11 | 1.3.0 | (internal) | i18n engine added, no visible change |
| Step 12 | 1.4.0 | minor | UI now bilingual via runtime switch |
| Step 13 | 2.0.0 | minor | Installer supports language selection |

## Future Language Addition

To add a new language (e.g., Japanese):

1. Copy `en.json` → `ja.json`
2. Translate all values (leave keys unchanged)
3. Add `"ja"` to `AvailableLanguages` in `I18n.cs`
4. Build — no other code changes needed
