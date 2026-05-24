# Interface Contract — 托盘通知中心 C# ↔ JS

> 版本: v1.0  
> 日期: 2026-05-24  
> 状态: Locked (S0 完成)  
> 所有新增/变更字段以 `🆕` 标记

---

## 1. C# → JS 消息

### 1.1 state_sync（初始化 + 全量状态同步）

**触发时机：** WebView2 加载完成 (`_loaded = true`)、JS 端 `get_state` 请求、`mark_all_read`/`mark_read` 完成、`clear_history` 完成。

```jsonc
{
  "type": "state_sync",
  "payload": {
    // ── 现有字段（不变） ──────────────────────────────────
    "counts": {
      "total": 128,
      "p0": 12,
      "p05": 8,
      "toast": 89,
      "stateful": 19
    },
    "subagentCount": 5,
    "taskCount": 2,
    "language": "en",
    "hookConfig": {
      "Notification(idle_prompt)": true,
      "StopFailure": true
      // ... 19 keys total
    },

    // ── 🆕 新增字段 ──────────────────────────────────────
    "unreadCount": 3,

    // ── 变更字段 ─────────────────────────────────────────
    "recentEvents": [
      {
        "timestamp": "14:32:01",
        "level": "P0",
        "eventName": "StopFailure",
        "summary": "Claude Code process exited abnormally",
        "detail": "Exit code: 1",
        "isRead": false          // 🆕
      }
    ],
    "allEvents": [
      {
        "timestamp": "14:32:01",
        "level": "P0",
        "eventName": "StopFailure",
        "summary": "Claude Code process exited abnormally",
        "detail": "Exit code: 1",
        "isRead": false          // 🆕
        // 🆕 index 字段: 可选。C# 端 _entries 正向索引，前端用于 mark_read。
        // 如果不传 index，前端用 timestamp+eventName 组合定位。
        "_idx": 127
      }
    ],
    // 🆕 托盘进入标记: 当用户从托盘单击进入时，前端应默认展示"未读"筛选
    "defaultFilter": "unread"    // "all" | "unread"
  }
}
```

### 1.2 event_push（实时事件推送）

**触发时机：** `TrayMode.OnIpcMessage` 收到新 IPC 消息并记录到 `EventHistory` 后。

```jsonc
{
  "type": "event_push",
  "payload": {
    "timestamp": "14:35:22",
    "level": "P0.5",
    "eventName": "TaskCompleted",
    "summary": "Task completed: fix-login-bug",
    "detail": "",
    "isRead": false,           // 🆕 新事件始终为 false
    "unreadCount": 4           // 🆕 当前未读总数（方便前端即时更新 badge）
  }
}
```

### 1.3 hook_config（不变）

已存在于 `MainWindow.OnJsMessage` 的 `get_hook_config` / `set_hook_config` 处理中，无需变更。

### 1.4 lang_changed（不变）

无需变更。

### 1.5 configure_hooks_result（不变）

无需变更。

---

## 2. JS → C# 消息

### 2.1 现有消息（不变）

| type | payload | 说明 |
|------|---------|------|
| `get_state` | — | 请求全量状态 |
| `set_lang` | `"en"` \| `"zh"` | 切换语言 |
| `update_hook_path` | — | 更新 hook 路径 |
| `get_hook_config` | — | 请求 hook 配置 |
| `set_hook_config` | `{ key, enabled }` | 切换单个 hook |
| `clear_history` | — | 清空历史 |
| `open_settings` | — | 打开 settings.json |

### 2.2 🆕 mark_all_read

```jsonc
{
  "type": "mark_all_read"
}
// 无 payload
```

**C# 处理：** `EventHistory.MarkAllRead()` → `PushState("state_sync", GetCurrentState())`

**GetCurrentState 返回的 defaultFilter：** `"all"`（面板内部操作，不强制筛选）

### 2.3 🆕 mark_read

```jsonc
{
  "type": "mark_read",
  "payload": {
    "index": 127
  }
}
```

