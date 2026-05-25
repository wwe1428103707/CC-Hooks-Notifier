# C# -- JS Bridge Interface Contract

**File:** `D:\CC Hooks Notifier\docs\INTERFACE_CONTRACT-cs-js-bridge.md`
**Version:** v1.12.0
**Scope:** WebView2 postMessage bridge between `MainWindow.cs` (C# host) and `App.tsx` (React UI).

---

## 1. Transport

- **Channel:** `CoreWebView2.PostWebMessageAsJson(string)` (C# to JS) and `chrome.webview.postMessage(string)` (JS to C#).
- **Format:** UTF-8 JSON, single-line, no trailing newline required.
- **Direction:** Bidirectional. All C# to JS messages are `{ type: string, payload: T }`. All JS to C# messages are `{ type: string, payload?: T }`.
- **Timing:** Messages are dispatched on the UI thread. C# to JS is fire-and-forget (no ACK). JS to C# is synchronous on the WebView2 message pump (heavy work is offloaded to `Task.Run` internally).

---

## 2. C# to JS Messages

### 2.1 `state_sync` -- Full state snapshot

Sent on: WebView initialization, explicit `get_state` request, and after every mutation (mark_read, mark_all_read, clear_history, set_max_entries, set_lang).

```json
{
  "type": "state_sync",
  "payload": {
    "counts": {
      "total": 0,
      "p0": 0,
      "p05": 0,
      "toast": 0,
      "stateful": 0
    },
    "unreadCount": 0,
    "subagentCount": 0,
    "taskCount": 0,
    "recentEvents": [
      {
        "timestamp": "HH:mm:ss",
        "level": "P0|P0.5|Toast|Stateful",
        "eventName": "string",
        "summary": "string",
        "detail": "string",
        "isRead": false
      }
    ],
    "allEvents": [
      {
        "timestamp": "HH:mm:ss",
        "level": "P0|P0.5|Toast|Stateful",
        "eventName": "string",
        "summary": "string",
        "detail": "string",
        "isRead": false,
        "_idx": 0
      }
    ],
    "maxEntries": 500,
    "language": "en",
    "hookConfig": {
      "Notification(idle_prompt)": true,
      "Notification(permission_prompt)": false
    }
  }
}
```

**Field semantics:**

| Field | Type | Notes |
|-------|------|-------|
| `counts` | object | Breakdown of all events by level. `total` = sum of all level buckets. |
| `counts.total` | number | Total events in history (0..MaxEntries). |
| `counts.p0` | number | Count of events with level `"P0"`. |
| `counts.p05` | number | Count of events with level `"P0.5"`. |
| `counts.toast` | number | Count of events with level `"Toast"`. |
| `counts.stateful` | number | Count of events with any other level (e.g. `"Stateful"`). |
| `unreadCount` | number | Total unread events. Computed by `EventHistory.UnreadCount`. |
| `subagentCount` | number | Lifetime subagent starts (from TrayMode). |
| `taskCount` | number | Lifetime task creations (from TrayMode). |
| `recentEvents` | EventRow[] | Last 5 events (newest first). No `_idx`. |
| `allEvents` | EventRow[] | Last 100 events in reverse chronological order (newest first). Each row carries `_idx` for `mark_read`. |
| `maxEntries` | number | Current history cap (50..10000, default 500). |
| `language` | string | `"en"` or `"zh"`. |
| `hookConfig` | object | Key-value map of hook event name to enabled boolean. All 19 hook keys always present. |

**EventRow schema (used in both `recentEvents` and `allEvents`):**

```typescript
{
  timestamp: string;   // "HH:mm:ss" formatted
  level: string;       // "P0" | "P0.5" | "Toast" | "Stateful"
  eventName: string;   // e.g. "Notification(idle_prompt)"
  summary: string;     // Human-readable title/body text
  detail: string;      // Extended detail (may be empty)
  isRead: boolean;     // Read state
  _idx?: number;       // Index in the ring buffer (only in allEvents, used for mark_read)
}
```

---

### 2.2 `event_push` -- Real-time event

Sent on: Every IPC message handled by the tray process. The payload is the same as a single EventRow, plus `unreadCount`.

```json
{
  "type": "event_push",
  "payload": {
    "timestamp": "HH:mm:ss",
    "level": "P0|P0.5|Toast|Stateful",
    "eventName": "string",
    "summary": "string",
    "detail": "string",
    "isRead": false,
    "unreadCount": 5
  }
}
```

| Field | Type | Notes |
|-------|------|-------|
| `unreadCount` 🆕 | number | Total unread count **after** this event was added. Only present in `event_push`, not in individual EventRow objects within `state_sync`. |

**Note:** `event_push` payload does NOT include `_idx` because the JS side prepends it to both `recentEvents` and `allEvents` arrays locally, deriving the index position from the next `state_sync`.

---

### 2.3 `lang_changed`

Sent after `set_lang` mutation completes.

```json
{
  "type": "lang_changed",
  "payload": "en|zh"
}
```

---

### 2.4 `configure_hooks_result`

Sent after the async `update_hook_path` operation completes (success or failure).

```json
{
  "type": "configure_hooks_result",
  "payload": {
    "success": true,
    "message": "Hook path updated"
  }
}
```

| Field | Type | Notes |
|-------|------|-------|
| `success` | boolean | Exit code of the `--configure-hooks` child process. |
| `message` | string | Human-readable result, with `"ERROR:"` prefix stripped if present. |

**Note:** The JS side auto-clears `_feedback` after 4 seconds (see `App.tsx` line 469).

---

### 2.5 `hook_config`

Sent after `get_hook_config` or `set_hook_config` mutations.

```json
{
  "type": "hook_config",
  "payload": {
    "Notification(idle_prompt)": true,
    "Notification(permission_prompt)": false,
    "StopFailure": false,
    "Stop": true,
    "TaskCompleted": true,
    "SessionEnd": true,
    "Notification(auth_success)": true,
    "Notification(elicitation_dialog)": true,
    "Notification(elicitation_complete)": true,
    "PermissionDenied": false,
    "PostToolUse(Edit|Write)": false,
    "PostToolUseFailure(Bash|Edit)": false,
    "SubagentStop": true,
    "SessionStart": true,
    "PostCompact": true,
    "ConfigChange": true,
    "SubagentStart": true,
    "TaskCreated": true,
    "PreCompact": true
  }
}
```

All 19 known hook keys are guaranteed to be present. Keys not found in `settings.json` default to `false`.

---

## 3. JS to C# Messages

### 3.1 `get_state`

Request a full state refresh. Triggers `state_sync` response.

```json
{ "type": "get_state" }
```

**Sent:** On initial WebView load (line 490 of App.tsx). Also available for any explicit refresh.

---

### 3.2 `set_lang`

Change language. Triggers `lang_changed` response.

```json
{ "type": "set_lang", "payload": "en|zh" }
```

---

### 3.3 `update_hook_path`

Trigger async hook path configuration. Spawns `--configure-hooks` child process. Triggers `configure_hooks_result` response on completion.

```json
{ "type": "update_hook_path" }
```

**Important:** This handler runs on a background thread (`Task.Run`). The result is marshalled back to the UI thread via `BeginInvoke`.

---

### 3.4 `get_hook_config`

Get current hook configuration. Triggers `hook_config` response.

```json
{ "type": "get_hook_config" }
```

---

### 3.5 `set_hook_config`

Toggle a hook event on or off in `~/.claude/settings.json`. Triggers `hook_config` response.

```json
{
  "type": "set_hook_config",
  "payload": {
    "key": "Notification(idle_prompt)",
    "enabled": true
  }
}
```

| Field | Type | Constraints |
|-------|------|-------------|
| `key` | string | Must be one of the 19 keys from `HookConfig.AllHooks`. |
| `enabled` | boolean | `true` = add hook entry to `settings.json`; `false` = remove hook entry. |

---

### 3.6 `mark_all_read` 🆕

Mark all events as read. Triggers `state_sync` response.

```json
{ "type": "mark_all_read" }
```

**Side effects:**
- `EventHistory.MarkAllRead()` sets `IsRead = true` on all entries.
- `TrayMode.UpdateTooltip()` updates tray tooltip text.

---

### 3.7 `mark_read` 🆕

Mark a single event as read by its ring-buffer index. Triggers `state_sync` response.

```json
{
  "type": "mark_read",
  "payload": { "index": 42 }
}
```

| Field | Type | Constraints |
|-------|------|-------------|
| `index` | number | 0-based index into `EventHistory.Entries`. Must be within valid bounds; out-of-range indices are silently ignored. |

**Note:** The JS side sends this when a user clicks on an unread row (line 268 of App.tsx). The `_idx` value from `state_sync` `allEvents` is used as the index.

---

### 3.8 `set_max_entries` 🆕

Change the maximum number of events stored. Triggers `state_sync` response.

```json
{
  "type": "set_max_entries",
  "payload": { "value": 500 }
}
```

| Field | Type | Constraints |
|-------|------|-------------|
| `value` | number | Clamped to `[50, 10000]`. Persisted to registry (`HKCU\Software\ClaudeCode\HooksNotifier\MaxEntries`). |

**Side effects:** If `value` is less than current entry count, excess entries are trimmed from the front (oldest first). The trimmed entries are lost permanently.

---

### 3.9 `clear_history`

Delete all events. Triggers `state_sync` response.

```json
{ "type": "clear_history" }
```

**Side effects:**
- `EventHistory.Clear()` empties the in-memory list and deletes `event_history.json` on disk.
- Unread count resets to 0.

---

### 3.10 `open_settings`

Open `~/.claude/settings.json` in the default system text editor. No response message.

```json
{ "type": "open_settings" }
```

**Implementation:** Uses `Process.Start` with `UseShellExecute = true` to invoke the OS file association.

---

## 4. TypeScript Type Definitions

The following types are defined in `webui/src/App.tsx` and should be kept in sync with the C# backend.

### 4.1 Counts

```typescript
interface Counts {
  total: number;
  p0: number;
  p05: number;
  toast: number;
  stateful: number;
}
```

### 4.2 EventRow

```typescript
interface EventRow {
  timestamp: string;      // "HH:mm:ss" — always 8 characters
  level: string;           // "P0" | "P0.5" | "Toast" | "Stateful"
  eventName: string;       // Fully qualified hook name e.g. "StopFailure"
  summary: string;         // Human-readable event summary
  detail?: string;         // Optional extended detail (defaults to "")
  isRead?: boolean;        // Read state (defaults to false)
  _idx?: number;           // ONLY present in allEvents[], NOT in recentEvents[] or event_push
}
```

### 4.3 Feedback

```typescript
interface Feedback {
  success: boolean;
  message: string;
}
```

### 4.4 AppState

```typescript
interface AppState {
  counts: Counts;
  unreadCount: number;
  subagentCount: number;
  taskCount: number;
  recentEvents: EventRow[];
  allEvents: EventRow[];
  language: string;          // "en" | "zh"
  maxEntries?: number;       // Default 500
  hookConfig?: Record<string, boolean>;  // 19-entry map of hook name to enabled state
  defaultFilter?: string;    // Optional initial filter tab (currently unused from C#)
  _feedback?: Feedback | null;  // configure_hooks_result, auto-cleared after 4s
}
```

### 4.5 CsMessage (Union Type)

The full union of messages that JS can receive from C# (this is not explicitly defined as a TypeScript union in the codebase, but is handled by the `handleCsMessage` function):

```typescript
type CsMessage =
  // Full state snapshot — handled by replacing entire AppState
  | { type: "state_sync"; payload: AppState }

  // Real-time event — handled by prepending to arrays
  | { type: "event_push"; payload: EventRow & { unreadCount: number } }

  // Language change
  | { type: "lang_changed"; payload: "en" | "zh" }

  // Hook path configuration result
  | { type: "configure_hooks_result"; payload: Feedback }

  // Hook configuration state
  | { type: "hook_config"; payload: Record<string, boolean> };
```

### 4.6 JsMessage (Sent to C#)

Messages sent from JS to C# follow this pattern (not explicitly union-typed in the codebase):

```typescript
type JsMessage =
  | { type: "get_state" }
  | { type: "clear_history" }
  | { type: "update_hook_path" }
  | { type: "get_hook_config" }
  | { type: "open_settings" }
  | { type: "mark_all_read" }                                          // 🆕
  | { type: "set_lang"; payload: "en" | "zh" }
  | { type: "set_hook_config"; payload: { key: string; enabled: boolean } }
  | { type: "mark_read"; payload: { index: number } }                  // 🆕
  | { type: "set_max_entries"; payload: { value: number } };           // 🆕
```

---

## 5. C# Method Signatures

### 5.1 EventHistory (static class)

```csharp
// Add a new entry (thread-safe via lock)
public static void Add(EventEntry entry);

// Get the last N entries (thread-safe)
public static EventEntry[] GetRecent(int count);

// Get the entire ring buffer (thread-safe, returns a snapshot)
public static IReadOnlyList<EventEntry> Entries { get; }

// Total unread count (thread-safe)
public static int UnreadCount { get; }

// Unread count filtered by level (thread-safe)
public static int UnreadCountByLevel(string level);

// Mark ALL entries as read (thread-safe, persists)
public static void MarkAllRead();                                              // 🆕

// Mark a single entry by 0-based index (thread-safe, persists)
public static void MarkRead(int index);                                         // 🆕

// Get all unread entries (thread-safe)
public static List<EventEntry> GetUnread();

// Get count breakdown by level
public static (int total, int p0, int p05, int toast, int stateful) Counts { get; }

// Max entries config (persisted to registry, 50..10000)
public static int MaxEntries { get; set; }                                      // 🆕

// Clear all entries
public static void Clear();
```

### 5.2 EventEntry (record)

```csharp
internal sealed record EventEntry(
    DateTime Timestamp,
    string Level,           // "P0" | "P0.5" | "Toast" | "Stateful"
    string EventName,
    string Summary,
    string Detail = "",
    bool IsRead = false     // 🆕 (added in v1.12.0)
);
```

### 5.3 TrayMode (static class)

```csharp
// Public state
public static int SubagentCount { get; }
public static int TaskCount { get; }

// Update tray tooltip to show unread count
public static void UpdateTooltip();                                             // 🆕
```

### 5.4 MainWindow (instance class, the WebView2 host)

```csharp
// Push a single event to JS (used by TrayMode IPC handler)
public void PushEvent(EventEntry entry);

// Push any typed message to JS (used for state_sync, lang_changed, etc.)
public void PushState(string type, object payload);

// Build the full state object from EventHistory + TrayMode + I18n + HookConfig
private object GetCurrentState();

// Entry point for all JS-to-C# messages
private void OnJsMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e);
```

### 5.5 HookConfig (static class)

```csharp
// All 19 known hook events with their severity level
public static Dictionary<string, string> AllHooks { get; }  // key → "P0"|"P0.5"|"P1"|"P2"

// Read current states from settings.json
public static Dictionary<string, bool> GetAllStates();

// Enable a hook (write to settings.json)
public static void Enable(string key);

// Disable a hook (remove from settings.json)
public static void Disable(string key);
```

---

## 6. Constraints

### 6.1 Backward Compatibility

- New fields appended to existing message payloads must not break the JS consumer. The JS `handleCsMessage` function in `App.tsx` does a switch on `type` -- every handler must still work when new keys appear in the payload.
- The `state_sync` payload replaces the entire `AppState` on the JS side. Adding new top-level keys to the payload is safe; the JS state merge (`setState(msg.payload)`) will include them.
- Removing or renaming existing keys is a BREAKING change. This requires a coordinated C# + JS update.
- New message types added to the `CsMessage` union must be added to the `handleCsMessage` switch in `App.tsx`. Unknown types are silently ignored.
- The `protocol` field in `IpcMessage` (inter-process, not WebView) is currently always `1`. If incremented, the tray's IPC handler may need to interpret differently, but this is invisible to the JS bridge.

### 6.2 Thread Safety

- `EventHistory` uses `lock (_entries)` for all read/write operations. It is safe to call from any thread.
- `TrayMode` properties (`SubagentCount`, `TaskCount`) are simple int fields. They are written from the UI thread (via `_uiContext.Post`) but may be read from any thread. Under extreme race the read may be stale, but never corrupt.
- `MainWindow.PushEvent` and `PushState` check `IsDisposed` and `_loaded` flags before posting to WebView2. These flags are set on the UI thread.
- JS-to-C# message handling (`OnJsMessage`) runs on the WebView2 message pump thread. Long-running operations (`update_hook_path`) are offloaded to `Task.Run` with result marshalled back via `BeginInvoke`.

### 6.3 Idempotency

- `get_state` is idempotent. The JS side may call it repeatedly.
- `mark_read(index)` is idempotent: calling it twice on the same index is a no-op (the `IsRead` flag is already `true`).
- `mark_all_read` is idempotent.
- `clear_history` on an already empty history is a no-op.
- `set_lang` with the same current language is a no-op but still triggers `lang_changed`.
- `set_hook_config` with unchanged state is idempotent at the `HookConfig` level (it checks for existing entries before adding), but will still emit `hook_config` response.

### 6.4 Index Semantics

- `_idx` in `allEvents` entries is the 0-based index into `EventHistory.Entries` (the C# `List<EventEntry>`).
- The order in `allEvents` (from `GetCurrentState()`) is **reverse chronological** (newest first), but `_idx` reflects the **original position** in the ring buffer. This means that entries with higher `_idx` values are older.
- After `clear_history`, indices restart from 0. After `trimExcess` (when `MaxEntries` is reduced), indices of surviving entries are unchanged (oldest entries are removed from index 0).
- The `mark_read` JS-to-C# message sends the `_idx` value as-is. The C# side validates: `if (index < 0 || index >= _entries.Count) return;`.
- `_idx` is NOT present in `event_push` payloads or in `recentEvents` within `state_sync`.

### 6.5 HookConfig Key Stability

The 19 keys in `HookConfig.AllHooks` are the single source of truth. JS mirrors these in `hookMeta` and `hookLevels` maps in `App.tsx`. If a new hook is added to the C# side, it must be simultaneously added to:
1. `HookConfig.AllHooks` dictionary
2. `webui/src/App.tsx` `hookMeta` and `hookLevels` maps
3. `webui/src/i18n.ts` en/zh translation tables

### 6.6 Version Coupling

The version string appears in:

| File | Field |
|------|-------|
| `src/HooksNotifier/TrayMode.cs` | `I18n.Get("about.version", "x.y.z")` |
| `webui/src/App.tsx` | `t("about.version", "x.y.z")` |
| `webui/src/i18n.ts` | `"header.version": "vx.y.z"` (en and zh) |

The C# and JS versions must always match. A mismatch implies the bridge contract may be out of sync.

---

## 7. Message Flow Diagrams

### 7.1 Initial Load

```
WebView2 Ready
  ├── PushState("state_sync", GetCurrentState())    // auto-sync on page load
  └── JS: sendToCs({ type: "get_state" })           // redundant safety request
        └── C#: PushState("state_sync", GetCurrentState())
```

### 7.2 Real-time Event (IPC → Tray → JS)

```
hooks-notify.exe
  └── Named pipe → IpcService.StartServer()
        └── TrayMode.OnIpcMessage()
              ├── EventHistory.Add(entry)
              ├── TrayMode.UpdateTooltip()
              ├── TrayMode.UpdateUnreadMenu()
              └── MainWindow.PushEvent(entry)
                    └── WebView2: { type: "event_push", payload: { ... } }
                          └── App.tsx: handleCsMessage → prepend to arrays, update unreadCount
```

### 7.3 User Action (JS → C# → JS)

```
User clicks "Mark All Read"
  └── sendToCs({ type: "mark_all_read" })
        └── OnJsMessage:
              ├── EventHistory.MarkAllRead()
              ├── TrayMode.UpdateTooltip()
              └── PushState("state_sync", GetCurrentState())
                    └── WebView2: { type: "state_sync", payload: { ... } }
                          └── App.tsx: handleCsMessage → replace AppState
```
