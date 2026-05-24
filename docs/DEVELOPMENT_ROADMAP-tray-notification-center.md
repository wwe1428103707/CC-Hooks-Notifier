# 托盘通知中心 — 技术开发路线文档

> 版本: v1.0  
> 日期: 2026-05-24  
> 基于需求报告: [REQUIREMENTS-tray-notification-center.md](./REQUIREMENTS-tray-notification-center.md)

---

## 目录

1. [总体架构概览](#1-总体架构概览)
2. [任务分解](#2-任务分解)
3. [依赖关系图](#3-依赖关系图)
4. [并行开发策略](#4-并行开发策略)
5. [开发阶段与时间线](#5-开发阶段与时间线)
6. [测试策略](#6-测试策略)
7. [风险与缓解](#7-风险与缓解)

---

## 1. 总体架构概览

### 1.1 变更影响范围

```
┌─────────────────────────────────────────────────────────┐
│                    变更涉及的文件                         │
├───────────────────┬─────────────────────────────────────┤
│ C# 后端 (5 文件)   │ Models.cs                           │
│                   │ EventHistory.cs                     │
│                   │ TrayMode.cs                         │
│                   │ MainWindow.cs                       │
│                   │ en.json / zh.json                   │
├───────────────────┼─────────────────────────────────────┤
│ React 前端 (2 文件)│ App.tsx                             │
│                   │ i18n.ts                             │
└───────────────────┴─────────────────────────────────────┘
```

### 1.2 数据流

```
hooks-notify.exe ──IPC──▶ TrayMode.OnIpcMessage()
                               │
                               ├── EventHistory.Add(entry)    ← IsRead=false
                               ├── StartBlinking(persistent)  ← 持续，不自动停止
                               ├── UpdateTooltip(unreadCount) ← 动态 tooltip
                               └── MainWindow.PushEvent()     ← 含 isRead 状态
                                        │
                                        ▼
                                  WebView2 ──▶ React App.tsx
                                                │
                                                ├── Dashboard (unread card)
                                                └── EventLog (unread dots + filter)
```

### 1.3 核心设计决策

| 决策点 | 方案 | 理由 |
|--------|------|------|
| 未读计数存储 | `EventHistory._entries` 中每个 `EventEntry` 的 `IsRead` 字段 | 数据与状态内聚，序列化自然持久化 |
| 闪烁停止条件 | 移除 `_maxBlinkTicks` 自动停止，仅用户交互停止 | 符合 QQ/微信行为模型 |
| 单击行为 | 单击 = 打开面板 + 清除未读（替代原双击） | 降低操作门槛 |
| 前端筛选 | 客户端内存筛选（非 C# 端） | 数据量小（≤500 条），避免 IPC 往返 |
| C#→JS 通信 | 扩展现有 `state_sync` / `event_push` 消息 | 最小化协议变更 |

---

## 2. 任务分解

### 2.1 任务编号规则

`L<层>-<序号>` ：层号越小越底层，同层任务可并行。

---

### 层 0：数据模型 & i18n 字符串（无依赖，可并行）

#### T0.1 — EventEntry 新增 IsRead 字段

| 属性 | 内容 |
|------|------|
| **文件** | `src/HooksNotifier/Models.cs` |
| **改动量** | ~3 行 |
| **描述** | 在 `EventEntry` record 中新增 `bool IsRead = false` 参数 |
| **兼容性** | 默认值 `false` 确保旧 JSON 反序列化自动兼容 |
| **验收** | 编译通过；旧 `event_history.json` 反序列化不报错 |

**改动明细：**
```csharp
// Before (Models.cs L5-11)
internal sealed record EventEntry(
    DateTime Timestamp,
    string Level,
    string EventName,
    string Summary,
    string Detail = ""
);

// After
internal sealed record EventEntry(
    DateTime Timestamp,
    string Level,
    string EventName,
    string Summary,
    string Detail = "",
    bool IsRead = false
);
```

---

#### T0.2 — i18n 字符串定义（C# 端 + JS 端）

| 属性 | 内容 |
|------|------|
| **文件** | `src/HooksNotifier/i18n/en.json`, `zh.json`, `webui/src/i18n.ts` |
| **改动量** | ~25 行新增 |
| **描述** | 为所有新增 UI 文案定义中英文 key |
| **验收** | 所有新增 key 有 en/zh 对应值 |

**新增 i18n Key 清单：**

| Key | English | 中文 |
|-----|---------|------|
| `tray.unread` | `{0} unread notifications` | `{0} 条未读通知` |
| `tray.unread_max` | `999+ unread notifications` | `999+ 条未读通知` |
| `tray.unread_title` | `{0} — Claude Code Hooks Notifier` | `{0} — Claude Code Hooks Notifier` |
| `menu.view_notifications` | `View Notifications ({0} unread)` | `查看通知（{0} 条未读）` |
| `menu.view_notifications_none` | `View Notifications` | `查看通知` |
| `dashboard.unread` | `Unread` | `未读` |
| `event_log.mark_all_read` | `Mark All Read` | `全部标为已读` |
| `event_log.filter_all` | `All` | `全部` |
| `event_log.filter_unread` | `Unread` | `未读` |

> **注意：** `webui/src/i18n.ts` 和 C# `i18n/*.json` 是**独立的两套** i18n 文件，需分别更新。C# 端只需要 tray/menu 相关 key；JS 端需要 dashboard/event_log 相关 key。

---

### 层 1：数据层（依赖层 0）

#### T1.1 — EventHistory 新增未读管理方法

| 属性 | 内容 |
|------|------|
| **文件** | `src/HooksNotifier/EventHistory.cs` |
| **改动量** | ~40 行新增 |
| **依赖** | T0.1 |
| **描述** | 新增 `UnreadCount` 属性、`MarkAllRead()`、`MarkRead(int)`、`GetUnread()` 方法 |
| **验收** | 单元测试覆盖所有新增方法 |

**新增 API 签名：**
```csharp
// EventHistory 新增成员
public static int UnreadCount { get; }              // _entries.Count(e => !e.IsRead)
public static int UnreadCountByLevel(string level)   // 按 Level 统计未读
public static void MarkAllRead()                     // 遍历全部标为已读 + SaveToFile()
public static void MarkRead(int index)               // 按索引标为已读 + SaveToFile()
public static List<EventEntry> GetUnread()           // 返回所有 IsRead==false 的条目
```

**实现要点：**
- 所有方法内部加 `lock(_entries)`，与现有方法保持一致
- `MarkAllRead()` 和 `MarkRead()` 需要调用 `SaveToFile()` 持久化
- `MarkRead(int index)` 参数是 `_entries` 的绝对索引（非倒序）

---

#### T1.2 — EventHistory.Counts 扩展 unread 维度

| 属性 | 内容 |
|------|------|
| **文件** | `src/HooksNotifier/EventHistory.cs` |
| **改动量** | ~10 行 |
| **依赖** | T1.1 |
| **描述** | 为现有 `Counts` 元组增加 `unread` 字段，或新增独立方法 |
| **验收** | `EventHistory.UnreadCount` 返回值与 `Counts` 中未读数一致 |

---

### 层 2：托盘后端逻辑（依赖层 1）

#### T2.1 — 持续闪烁模式

| 属性 | 内容 |
|------|------|
| **文件** | `src/HooksNotifier/TrayMode.cs` |
| **改动量** | ~15 行修改 |
| **依赖** | T1.1 |
| **描述** | 移除闪烁定时器的自动停止逻辑，使闪烁持续到用户交互 |
| **验收** | 发送测试通知 → 图标无限闪烁，直到单击托盘图标才停止 |

**改动明细：**

1. `StartBlinking()` 方法不再设置 `_maxBlinkTicks` 上限，或设为 `int.MaxValue`
2. `_blinkTimer.Tick` 处理器移除 `if (_blinkTick >= _maxBlinkTicks) StopBlinking()` 逻辑
3. `StopBlinking()` 保持不变

```csharp
// TrayMode.cs — Blink timer tick handler (L114-121)
// Before:
_blinkTimer.Tick += (_, _) =>
{
    _isHighlighted = !_isHighlighted;
    _trayIcon.Icon = _isHighlighted ? IconHelper.Highlighted : IconHelper.Normal;
    _blinkTick++;
    if (_blinkTick >= _maxBlinkTicks)
        StopBlinking();
};

// After:
_blinkTimer.Tick += (_, _) =>
{
    _isHighlighted = !_isHighlighted;
    _trayIcon.Icon = _isHighlighted ? IconHelper.Highlighted : IconHelper.Normal;
};
```

---

#### T2.2 — 动态 Tooltip（悬停显示未读计数）

| 属性 | 内容 |
|------|------|
| **文件** | `src/HooksNotifier/TrayMode.cs` |
| **改动量** | ~25 行新增 |
| **依赖** | T1.1, T0.2 |
| **描述** | `NotifyIcon.Text` 根据 `EventHistory.UnreadCount` 动态更新 |
| **验收** | 发送通知 → 悬停托盘图标 → tooltip 显示"1 条未读通知"；单击托盘 → tooltip 恢复默认 |

**实现方案：** 提取一个 `UpdateTooltip()` 方法，在以下时机调用：
- `OnIpcMessage` 处理完 `toast` 消息后
- 用户单击托盘图标（标记已读后）
- `MainWindow` 中"全部标为已读"操作后

```csharp
private static void UpdateTooltip()
{
    if (_trayIcon == null) return;
    var unread = EventHistory.UnreadCount;
    if (unread == 0)
    {
        _trayIcon.Text = "Claude Code Hooks Notifier";
    }
    else
    {
        var display = unread > 999 ? "999+" : unread.ToString();
        var prefix = I18n.Get("tray.unread", display);
        _trayIcon.Text = I18n.Get("tray.unread_title", prefix);
    }
}
```

---

#### T2.3 — 单击打开面板 + 清除未读

| 属性 | 内容 |
|------|------|
| **文件** | `src/HooksNotifier/TrayMode.cs` |
| **改动量** | ~20 行修改 |
| **依赖** | T1.1, T2.1 |
| **描述** | 托盘图标单击改为：停止闪烁 → 全部标记已读 → 打开 MainWindow（若已打开则聚焦） |
| **验收** | 单击托盘 → 面板打开，闪烁停止，tooltip 恢复，面板 Event Log 显示无未读高亮 |

**改动明细：**

```csharp
// TrayMode.cs L84-88 — MouseClick handler
// Before:
_trayIcon.MouseClick += (_, e) =>
{
    if (e.Button == MouseButtons.Left)
        StopBlinking();
};

// After:
_trayIcon.MouseClick += (_, e) =>
{
    if (e.Button == MouseButtons.Left)
        OpenDashboard();
};
```

新增 `OpenDashboard()` 方法：
```csharp
private static void OpenDashboard()
{
    StopBlinking();
    EventHistory.MarkAllRead();
    UpdateTooltip();
    try
    {
        if (_mainWindow == null || _mainWindow.IsDisposed)
        {
            _mainWindow = new MainWindow();
            _mainWindow.Show();
        }
        else
        {
            _mainWindow.Activate();
        }
    }
    catch (Exception ex)
    {
        Log.Error($"Dashboard window error: {ex.Message}");
        ToastService.ShowBalloon("Dashboard Error", $"Could not open dashboard:\n{ex.Message}");
    }
}
```

同时修改 `DoubleClick` 处理器调用 `OpenDashboard()`，避免代码重复。

---

#### T2.4 — 右键菜单新增"查看通知"项

| 属性 | 内容 |
|------|------|
| **文件** | `src/HooksNotifier/TrayMode.cs` |
| **改动量** | ~15 行新增 |
| **依赖** | T0.2, T2.3 |
| **描述** | 在 `BuildMenu()` 顶部新增"查看通知 (N 条未读)"菜单项 |
| **验收** | 右键托盘 → 菜单首项显示未读计数 → 点击后打开面板，与单击行为一致 |

**实现要点：**
- 菜单项使用 `ToolStripMenuItem`，动态文本通过 `_unreadMenuItem` 字段持有引用
- 在 `OnIpcMessage` 中更新菜单项文本（类似已有的 `UpdateStatusMenu()` 模式）
- 点击时调用 `OpenDashboard()`

---

#### T2.5 — MainWindow 扩展 state_sync 包含未读数据

| 属性 | 内容 |
|------|------|
| **文件** | `src/HooksNotifier/MainWindow.cs` |
| **改动量** | ~15 行修改 |
| **依赖** | T1.1 |
| **描述** | `GetCurrentState()` 和 `PushEvent()` 中增加 `isRead` 和 `unreadCount` 字段 |
| **验收** | 面板打开时接收到的 `state_sync` 消息包含 `unreadCount` 和每条 event 的 `isRead` |

**改动明细 — GetCurrentState()：**
```csharp
// MainWindow.cs — GetCurrentState() 返回对象增加:
unreadCount = EventHistory.UnreadCount,
allEvents = EventHistory.Entries.Reverse().Take(100).Select(e => new
{
    timestamp = e.Timestamp.ToString("HH:mm:ss"),
    level = e.Level,
    eventName = e.EventName,
    summary = e.Summary,
    detail = e.Detail ?? "",
    isRead = e.IsRead        // ← 新增
}),
```

**改动明细 — PushEvent()：**
```csharp
// MainWindow.cs — PushEvent() payload 增加:
isRead = entry.IsRead,       // ← 新增
unreadCount = EventHistory.UnreadCount  // ← 新增
```

---

#### T2.6 — MainWindow 处理 JS 端未读操作消息

| 属性 | 内容 |
|------|------|
| **文件** | `src/HooksNotifier/MainWindow.cs` |
| **改动量** | ~25 行新增 |
| **依赖** | T1.1, T2.2 |
| **描述** | `OnJsMessage` 新增 `mark_all_read` 和 `mark_read` 消息处理 |
| **验收** | 面板点击"全部标为已读"→ 托盘 tooltip 同步更新；点击单条 → 该条 IsRead=true |

**新增 JS→C# 消息：**

| 消息 type | payload | C# 处理 |
|-----------|---------|---------|
| `"mark_all_read"` | 无 | `EventHistory.MarkAllRead()` → `PushState("state_sync", ...)` → `TrayMode.UpdateTooltip()` |
| `"mark_read"` | `{ index: number }` | `EventHistory.MarkRead(index)` → `PushState("state_sync", ...)` → `TrayMode.UpdateTooltip()` |

**注意：** `mark_read` 的 index 需要约定语义。由于前端使用 `Reverse()` 倒序展示，建议 index 为 `_entries` 正向索引。或者通过 timestamp + eventName 组合定位。推荐方案：C# 端给每条 event 分配一个稳定的 ID（如 `_entries` 索引），在 `state_sync` 中传递。

---

### 层 3：React 前端（依赖层 2）

#### T3.1 — AppState / EventRow 类型扩展

| 属性 | 内容 |
|------|------|
| **文件** | `webui/src/App.tsx` |
| **改动量** | ~5 行 |
| **依赖** | T2.5 (API 契约确定) |
| **描述** | `EventRow` interface 增加 `isRead?: boolean`；`AppState` 增加 `unreadCount: number` |
| **验收** | TypeScript 编译无报错 |

---

#### T3.2 — Dashboard 新增未读卡片

| 属性 | 内容 |
|------|------|
| **文件** | `webui/src/App.tsx` |
| **改动量** | ~10 行 |
| **依赖** | T3.1, T0.2 |
| **描述** | 在 Dashboard 的 5 张卡片中新增第 6 张"Unread"卡片 |
| **验收** | 面板 Dashboard 显示未读计数卡片，有未读时数字醒目 |

```tsx
// 在 cards 数组中插入 (位置 1，紧跟 Total)
{ title: t("dashboard.unread"), value: state.unreadCount ?? 0, accent: "border-t-amber-500" },
```

卡片顺序：Total → **Unread (new)** → P0 Blinks → Toasts → Subagents → Tasks  
grid 从 `grid-cols-5` 改为 `grid-cols-6`

---

#### T3.3 — Event Log 未读状态指示

| 属性 | 内容 |
|------|------|
| **文件** | `webui/src/App.tsx` |
| **改动量** | ~20 行 |
| **依赖** | T3.1 |
| **描述** | 未读条目左侧显示彩色圆点 + 浅色背景；已读条目无标记 |
| **验收** | 未读条目有蓝色/橙色圆点和高亮背景；标记已读后圆点消失 |

**实现要点：**
```tsx
// Event Log 表格行增加:
<tr className={`border-t hover:bg-muted/30 cursor-pointer ${!e.isRead ? 'bg-amber-50' : ''}`} ...>
  <td className="px-3 py-1.5 text-muted-foreground">
    {!e.isRead && <span className={`inline-block w-2 h-2 rounded-full mr-1.5 ${e.level === 'P0' ? 'bg-red-500' : e.level === 'P0.5' ? 'bg-orange-500' : 'bg-blue-500'}`} />}
    {e.timestamp}
  </td>
  ...
</tr>
```

未读条目置顶排序：在渲染前对 `allEvents` 排序 — 未读在前（按时间倒序），已读在后（按时间倒序）。

---

#### T3.4 — Event Log 筛选标签

| 属性 | 内容 |
|------|------|
| **文件** | `webui/src/App.tsx` |
| **改动量** | ~40 行 |
| **依赖** | T3.1, T3.3, T0.2 |
| **描述** | 在 Event Log 顶部增加筛选标签：全部 / 未读 / P0 / P0.5 / Toast |
| **验收** | 点击不同标签 → 列表按条件筛选；默认"全部"；从托盘进入时默认"未读" |

**实现要点：**
- 使用本地 state `filter: string` 默认为 `"all"`
- 当 `state_sync` 中携带 `defaultFilter: "unread"` 标记时（托盘单击进入），自动切换到"未读"筛选
- 筛选逻辑在 `useMemo` 中完成，避免重复计算

---

#### T3.5 — "全部标为已读"按钮

| 属性 | 内容 |
|------|------|
| **文件** | `webui/src/App.tsx` |
| **改动量** | ~10 行 |
| **依赖** | T3.3, T2.6, T0.2 |
| **描述** | Event Log 右上角增加"全部标为已读"按钮 |
| **验收** | 点击按钮 → 所有未读条目标为已读 → 圆点消失 → 发送 `mark_all_read` 到 C# |

---

#### T3.6 — 单条点击标记已读

| 属性 | 内容 |
|------|------|
| **文件** | `webui/src/App.tsx` |
| **改动量** | ~10 行 |
| **依赖** | T3.3, T2.6 |
| **描述** | 点击未读条目行或展开详情 → 该条标记为已读 |
| **验收** | 点击未读行 → 圆点消失 + 背景恢复正常 + 发送 `mark_read` 到 C# |

---

#### T3.7 — 前端 i18n 新增 key

| 属性 | 内容 |
|------|------|
| **文件** | `webui/src/i18n.ts` |
| **改动量** | ~15 行 (en + zh) |
| **依赖** | T0.2 |
| **描述** | 在 `strings.en` 和 `strings.zh` 中增加前端需要的新 key |
| **验收** | 所有新增 UI 文案中英文正常显示 |

> 注：T3.7 与 T0.2 内容重叠，T0.2 是定义 key 清单，T3.7 是往 i18n.ts 中写入。实际执行可合并。

---

## 3. 依赖关系图

```
                        ┌─────────────────────┐
                        │   需求报告 (已完成)    │
                        └─────────┬───────────┘
                                  │
              ┌───────────────────┼───────────────────┐
              ▼                   ▼                   ▼
        ┌──────────┐       ┌──────────┐        ┌──────────┐
        │   T0.1   │       │ T0.2 C#  │        │ T0.2 JS  │
        │ Models   │       │ i18n JSON│        │ i18n.ts  │
        │ IsRead   │       │ 新 key   │        │ 新 key   │
        └────┬─────┘       └────┬─────┘        └────┬─────┘
             │                  │                   │
             └────────┬─────────┘                   │
                      ▼                             │
                ┌──────────┐                        │
                │   T1.1   │                        │
                │EventHistory                      │
                │ 未读方法  │                        │
                └────┬─────┘                        │
                     │                              │
                ┌──────────┐                        │
                │   T1.2   │                        │
                │ Counts   │                        │
                │ 扩展     │                        │
                └────┬─────┘                        │
                     │                              │
        ┌────────────┼────────────┐                 │
        ▼            ▼            ▼                 │
  ┌──────────┐ ┌──────────┐ ┌──────────┐           │
  │   T2.1   │ │   T2.2   │ │   T2.5   │           │
  │ 持续闪烁  │ │ 动态     │ │ PushState│           │
  │          │ │ Tooltip  │ │ 扩展     │           │
  └────┬─────┘ └────┬─────┘ └────┬─────┘           │
       │            │            │                  │
       └──────┬─────┘            │                  │
              ▼                  │                  │
        ┌──────────┐             │                  │
        │   T2.3   │             │                  │
        │ 单击开    │             │                  │
        │ 面板+已读 │             │                  │
        └────┬─────┘             │                  │
             │                   │                  │
        ┌──────────┐             │                  │
        │   T2.4   │             │                  │
        │ 右键菜单  │             │                  │
        │ 查看通知  │             │                  │
        └────┬─────┘             │                  │
             │                   │                  │
             └────────┬──────────┘                  │
                      ▼                             │
                ┌──────────┐                        │
                │   T2.6   │                        │
                │ JS→C#    │                        │
                │ 未读消息  │                        │
                └────┬─────┘                        │
                     │                              │
                     └──────────┬───────────────────┘
                                ▼
                    ┌────────────────────┐
                    │  层 3: React 前端   │
                    │  T3.1 ~ T3.7       │
                    └────────────────────┘
```

---

## 4. 并行开发策略

### 4.1 并行分组

```
═══════════════════════════════════════════════════════════
  阶段 A (可并行 3 路)
═══════════════════════════════════════════════════════════
  ┌──────────┐   ┌──────────────┐   ┌──────────────┐
  │ 路 A1    │   │ 路 A2        │   │ 路 A3        │
  │ T0.1     │   │ T0.2 C# 端   │   │ T0.2 JS 端   │
  │ Models   │   │ en.json      │   │ i18n.ts      │
  │ IsRead   │   │ zh.json      │   │ 新增 key     │
  └──────────┘   └──────────────┘   └──────────────┘
       │               │                   │
       └───────┬───────┘                   │
               ▼                           │
═══════════════════════════════════════════════════════════
  阶段 B (合并后)
═══════════════════════════════════════════════════════════
  ┌──────────────────────────────────────┐
  │ T1.1 + T1.2                          │
  │ EventHistory 未读管理完整实现          │
  └──────────────────────────────────────┘
               │
═══════════════════════════════════════════════════════════
  阶段 C (C# 后端 — T2.1~T2.4 可部分并行)
═══════════════════════════════════════════════════════════
  ┌──────────┐   ┌──────────┐   ┌──────────┐
  │ 路 C1    │   │ 路 C2    │   │ 路 C3    │
  │ T2.1     │   │ T2.2     │   │ T2.5     │
  │ 持续闪烁  │   │ Tooltip  │   │ state    │
  └──────────┘   └──────────┘   │ 扩展     │
       │               │        └──────────┘
       └───────┬───────┘
               ▼
  ┌──────────┐   ┌──────────┐
  │ T2.3     │   │ T2.6     │
  │ 单击面板  │   │ JS→C#    │
  └────┬─────┘   └──────────┘
       │
  ┌──────────┐
  │ T2.4     │
  │ 右键菜单  │
  └──────────┘
               │
═══════════════════════════════════════════════════════════
  阶段 D (前端 — T3.1~T3.7 全部可并行)
═══════════════════════════════════════════════════════════
  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐
  │ T3.1     │   │ T3.2     │   │ T3.3     │   │ T3.4     │
  │ 类型扩展  │   │ 未读卡片  │   │ 未读指示  │   │ 筛选标签  │
  └──────────┘   └──────────┘   └──────────┘   └──────────┘

  ┌──────────┐   ┌──────────┐   ┌──────────┐
  │ T3.5     │   │ T3.6     │   │ T3.7     │
  │ 全部已读  │   │ 单条已读  │   │ i18n key │
  └──────────┘   └──────────┘   └──────────┘
```

### 4.2 并行可行性分析

| 任务对 | 能否并行 | 原因 |
|--------|---------|------|
| T0.1 ∥ T0.2 | **是** | 不同文件，零依赖 |
| T0.2 C# ∥ T0.2 JS | **是** | 两套独立 i18n 系统 |
| T2.1 ∥ T2.2 ∥ T2.5 | **是** | 同文件不同方法，可分别编写再合并 |
| T2.3 ∥ T2.6 | **是** | 不同文件 (TrayMode vs MainWindow) |
| T2.4 ∥ T2.6 | **是** | 不同文件 |
| T3.1~T3.7 | **部分** | 全部在 App.tsx 中，建议同一人顺序完成，但 i18n.ts (T3.7) 可独立并行 |
| 阶段 C (C#) ∥ 阶段 D (前端) | **部分** | 前端依赖 C# 的 API 契约（state_sync 消息格式）。如果先约定接口规范（接口先行），前端可 Mock 数据开发 |

### 4.3 推荐并行策略

**策略：接口先行 + 双轨开发**

1. **先锁定 API 契约**（阶段 A 完成后，花 10 分钟编写契约文档）
2. **C# 轨道**：阶段 B → 阶段 C（1 人）
3. **前端轨道**：阶段 D（1 人，使用 Mock 数据进行开发）
4. 两队独立开发，最后联调集成

---

## 5. 开发阶段与时间线

### 5.1 阶段划分

| 阶段 | 任务 | 预估人时 | 可并行 | 输出物 |
|------|------|---------|--------|--------|
| **S0: 接口契约** | 约定 state_sync / event_push 新 JSON 格式 | 0.5h | 否 | 接口文档（几段 JSON 示例） |
| **S1: 数据层** | T0.1 + T0.2 + T1.1 + T1.2 | 2h | 部分 | Models.cs, EventHistory.cs, i18n 文件 |
| **S2: C# 后端** | T2.1 ~ T2.6 | 4h | 部分 | TrayMode.cs, MainWindow.cs |
| **S3: React 前端** | T3.1 ~ T3.7 | 5h | 与 S2 并行 | App.tsx, i18n.ts |
| **S4: 集成联调** | 前后端联调、修复不一致 | 2h | 否 | 可工作的完整功能 |
| **S5: 测试** | 见第 6 节 | 3h | 部分 | 测试报告 |
| **S6: 打包发布** | 版本号更新 + 构建 + 安装器 | 0.5h | 否 | Setup.exe |

**总预估：** ~17 人时（含测试）。双人并行开发可压缩到 **~10 小时（约 1.5 个工作日）**。

### 5.2 Gantt 图（双人并行）

```
         Day 1                    Day 2
         ───────────              ───────────
S0: 接口  ██
S1: 数据  ████████
S2: C#    ████████████████████
S3: 前端        ████████████████████████
S4: 联调                          ████████
S5: 测试                            ████████████
S6: 发布                                  ██
```

---

## 6. 测试策略

### 6.1 测试矩阵

| 测试层 | 类型 | 覆盖范围 | 工具 |
|--------|------|---------|------|
| 单元测试 | 自动化 | EventHistory 所有新方法 | xUnit / NUnit |
| 集成测试 | 手动 + 脚本 | IPC → TrayMode → EventHistory 完整链路 | PowerShell 脚本 |
| UI 测试 | 手动 | 前端所有交互 | 浏览器 DevTools |
| 回归测试 | 手动 | 现有功能（toast、菜单、权限对话框） | 检查清单 |
| i18n 测试 | 手动 | 中英文切换全覆盖 | 检查清单 |

### 6.2 单元测试用例（T1.1）

```csharp
// EventHistoryTests.cs
[Fact] public void UnreadCount_AllNew_ReturnsTotal() { }
[Fact] public void UnreadCount_AfterMarkAllRead_ReturnsZero() { }
[Fact] public void UnreadCount_AfterMarkRead_DecrementsByOne() { }
[Fact] public void MarkAllRead_PersistsToFile() { }
[Fact] public void MarkRead_InvalidIndex_NoThrow() { }
[Fact] public void GetUnread_ReturnsOnlyUnreadEntries() { }
[Fact] public void UnreadCount_MaxLimit_Handles999Plus() { }
[Fact] public void Deserialize_OldFormat_IsReadDefaultsToFalse() { }  // 兼容性
```

### 6.3 集成测试场景

| 编号 | 场景 | 预期结果 |
|------|------|---------|
| IT-1 | `hooks-notify.exe` 发送 P0 事件 → 托盘 | 图标开始闪烁（持续），tooltip 显示"1 条未读通知" |
| IT-2 | 发送 3 个不同类型事件 | tooltip 显示"3 条未读通知"，右键菜单显示正确 |
| IT-3 | 单击托盘图标 | 闪烁停止，面板打开，tooltip 恢复默认，Event Log 无未读高亮 |
| IT-4 | 面板中点击"全部标为已读" | 所有条目圆点消失，tooltip 同步更新（即使面板已打开） |
| IT-5 | 重启托盘进程 | 未读状态从 event_history.json 恢复，tooltip 正确 |
| IT-6 | 中英文切换 | tooltip、菜单、面板文案全部切换正确 |
| IT-7 | 超过 999 条未读 | tooltip 显示"999+ 条未读通知" |
| IT-8 | 闪烁中再次收到通知 | 闪烁不中断，未读计数 +1，tooltip 更新 |
| IT-9 | 面板未打开时发送通知 → 再打开面板 | 面板打开后 event_push 中的 isRead 正确 (false) |
| IT-10 | 单条点击标为已读 | 该条圆点消失，其他未读不变，C# 端持久化正确 |

### 6.4 回归测试检查清单

- [ ] `--tray` 启动不报错
- [ ] 右键菜单所有原有项功能正常（Configure Hooks, Update Hook Path, Clear counters, Language, Open at Login, About, Exit）
- [ ] 双击托盘仍然可以打开面板
- [ ] `--hook` 模式 stdin 处理正常
- [ ] WinRT Toast 通知正常弹出
- [ ] 权限对话框（Allow/Deny）正常
- [ ] 设置面板中语言切换正常
- [ ] 设置面板中 Hook Path 更新正常
- [ ] Hook 事件开关（enable/disable）正常
- [ ] 清空历史记录功能正常
- [ ] 开机自启注册表项正常
- [ ] `--register` AUMID 注册正常
- [ ] `--configure-hooks` 功能正常
- [ ] 安装器构建正常
- [ ] 安装后程序正常运行

### 6.5 测试环境

| 环境 | 用途 |
|------|------|
| Windows 11 开发机 | 主测试环境 |
| 全新安装测试 | 模拟用户首次使用 |
| 升级安装测试 | 旧版本 event_history.json 兼容性 |

---

## 7. 风险与缓解

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|---------|
| **TrayMode.cs 代码膨胀** | 中 | 中 | 当前 ~400 行，新增后 ~500 行，仍在可维护范围。若超过 600 行考虑拆分 |
| **闪烁 + WebView2 同时启动资源竞争** | 低 | 低 | WebView2 初始化是异步的，不影响 UI 线程 |
| **event_history.json 频繁写入** | 低 | 低 | 仅在 Add/MarkAllRead/MarkRead/Clear 时写入，频率很低 |
| **前端筛选性能** | 低 | 低 | 500 条数据客户端筛选，React useMemo 优化，性能无问题 |
| **旧版 event_history.json 无 IsRead 字段** | 低 | 中 | `record` 默认值 `= false` 自动兼容，已通过 T0.1 的兼容性测试覆盖 |
| **前后端接口不一致** | 中 | 中 | 阶段 S0 先锁定接口契约（JSON 格式示例），双方按契约开发 |
| **WinForms NotifyIcon.Text 长度限制** | 低 | 低 | Windows 托盘 tooltip 支持 128 字符，我们的最大长度约 60 字符 |

---

## 附录 A：接口契约（C# ↔ JS）

### A.1 state_sync（初始化 + 全量同步）

```json
{
  "type": "state_sync",
  "payload": {
    "counts": { "total": 128, "p0": 12, "p05": 8, "toast": 89, "stateful": 19 },
    "unreadCount": 3,
    "subagentCount": 5,
    "taskCount": 2,
    "recentEvents": [
      {
        "timestamp": "14:32:01",
        "level": "P0",
        "eventName": "StopFailure",
        "summary": "Claude Code process exited abnormally",
        "detail": "Exit code: 1",
        "isRead": false
      }
    ],
    "allEvents": [ /* ... same shape, max 100 entries ... */ ],
    "language": "en",
    "hookConfig": { "Notification(idle_prompt)": true, /* ... */ }
  }
}
```

### A.2 event_push（实时推送）

```json
{
  "type": "event_push",
  "payload": {
    "timestamp": "14:35:22",
    "level": "P0.5",
    "eventName": "TaskCompleted",
    "summary": "Task completed: fix-login-bug",
    "detail": "",
    "isRead": false,
    "unreadCount": 4
  }
}
```

### A.3 JS → C# 新增消息

```json
// 全部标为已读
{ "type": "mark_all_read" }

// 单条标为已读
{ "type": "mark_read", "payload": { "index": 42 } }
```

---

## 附录 B：任务完成检查清单

### 数据层
- [ ] T0.1 EventEntry.IsRead 字段添加
- [ ] T0.2 C# i18n JSON 新 key 添加
- [ ] T0.2 JS i18n.ts 新 key 添加
- [ ] T1.1 EventHistory.UnreadCount 属性
- [ ] T1.1 EventHistory.MarkAllRead() 方法
- [ ] T1.1 EventHistory.MarkRead(int) 方法
- [ ] T1.1 EventHistory.GetUnread() 方法
- [ ] T1.2 EventHistory.Counts 扩展

### 后端
- [ ] T2.1 持续闪烁模式
- [ ] T2.2 UpdateTooltip() 方法
- [ ] T2.3 OpenDashboard() 方法
- [ ] T2.4 右键菜单"查看通知"项
- [ ] T2.5 MainWindow.GetCurrentState() 扩展
- [ ] T2.5 MainWindow.PushEvent() 扩展
- [ ] T2.6 MainWindow.OnJsMessage() 新增 mark_all_read
- [ ] T2.6 MainWindow.OnJsMessage() 新增 mark_read

### 前端
- [ ] T3.1 EventRow / AppState 类型扩展
- [ ] T3.2 Dashboard 未读计数卡片
- [ ] T3.3 Event Log 未读圆点 + 背景高亮
- [ ] T3.4 Event Log 筛选标签
- [ ] T3.5 "全部标为已读"按钮
- [ ] T3.6 单条点击标为已读
- [ ] T3.7 前端 i18n key 添加

### 集成
- [ ] 端到端流程：通知 → 闪烁 → tooltip → 单击 → 面板打开
- [ ] 端到端流程：面板内标记已读 → tooltip 同步
- [ ] 端到端流程：重启恢复未读状态

### 测试
- [ ] 单元测试 8 个用例通过
- [ ] 集成测试 10 个场景通过
- [ ] 回归测试 15 项检查清单全部通过
- [ ] 中英文 i18n 全覆盖

### 发布
- [ ] 版本号更新（5 个文件）
- [ ] 全量构建通过
- [ ] grep -rn 验证无旧版本号残留
- [ ] 安装器构建成功
- [ ] Git commit
