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