**`index` 语义：** `EventHistory.Entries` (即 `_entries`) 的正向索引（0-based，从最早到最新）。

**C# 处理：** `EventHistory.MarkRead(payload.index)` → `PushState("state_sync", GetCurrentState())`

**GetCurrentState 返回的 defaultFilter：** `"all"`（单条操作，不切换筛选）

**错误处理：** index 越界时静默忽略（不崩溃、不通知前端）。

---

## 3. TypeScript 类型定义（前端参考）

```typescript
// ── 🆕 更新后的类型 ──────────────────────────────────────
interface Counts {
  total: number
  p0: number
  p05: number
  toast: number
  stateful: number
}

interface EventRow {
  timestamp: string          // "HH:mm:ss"
  level: string              // "P0" | "P0.5" | "Toast" | "Stateful"
  eventName: string
  summary: string
  detail?: string
  isRead?: boolean           // 🆕
  _idx?: number              // 🆕 C# 端索引，用于 mark_read
}

interface AppState {
  counts: Counts
  unreadCount: number        // 🆕
  subagentCount: number
  taskCount: number
  recentEvents: EventRow[]
  allEvents: EventRow[]
  language: string
  hookConfig?: Record<string, boolean>
  defaultFilter?: string     // 🆕 "all" | "unread"
  _feedback?: Feedback | null
}

// 🆕 新增 JS→C# 消息类型
type CsMessage =
  | { type: "get_state" }
  | { type: "set_lang"; payload: string }
  | { type: "update_hook_path" }
  | { type: "get_hook_config" }
  | { type: "set_hook_config"; payload: { key: string; enabled: boolean } }
  | { type: "clear_history" }
  | { type: "open_settings" }
  | { type: "mark_all_read" }                                          // 🆕
  | { type: "mark_read"; payload: { index: number } }                  // 🆕
```

---

## 4. C# 类型参考

```csharp
// Models.cs — EventEntry（🆕 IsRead 字段）
internal sealed record EventEntry(
    DateTime Timestamp,
    string Level,
    string EventName,
    string Summary,
    string Detail = "",
    bool IsRead = false       // 🆕
);

// EventHistory 新增成员签名
public static int UnreadCount { get; }                       // 🆕
public static int UnreadCountByLevel(string level);          // 🆕
public static void MarkAllRead();                            // 🆕
public static void MarkRead(int index);                      // 🆕
public static List<EventEntry> GetUnread();                  // 🆕

// TrayMode 新增成员
private static void UpdateTooltip();                         // 🆕
private static void OpenDashboard();                         // 🆕 提取自双击逻辑 + 已读清除
private static ToolStripMenuItem? _unreadMenuItem;           // 🆕 动态菜单项引用
```

---

## 5. 不变量 / 约束

| 约束 | 说明 |
|------|------|
| **向后兼容** | 旧 `event_history.json` 无 `IsRead` 字段，反序列化时 `= false` 默认值自动兼容 |
| **线程安全** | `EventHistory` 所有新增方法内部加 `lock(_entries)` |
| **消息幂等** | `mark_all_read` 多次调用无副作用 |
| **索引稳定** | `_idx` 只在 `state_sync` 全量推送时传递，`event_push` 不传（新事件前端从 `allEvents` 头部推断） |
| **defaultFilter** | 仅 `state_sync` 消息携带；`event_push` 不改变筛选状态 |

---

## 6. 变更摘要

| 消息 | 变更类型 | 详情 |
|------|---------|------|
| `state_sync` | **扩展** | payload 增加 `unreadCount`, `defaultFilter`；allEvents/recentEvents 每项增加 `isRead`, `_idx` |
| `event_push` | **扩展** | payload 增加 `isRead`, `unreadCount` |
| `mark_all_read` | **新增** | JS→C#，无 payload |
| `mark_read` | **新增** | JS→C#，payload: `{ index: number }` |

所有现有消息类型不变。旧版前端忽略新增字段（JSON 反序列化容错）。旧版 C# 收到未知消息 type 时静默忽略（现有 `catch { }` 块）。
